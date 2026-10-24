#!/usr/bin/env bash
# Brings up the whole Fresh Orbit stack with the sample store, for testing in a GitHub Codespace
# (docs/runbooks/owner-testing.md). Also works in any dev container or Linux machine with Docker, .NET and pnpm.
#
#   bash infra/scripts/codespace-up.sh           start everything (safe to run again: running parts are kept)
#   bash infra/scripts/codespace-up.sh --stop    stop the apps and the containers (data is kept)
#   bash infra/scripts/codespace-up.sh --reset   delete the database and images, then start fresh with a new
#                                                sample staff password
#
# Starts: PostgreSQL and Mailpit (infra/compose/docker-compose.dev.yml), the Migrations host with --sample
# (Development), the Api (:5080), the Worker, the website (:3000) and the admin (:5173). Logs and process ids are
# kept in .codespace-logs/ (gitignored). Development only: never point this at a real database.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LOGS="$ROOT/.codespace-logs"
COMPOSE=(docker compose -f "$ROOT/infra/compose/docker-compose.dev.yml")
SAMPLE_ADMIN_EMAIL="sample.admin@freshorbit.example"
SAMPLE_FINANCE_EMAIL="sample.finance@freshorbit.example"
# Default FileStorage:RootPath in Development (LocalApplicationData/freshorbit/media); the Api serves it at /media.
MEDIA_ROOT="${XDG_DATA_HOME:-$HOME/.local/share}/freshorbit/media"

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 COREPACK_ENABLE_DOWNLOAD_PROMPT=0 NEXT_TELEMETRY_DISABLED=1

say() { printf '\n==> %s\n' "$*"; }
fail() { printf '\nERROR: %s\n' "$*" >&2; exit 1; }

# Public URL of a port: the Codespace forwarded https URL when in a Codespace, else localhost.
url_for() {
  if [[ -n "${CODESPACE_NAME:-}" && -n "${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-}" ]]; then
    printf 'https://%s-%s.%s' "$CODESPACE_NAME" "$1" "$GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN"
  else
    printf 'http://localhost:%s' "$1"
  fi
}

# True when something answers HTTP on the URL (any status code).
answers() {
  local code
  code="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$1" || true)"
  [[ -n "$code" && "$code" != "000" ]]
}

is_running() {
  local pidfile="$LOGS/$1.pid"
  [[ -f "$pidfile" ]] && kill -0 "$(cat "$pidfile")" 2> /dev/null
}

# start_bg NAME COMMAND...: runs the command in its own session, output to .codespace-logs/NAME.log.
start_bg() {
  local name="$1"
  shift
  if is_running "$name"; then
    echo "$name is already running (pid $(cat "$LOGS/$name.pid"))."
    return
  fi
  echo "Starting $name (log: .codespace-logs/$name.log)."
  (cd "$ROOT" && exec setsid "$@" > "$LOGS/$name.log" 2>&1 < /dev/null) &
  echo $! > "$LOGS/$name.pid"
}

# wait_for NAME URL SECONDS: waits until the URL answers; shows the log end and stops if the process died.
wait_for() {
  local name="$1" url="$2" seconds="$3" waited=0
  until answers "$url"; do
    if ! is_running "$name"; then
      tail -n 40 "$LOGS/$name.log" >&2 || true
      fail "$name stopped while starting; see .codespace-logs/$name.log."
    fi
    if ((waited >= seconds)); then
      tail -n 40 "$LOGS/$name.log" >&2 || true
      fail "$name did not answer at $url within $seconds seconds; see .codespace-logs/$name.log."
    fi
    sleep 3
    waited=$((waited + 3))
  done
  echo "$name is up."
}

stop_apps() {
  local name pid
  for name in web admin worker api; do
    if is_running "$name"; then
      pid="$(cat "$LOGS/$name.pid")"
      echo "Stopping $name (pid $pid)."
      kill -TERM -- "-$pid" 2> /dev/null || kill -TERM "$pid" 2> /dev/null || true
    fi
    rm -f "$LOGS/$name.pid"
  done
}

wait_for_docker() {
  local waited=0
  until docker info > /dev/null 2>&1; do
    ((waited >= 90)) && fail "Docker is not running. In a Codespace, wait a minute after it opens and try again."
    sleep 3
    waited=$((waited + 3))
  done
}

psql_value() {
  "${COMPOSE[@]}" exec -T postgres psql -U freshorbit -d freshorbit -tAc "$1" 2> /dev/null || true
}

mkdir -p "$LOGS"

case "${1:-}" in
  --stop)
    stop_apps
    wait_for_docker
    "${COMPOSE[@]}" stop
    say "Stopped. Start again with: bash infra/scripts/codespace-up.sh"
    exit 0
    ;;
  --reset)
    stop_apps
    wait_for_docker
    "${COMPOSE[@]}" down -v
    rm -rf "$MEDIA_ROOT"
    say "Database and images deleted; starting fresh."
    ;;
  "") ;;
  *) fail "Unknown option $1. Use no option, --stop or --reset." ;;
esac

say "1/7 Database and mail catcher"
wait_for_docker
"${COMPOSE[@]}" up -d --wait

say "2/7 Front-end packages"
if [[ ! -d "$ROOT/node_modules" ]]; then
  (cd "$ROOT" && pnpm install --frozen-lockfile)
fi
(cd "$ROOT" && pnpm exec turbo run build --filter='./packages/*' --output-logs=errors-only)

say "3/7 Backend build"
if is_running api || is_running worker; then
  echo "Api or Worker is running; skipping the build (stop with --stop to rebuild)."
else
  dotnet build "$ROOT/backend/FreshOrbit.slnx" -v quiet -nologo > "$LOGS/build.log" 2>&1 ||
    { tail -n 40 "$LOGS/build.log" >&2; fail "Backend build failed; see .codespace-logs/build.log."; }
  echo "Built."
fi

say "4/7 Database migrations and the sample store"
generated_password=""
sample_loaded="$(psql_value "select 1 from identity.users where email = '$SAMPLE_ADMIN_EMAIL'")"
if [[ "$sample_loaded" != "1" && -z "${FRESHORBIT_SAMPLE_STAFF_PASSWORD:-}" ]]; then
  # Same alphabet as the Migrations host's own generator (no look-alike characters); never written to a file.
  generated_password="$(LC_ALL=C tr -dc 'A-HJ-NP-Za-km-z2-9' < /dev/urandom 2> /dev/null | head -c 20 || true)"
  [[ ${#generated_password} -eq 20 ]] || fail "Could not generate the sample staff password."
fi
(cd "$ROOT" && FRESHORBIT_SAMPLE_STAFF_PASSWORD="${FRESHORBIT_SAMPLE_STAFF_PASSWORD:-$generated_password}" \
  DOTNET_ENVIRONMENT=Development \
  dotnet run --no-build --project backend/src/Hosts/FreshOrbit.Migrations -- --sample) > "$LOGS/migrations.log" 2>&1 ||
  { tail -n 40 "$LOGS/migrations.log" >&2; fail "Migrations failed; see .codespace-logs/migrations.log."; }
echo "Database is up to date and the sample store is loaded."

say "5/7 Api and Worker"
if answers "http://localhost:5080/health"; then
  echo "Api is already answering on :5080."
else
  start_bg api dotnet run --no-build --project backend/src/Hosts/FreshOrbit.Api --launch-profile http
  wait_for api "http://localhost:5080/health" 180
fi
start_bg worker env DOTNET_ENVIRONMENT=Development dotnet run --no-build --project backend/src/Hosts/FreshOrbit.Worker
sleep 3
is_running worker || { tail -n 40 "$LOGS/worker.log" >&2; fail "Worker stopped; see .codespace-logs/worker.log."; }

say "6/7 Website and admin"
WEB_URL="$(url_for 3000)"
if answers "http://localhost:3000"; then
  echo "Website is already answering on :3000."
else
  start_bg web env FRESH_API_ORIGIN=http://localhost:5080 NEXT_PUBLIC_SITE_URL="$WEB_URL" \
    pnpm --filter @fresh/web dev
fi
if answers "http://localhost:5173"; then
  echo "Admin is already answering on :5173."
else
  start_bg admin env FRESH_API_PROXY=http://localhost:5080 pnpm --filter @fresh/admin dev
fi
is_running web && wait_for web "http://localhost:3000" 240
is_running admin && wait_for admin "http://localhost:5173" 120

say "7/7 Ready"
API_URL="$(url_for 5080)"
cat << EOF

  Website (customer)   $WEB_URL
  Admin (staff)        $(url_for 5173)
  API reference        $API_URL/docs
  Sign-in codes (OTP)  $API_URL/api/v1/dev/sms?phone=+919999900301   (put your 10 digits after +91)
  E-mails (Mailpit)    $(url_for 8025)

  Delivery pincode     530017 (also 530016, 530048, 530026)
  Admin sign-in        $SAMPLE_ADMIN_EMAIL   (finance: $SAMPLE_FINANCE_EMAIL)
EOF
if [[ -n "$generated_password" ]]; then
  cat << EOF

  Staff password       $generated_password

  Write the staff password down now: it is shown only this once and is not saved anywhere.
  (Lost it? Run: bash infra/scripts/codespace-up.sh --reset   This deletes the test data and makes a new one.)
EOF
elif [[ -n "${FRESHORBIT_SAMPLE_STAFF_PASSWORD:-}" && "$sample_loaded" != "1" ]]; then
  echo "  Staff password       the value of FRESHORBIT_SAMPLE_STAFF_PASSWORD"
else
  echo "  Staff password       shown when the sample store was first loaded (lost it? run with --reset)"
fi
cat << EOF

  Open the links from the PORTS tab (or click them above). Logs: .codespace-logs/
  Stop:  bash infra/scripts/codespace-up.sh --stop     (or just stop the Codespace)

EOF
