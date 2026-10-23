#!/usr/bin/env bash
# Shows Fresh Orbit container logs and status.
#
#   infra/scripts/logs.sh                      follow all services (last 100 lines each)
#   infra/scripts/logs.sh api worker           follow selected services
#   infra/scripts/logs.sh -n 500 --since 2h api --no-follow
#   infra/scripts/logs.sh --status             containers, health, restarts, image tag, disk and memory use
#   infra/scripts/logs.sh --errors [--since 1h] api/worker lines at Error or Fatal level (Serilog JSON @l field)
#
# Services: caddy api worker migrations web admin postgres (and loki alloy grafana with the monitoring profile).
set -euo pipefail

# shellcheck source=infra/scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

usage() {
  sed -n '2,11p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
  exit 2
}

status() {
  log "Release: $(resolve_image_tag)"
  compose ps --all --format 'table {{.Service}}\t{{.Image}}\t{{.State}}\t{{.Status}}'
  printf '\n'
  local ids
  mapfile -t ids < <(compose ps -q)
  if ((${#ids[@]})); then
    docker stats --no-stream --format 'table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.MemPerc}}' "${ids[@]}"
  fi
  printf '\n'
  df -h / "$(docker info --format '{{.DockerRootDir}}')" 2>/dev/null | uniq
  printf '\n'
  free -h 2>/dev/null || true
}

main() {
  require_command docker
  require_env_file

  local tail=100 since="" follow=true errors=false services=()
  while (($#)); do
    case $1 in
      --status)
        status
        return 0
        ;;
      --errors) errors=true ;;
      -n | --tail)
        [[ $# -ge 2 ]] || usage
        tail=$2
        shift
        ;;
      --since)
        [[ $# -ge 2 ]] || usage
        since=$2
        shift
        ;;
      --no-follow) follow=false ;;
      -h | --help) usage ;;
      -*) usage ;;
      *) services+=("$1") ;;
    esac
    shift
  done

  local args=(logs --tail "$tail")
  [[ -n $since ]] && args+=(--since "$since")

  if [[ $errors == true ]]; then
    ((${#services[@]})) || services=(api worker)
    compose "${args[@]}" --no-log-prefix "${services[@]}" | grep -E '"@l":"(Error|Fatal)"' || true
    return 0
  fi

  [[ $follow == true ]] && args+=(--follow)
  compose "${args[@]}" "${services[@]}"
}

main "$@"
