#!/usr/bin/env bash
# Deploys one release of Fresh Orbit on this server.
#
#   infra/scripts/deploy.sh <image-tag>            e.g. deploy.sh v1.4.0  (or sha-1a2b3c4)
#   infra/scripts/deploy.sh --rollback              redeploy the previous tag
#
# Steps: pull images → start Postgres → backup (local) → run migrations (+ sample data when SEED_SAMPLE_DATA=true)
# → `up -d` → wait until every service is healthy/running → record the tag. If the new release does not become
# healthy, the previous tag is started again and the script exits non-zero.
#
# Migrations are not rolled back automatically. Write expand/contract migrations so the previous release still runs
# on the new schema; if a schema change must be undone, restore the pre-deploy backup (docs/runbooks/backup-restore.md).
#
# Environment: HEALTH_TIMEOUT (seconds, default 240), SKIP_PRE_DEPLOY_BACKUP=true to skip the local backup,
# SKIP_PULL=true to use images already on the host (local testing, registry outage).
#
# Runs on the Linux server (bash 4+, flock) and also on macOS's bash 3.2 without flock (E2E-7: a mkdir lock is used
# instead), so it can drive the local fo-e2e stack (docs/runbooks/e2e-check.md section 5).

# Before anything else (even `set -o pipefail`): `sh deploy.sh` with a non-bash sh, or a bash older than 3.2, would
# otherwise fail half-way with an obscure error. POSIX syntax only up to the `fi`.
if [ -z "${BASH_VERSION:-}" ]; then
  echo "deploy.sh needs bash 3.2 or later; run it as 'bash infra/scripts/deploy.sh' or './infra/scripts/deploy.sh'." >&2
  exit 1
fi
case $BASH_VERSION in
  1.* | 2.* | 3.0.* | 3.1.*)
    echo "deploy.sh needs bash 3.2 or later (this is bash $BASH_VERSION)." >&2
    exit 1
    ;;
esac
set -euo pipefail

# shellcheck source=infra/scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

HEALTH_TIMEOUT=${HEALTH_TIMEOUT:-240}
STABILITY_SECONDS=${STABILITY_SECONDS:-15}

usage() {
  # The header comment (line 2 up to the first line that is not a comment).
  awk 'NR == 1 { next } /^#/ { sub(/^# ?/, ""); print; next } { exit }' "${BASH_SOURCE[0]}"
  exit 2
}

# check_services: 0 = all running (and healthy where a healthcheck exists), 1 = still starting, 2 = failed.
# Prints the services that are not ready.
check_services() {
  local svc id state health restarts not_ready=() failed=()
  # A while-read loop on fd 3, not mapfile (bash 3.2 on macOS has none); fd 3 keeps docker from eating the list.
  while IFS= read -r svc <&3; do
    [[ -n $svc && $svc != migrations ]] || continue
    id=$(compose ps -q "$svc" 2>/dev/null || true)
    if [[ -z $id ]]; then
      not_ready+=("$svc:missing")
      continue
    fi
    read -r state health restarts < <(docker inspect -f \
      '{{.State.Status}} {{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}} {{.RestartCount}}' "$id")
    if [[ $state == restarting || $state == exited || $state == dead || $health == unhealthy || $restarts -gt 0 ]]; then
      failed+=("$svc:$state/$health/restarts=$restarts")
    elif [[ $state != running || ($health != healthy && $health != none) ]]; then
      not_ready+=("$svc:$state/$health")
    fi
  done 3< <(compose config --services)
  if ((${#failed[@]})); then
    printf '%s\n' "${failed[*]}"
    return 2
  fi
  if ((${#not_ready[@]})); then
    printf '%s\n' "${not_ready[*]}"
    return 1
  fi
  return 0
}

wait_healthy() {
  local deadline=$((SECONDS + HEALTH_TIMEOUT)) status=0 detail=""
  while ((SECONDS < deadline)); do
    status=0
    detail=$(check_services) || status=$?
    if ((status == 0)); then
      # Services without a healthcheck (worker) must also stay up for a short while.
      sleep "$STABILITY_SECONDS"
      status=0
      detail=$(check_services) || status=$?
      ((status == 0)) && return 0
    fi
    if ((status == 2)); then
      log "Unhealthy: $detail"
      return 1
    fi
    sleep 5
  done
  log "Timed out after ${HEALTH_TIMEOUT}s waiting for: $detail"
  return 1
}

# Best-effort check through Caddy (TLS may still be issuing on a first deploy, so only a warning).
smoke_check() {
  local domain
  domain=$(env_value DOMAIN)
  [[ -n $domain ]] || return 0
  if curl -fsS -o /dev/null -m 10 --resolve "api.$domain:443:127.0.0.1" "https://api.$domain/health"; then
    log "https://api.$domain/health answers through Caddy."
  else
    log "WARNING: https://api.$domain/health did not answer through Caddy (certificate still issuing?)."
  fi
}

# Removes this registry's older fresh-orbit images only (never another registry's or a local build's), then dangling
# layers. SKIP_IMAGE_PRUNE=true leaves every image alone (a shared Docker host, e.g. the local e2e check).
prune_images() {
  local keep_current=$1 keep_previous=${2:-} registry ref
  [[ ${SKIP_IMAGE_PRUNE:-false} == true ]] && return 0
  registry=$(env_value IMAGE_REGISTRY ghcr.io/mosasivamani)
  while read -r ref; do
    [[ $ref == "$registry"/fresh-orbit-api:* || $ref == "$registry"/fresh-orbit-worker:* \
      || $ref == "$registry"/fresh-orbit-migrations:* || $ref == "$registry"/fresh-orbit-web:* \
      || $ref == "$registry"/fresh-orbit-admin:* ]] || continue
    [[ $ref == *":$keep_current" ]] && continue
    [[ -n $keep_previous && $ref == *":$keep_previous" ]] && continue
    docker image rm "$ref" >/dev/null 2>&1 || true
  done < <(docker image ls --format '{{.Repository}}:{{.Tag}}')
  docker image prune -f >/dev/null || true
}

# One deploy at a time. With flock (Linux): a lock on deploy.lock, released by the kernel however the script ends.
# Without flock (macOS): an atomic mkdir of deploy.lock.d holding the owner's PID, removed on exit; a lock whose PID
# no longer runs (the deploy was killed with -9) is stale and is taken over.
acquire_deploy_lock() {
  if command -v flock >/dev/null 2>&1; then
    exec 9>"$FO_STATE_DIR/deploy.lock"
    flock -n 9 || die "Another deploy is running."
    return 0
  fi
  local dir="$FO_STATE_DIR/deploy.lock.d" owner
  if ! mkdir "$dir" 2>/dev/null; then
    owner=$(cat "$dir/pid" 2>/dev/null || true)
    if [[ -n $owner ]] && { kill -0 "$owner" 2>/dev/null || ps -p "$owner" >/dev/null 2>&1; }; then
      die "Another deploy is running (PID $owner)."
    fi
    log "Removing a stale deploy lock (PID ${owner:-unknown} is not running)."
    rm -rf "$dir"
    mkdir "$dir" 2>/dev/null || die "Another deploy is running."
  fi
  printf '%s\n' "$$" >"$dir/pid"
  # shellcheck disable=SC2064 # expand $dir now
  trap "rm -rf '$dir'" EXIT
}

main() {
  [[ $# -eq 1 ]] || usage
  require_command docker curl
  require_env_file
  mkdir -p "$FO_STATE_DIR"
  acquire_deploy_lock

  local current tag
  current=$(cat "$FO_STATE_DIR/current-tag" 2>/dev/null || true)

  local skip_pull=${SKIP_PULL:-false}
  if [[ $1 == --rollback ]]; then
    tag=$(cat "$FO_STATE_DIR/previous-tag" 2>/dev/null || true)
    [[ -n $tag ]] || die "No previous tag recorded; nothing to roll back to."
    # The previous release's images are kept on the host (prune_images), so no registry login is needed.
    skip_pull=${SKIP_PULL:-true}
  else
    tag=$1
  fi
  [[ $tag =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$ ]] || die "Invalid image tag '$tag'."

  log "Deploying $tag (running now: ${current:-nothing})."
  export IMAGE_TAG=$tag

  if [[ $skip_pull == true ]]; then
    log "SKIP_PULL=true: using images already on this host."
  else
    log "Pulling images."
    compose pull --quiet
  fi

  log "Starting Postgres."
  compose up -d postgres
  wait_for_postgres 180

  if [[ -n $current && ${SKIP_PRE_DEPLOY_BACKUP:-false} != true ]]; then
    log "Pre-deploy backup (local only)."
    "$(dirname "${BASH_SOURCE[0]}")/backup.sh" --local-only --label "pre-$tag"
  fi

  local migration_args=()
  if [[ $(env_value SEED_SAMPLE_DATA false) == true ]]; then
    migration_args+=(--sample)
  fi
  log "Running migrations ${migration_args[*]:-}."
  # ${a[@]+...}: an empty array is "unbound" under set -u in bash before 4.4 (macOS has 3.2).
  if ! compose run --rm --no-deps migrations ${migration_args[@]+"${migration_args[@]}"}; then
    die "Migrations failed; the running release was not touched. See the output above."
  fi

  log "Starting release $tag."
  if compose up -d --remove-orphans && wait_healthy; then
    if [[ $current != "$tag" ]]; then
      printf '%s\n' "$current" >"$FO_STATE_DIR/previous-tag"
    fi
    printf '%s\n' "$tag" >"$FO_STATE_DIR/current-tag"
    smoke_check
    prune_images "$tag" "$current"
    log "Deployed $tag."
    return 0
  fi

  compose ps >&2 || true
  compose logs --tail 80 >&2 || true
  if [[ -n $current && $current != "$tag" ]]; then
    log "Rolling back to $current."
    export IMAGE_TAG=$current
    if compose up -d --remove-orphans && wait_healthy; then
      log "Rolled back to $current; $tag was NOT deployed."
    else
      log "Rollback to $current is not healthy either; investigate now (logs.sh)."
    fi
  fi
  exit 1
}

main "$@"
