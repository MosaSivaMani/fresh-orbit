# shellcheck shell=bash
# Shared helpers for deploy.sh, backup.sh, restore.sh and logs.sh. Sourced, never run.
#
# Server layout (created by server-setup.sh, filled by the deploy workflow):
#   /opt/freshorbit/infra/{compose,caddy,scripts,monitoring}   copied from the repository on every deploy
#   /opt/freshorbit/infra/compose/.env.prod                    secrets (never in git, never overwritten)
#   /opt/freshorbit/.deploy/                                   current-tag, previous-tag, deploy.lock
#   /var/backups/freshorbit/                                   local backups (BACKUP_DIR)

# The FO_* settings are used by the scripts that source this file.
# shellcheck disable=SC2034
if [[ -z ${FO_LIB_LOADED:-} ]]; then
  FO_LIB_LOADED=1

  FO_INFRA_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
  FO_COMPOSE_DIR="$FO_INFRA_DIR/compose"
  FO_COMPOSE_FILE="$FO_COMPOSE_DIR/docker-compose.prod.yml"
  FO_ENV_FILE="${FO_ENV_FILE:-$FO_COMPOSE_DIR/.env.prod}"
  FO_STATE_DIR="${FO_STATE_DIR:-$(dirname "$FO_INFRA_DIR")/.deploy}"
  # Must match `name:` in docker-compose.prod.yml (volume names are <project>_<volume>).
  FO_PROJECT="freshorbit"
  FO_POSTGRES_IMAGE="postgres:17.10-alpine3.24"
  FO_BUSYBOX_IMAGE="busybox:1.37.0"
  FO_RCLONE_IMAGE="rclone/rclone:1.75.2"
  FO_RCLONE_CONF="${FO_RCLONE_CONF:-$FO_COMPOSE_DIR/rclone.conf}"
  # Containers run the .NET images' non-root user; restored media must belong to it.
  FO_APP_UID=1654
fi

log() {
  printf '%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*" >&2
}

die() {
  log "ERROR: $*"
  exit 1
}

require_command() {
  local name
  for name in "$@"; do
    command -v "$name" >/dev/null 2>&1 || die "'$name' is not installed."
  done
}

require_env_file() {
  [[ -f $FO_ENV_FILE ]] || die "Missing $FO_ENV_FILE; copy .env.prod.example and fill it in."
}

# env_value KEY [DEFAULT]: reads KEY from the env file without sourcing it (values may contain shell characters).
env_value() {
  local key=$1 default=${2-} line
  line=$(grep -E "^${key}=" "$FO_ENV_FILE" | tail -n 1 || true)
  if [[ -n $line ]]; then
    line=${line#*=}
    line=${line%\"}
    line=${line#\"}
    printf '%s' "$line"
  else
    printf '%s' "$default"
  fi
}

# The image tag compose should use: IMAGE_TAG from the environment, else the last deployed tag, else .env.prod.
resolve_image_tag() {
  if [[ -n ${IMAGE_TAG:-} ]]; then
    printf '%s' "$IMAGE_TAG"
  elif [[ -s $FO_STATE_DIR/current-tag ]]; then
    cat "$FO_STATE_DIR/current-tag"
  else
    env_value IMAGE_TAG
  fi
}

compose() {
  IMAGE_TAG="$(resolve_image_tag)" docker compose --env-file "$FO_ENV_FILE" -f "$FO_COMPOSE_FILE" "$@"
}

postgres_user() { env_value POSTGRES_USER freshorbit; }
postgres_db() { env_value POSTGRES_DB freshorbit; }

wait_for_postgres() {
  local deadline=$((SECONDS + ${1:-120}))
  until compose exec -T postgres pg_isready -U "$(postgres_user)" -d "$(postgres_db)" >/dev/null 2>&1; do
    ((SECONDS < deadline)) || die "Postgres did not become ready."
    sleep 2
  done
}
