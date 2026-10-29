#!/usr/bin/env bash
# Local end-to-end check of a production deploy (docs/runbooks/e2e-check.md).
#
#   infra/scripts/e2e-check.sh              both passes (below), each: migrate (twice) → up → health → Caddy → smoke → down -v
#   infra/scripts/e2e-check.sh <step>...    only some steps of one pass: env build migrate up caddy smoke down
#   E2E_MODE=testing infra/scripts/e2e-check.sh <step>...   the same steps in the testing pass
#   KEEP=true infra/scripts/e2e-check.sh    leave the stack running at the end (tear down later with `down`)
#
# Two passes, each on a fresh database (E2E_MODE=production|testing|both, default both):
#   production  APP_ENVIRONMENT=Production with the DLT SMS settings, exactly as on a server: proves the start-up
#               guards accept a complete configuration, the Production guards hold (no /dev/sms; the OTP send through
#               the real DLT adapter to an unreachable .invalid gateway answers 503 SMS_PROVIDER_UNAVAILABLE, never
#               500), and runs smoke.sh up to the OTP sign-in (SMOKE_STOP_BEFORE_OTP=true).
#   testing     APP_ENVIRONMENT=Testing plus docker-compose.e2e-testing.yml (DEVELOPMENT SMS sender, /dev/sms): the
#               full smoke.sh (sign-in, order, fulfilment, invoice, payment, stock, notifications).
#
# Runs docker-compose.prod.yml + docker-compose.e2e.yml under its OWN project name (default fo-e2e), so it never
# touches a `freshorbit` stack (dev or prod) or its volumes, and `down` removes only this project's containers and
# volumes. Caddy listens on 127.0.0.1:${E2E_HTTPS_PORT:-18443} (HTTPS, Caddy's local CA) and :${E2E_HTTP_PORT:-18080}.
#
# Environment: E2E_PROJECT (fo-e2e), E2E_ENV_FILE (infra/compose/.env.e2e, generated from .env.e2e.example on first
# use), E2E_WORK_DIR (logs, CA certificate, curl config; default $TMPDIR/fo-e2e), HEALTH_TIMEOUT (seconds, 300),
# SKIP_BUILD=true (use images already built). Needs docker (compose v2.24+ for `!override`), curl, jq, openssl.
# Exit code 0 only when every check passes.
set -euo pipefail

INFRA_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_DIR="$INFRA_DIR/compose"
PROJECT="${E2E_PROJECT:-fo-e2e}"
ENV_FILE="${E2E_ENV_FILE:-$COMPOSE_DIR/.env.e2e}"
WORK_DIR="${E2E_WORK_DIR:-${TMPDIR:-/tmp}/fo-e2e}"
WORK_DIR="${WORK_DIR%/}"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-300}"
# The first EF migration and where EF records applied migrations (the shared DbContext keeps its history in the
# platform schema, snake_case).
MIGRATION_ID="20261023185953_InitialCreate"
HISTORY_TABLE="platform.__ef_migrations_history"

log() { printf '%s %s\n' "$(date -u +%H:%M:%SZ)" "$*" >&2; }
die() { log "ERROR: $*"; exit 1; }
ok() { printf 'OK    %s\n' "$*"; }

[[ $PROJECT != freshorbit ]] || die "E2E_PROJECT must not be 'freshorbit' (that is the real stack's project name)."

MODE="${E2E_MODE:-both}"

# MODE is production or testing here (main runs each pass of "both" separately). The shell's APP_ENVIRONMENT wins
# over the env file in compose.
compose() {
  local files=(-f "$COMPOSE_DIR/docker-compose.prod.yml" -f "$COMPOSE_DIR/docker-compose.e2e.yml")
  if [[ $MODE == testing ]]; then
    files+=(-f "$COMPOSE_DIR/docker-compose.e2e-testing.yml")
    APP_ENVIRONMENT=Testing docker compose -p "$PROJECT" --env-file "$ENV_FILE" "${files[@]}" "$@"
  else
    APP_ENVIRONMENT=Production docker compose -p "$PROJECT" --env-file "$ENV_FILE" "${files[@]}" "$@"
  fi
}

env_value() { grep -E "^$1=" "$ENV_FILE" | tail -n 1 | cut -d= -f2-; }

psql_at() {
  compose exec -T postgres psql -v ON_ERROR_STOP=1 -U "$(env_value POSTGRES_USER)" -d "$(env_value POSTGRES_DB)" -At -c "$1"
}

# ---- env: generate the throwaway env file -------------------------------------------------------------------------
step_env() {
  if [[ -f $ENV_FILE ]]; then
    log "Using existing $ENV_FILE."
    # E2E-1 added the DLT gateway and template ids; an env file generated before that cannot pass compose config.
    grep -qE '^SMS_DLT_GATEWAY=' "$ENV_FILE" \
      || die "$ENV_FILE predates the DLT SMS settings; delete it so it is generated again from .env.e2e.example."
  else
    log "Generating $ENV_FILE from .env.e2e.example (random secrets)."
    local line key
    umask 077
    while IFS= read -r line || [[ -n $line ]]; do
      if [[ $line == *=__GENERATED__ ]]; then
        key=${line%%=*}
        if [[ $key == *PASSWORD ]]; then
          printf '%s=E2e-%s\n' "$key" "$(openssl rand -hex 16)"
        else
          printf '%s=%s\n' "$key" "$(openssl rand -hex 32)"
        fi
      else
        printf '%s\n' "$line"
      fi
    done <"$COMPOSE_DIR/.env.e2e.example" >"$ENV_FILE"
  fi
  ! grep -qE '^[A-Z0-9_]+=__GENERATED__' "$ENV_FILE" || die "$ENV_FILE still has __GENERATED__ placeholders."
  [[ $(env_value DOMAIN) == localhost ]] || log "WARNING: DOMAIN is not localhost; Caddy may try Let's Encrypt."
  compose config --quiet || die "compose config is invalid."
  mkdir -p "$WORK_DIR"
  ok "env file and compose configuration valid (project $PROJECT)"
}

# ---- build: all five images from this checkout ---------------------------------------------------------------------
step_build() {
  if [[ ${SKIP_BUILD:-false} == true ]]; then
    log "SKIP_BUILD=true: using existing images."
    return
  fi
  log "Building api, worker, migrations, web and admin images."
  compose build migrations api worker web admin
  ok "images built: $(env_value IMAGE_REGISTRY)/fresh-orbit-{migrations,api,worker,web,admin}:$(env_value IMAGE_TAG)"
}

# Counts that prove the reference loaders ran; printed and returned as one line for the idempotency comparison.
reference_counts() {
  psql_at "
    SELECT 'migrations=' || (SELECT count(*) FROM $HISTORY_TABLE)
      || ' permissions=' || (SELECT count(*) FROM identity.permissions)
      || ' roles=' || (SELECT count(*) FROM identity.roles)
      || ' role_permissions=' || (SELECT count(*) FROM identity.role_permissions)
      || ' preparation_methods=' || (SELECT count(*) FROM catalog.preparation_methods)
      || ' storage_units=' || (SELECT count(*) FROM inventory.storage_units)
      || ' tax_categories=' || (SELECT count(*) FROM pricing.tax_categories)
      || ' notification_templates=' || (SELECT count(*) FROM notifications.templates)
      || ' number_series=' || (SELECT count(*) FROM settings.number_series)
      || ' order_policies=' || (SELECT count(*) FROM settings.order_policies)
      || ' staff=' || (SELECT count(*) FROM identity.staff_members);"
}

# The per-file lines the reference loaders log ("Reference data \"x.json\": 25 inserted, ...") in Serilog compact JSON.
print_loaded() {
  grep -oE '"@m":"Reference data \\"[^"\\]+\\": [^"]+"' "$1" \
    | sed -E 's/^"@m":"Reference data \\"([^"\\]+)\\": ([^"]+)"$/      \1: \2/' || true
}

# E2E-5: no_error_lines LOGFILE NAME fails when the log has an Error or Fatal entry (Serilog compact JSON).
no_error_lines() {
  if grep -qE '"@l":"(Error|Fatal)"' "$1"; then
    grep -E '"@l":"(Error|Fatal)"' "$1" | head -n 5 >&2
    die "$2 logged an Error (log: $1)."
  fi
  ok "$2 logged no Error"
}

run_migrations() { # run_migrations LOGFILE [args...] → exit code of the one-shot container
  local logfile=$1 code=0
  shift
  compose run --rm --no-deps -T migrations "$@" >"$logfile" 2>&1 || code=$?
  return "$code"
}

# ---- migrate: fresh database → migrations + reference data + first admin, then a second (idempotent) run -----------
step_migrate() {
  log "Starting Postgres (fresh volume unless one exists from a KEEP=true run)."
  compose up -d --wait postgres
  local tables_before
  tables_before=$(psql_at "SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema');")
  log "Tables before the first run: $tables_before."

  local code=0
  log "Migrations run 1 (with --create-admin)."
  run_migrations "$WORK_DIR/migrations-run1.log" --create-admin \
    --email "$(env_value E2E_ADMIN_EMAIL)" --name "$(env_value E2E_ADMIN_NAME)" --password "$(env_value E2E_ADMIN_PASSWORD)" || code=$?
  if ((code == 3)) && ((tables_before > 0)); then
    log "Run 1 refused the first admin (exit 3): the database already had one (KEEP=true run). Re-running plain."
    code=0
    run_migrations "$WORK_DIR/migrations-run1.log" || code=$?
  fi
  ((code == 0)) || { tail -n 60 "$WORK_DIR/migrations-run1.log" >&2; die "Migrations run 1 exited $code (log: $WORK_DIR/migrations-run1.log)."; }
  ok "migrations run 1 exited 0"
  # E2E-5: a fresh database migrates without a single Error line (compact JSON writes "@l" only when not Information).
  no_error_lines "$WORK_DIR/migrations-run1.log" "migrations run 1"

  local history
  history=$(psql_at "SELECT migration_id || ' (EF ' || product_version || ')' FROM $HISTORY_TABLE ORDER BY 1;")
  printf '      %s: %s\n' "$HISTORY_TABLE" "$history"
  grep -q "$MIGRATION_ID" <<<"$history" || die "$MIGRATION_ID is not in $HISTORY_TABLE."
  ok "$MIGRATION_ID recorded in $HISTORY_TABLE"

  local schemas tables
  schemas=$(psql_at "SELECT string_agg(nspname, ',' ORDER BY nspname) FROM pg_namespace WHERE nspname NOT LIKE 'pg\\_%' AND nspname NOT IN ('information_schema','public');")
  tables=$(psql_at "SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema');")
  printf '      schemas: %s\n      tables and views: %s\n' "$schemas" "$tables"

  local loaders
  # Serilog compact JSON: one "Loader" property per reference loader, one "File" line per file it loaded.
  loaders=$(grep -oE '"Loader":"[A-Za-z]+"' "$WORK_DIR/migrations-run1.log" | cut -d'"' -f4 | paste -sd, - || true)
  printf '      reference loaders run: %s\n' "${loaders:-none found in log}"
  [[ -n $loaders ]] || die "No reference loader lines in the migrations log."
  print_loaded "$WORK_DIR/migrations-run1.log"
  grep -q '"Step":"Done"' "$WORK_DIR/migrations-run1.log" || die "Migrations run 1 did not log Done."
  grep -q '"Name":"LogAdminCreated"' "$WORK_DIR/migrations-run1.log" || die "Migrations run 1 did not create the first admin."
  ok "reference loaders ran and the first admin was created"

  local counts1
  counts1=$(reference_counts)
  printf '      after run 1: %s\n' "$counts1"
  local key value
  for key in permissions roles preparation_methods storage_units tax_categories notification_templates number_series order_policies; do
    value=$(grep -oE "(^| )$key=[0-9]+" <<<"$counts1" | cut -d= -f2)
    [[ ${value:-0} -gt 0 ]] || die "Reference table for $key is empty after run 1."
  done
  ok "reference tables non-empty"

  log "Migrations run 2 (no options): must be a no-op."
  code=0
  run_migrations "$WORK_DIR/migrations-run2.log" || code=$?
  ((code == 0)) || { tail -n 60 "$WORK_DIR/migrations-run2.log" >&2; die "Migrations run 2 exited $code."; }
  no_error_lines "$WORK_DIR/migrations-run2.log" "migrations run 2"
  print_loaded "$WORK_DIR/migrations-run2.log"
  local counts2
  counts2=$(reference_counts)
  printf '      after run 2: %s\n' "$counts2"
  [[ $counts1 == "$counts2" ]] || die "Run 2 changed row counts (not idempotent)."
  ok "migrations run 2 exited 0 with identical counts (idempotent)"

  log "A second --create-admin must be refused (exit 3)."
  code=0
  run_migrations "$WORK_DIR/migrations-run3.log" --create-admin \
    --email "second-$(env_value E2E_ADMIN_EMAIL)" --name "Second Admin" --password "$(env_value E2E_ADMIN_PASSWORD)" || code=$?
  ((code == 3)) || die "A second --create-admin exited $code, expected 3."
  ok "second --create-admin refused (exit 3)"
}

# check_services → 0 ready, 1 starting, 2 failed; prints one status word per service.
check_services() {
  local svc id state health restarts exit_code line=() failed=0 starting=0
  for svc in postgres migrations api worker web admin caddy; do
    id=$(compose ps -a -q "$svc" 2>/dev/null | head -n 1)
    if [[ -z $id ]]; then
      line+=("$svc=missing"); starting=1; continue
    fi
    read -r state health restarts exit_code < <(docker inspect -f \
      '{{.State.Status}} {{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}} {{.RestartCount}} {{.State.ExitCode}}' "$id")
    if [[ $svc == migrations ]]; then
      line+=("$svc=$state/exit$exit_code")
      if [[ $state == exited && $exit_code != 0 ]]; then failed=1; elif [[ $state != exited ]]; then starting=1; fi
      continue
    fi
    line+=("$svc=$state/$health/r$restarts")
    if [[ $state == exited || $state == dead || $state == restarting || $health == unhealthy || $restarts -gt 0 ]]; then
      failed=1
    elif [[ $state != running || ($health != healthy && $health != none) ]]; then
      starting=1
    fi
  done
  printf '%s\n' "${line[*]}"
  ((failed)) && return 2
  ((starting)) && return 1
  return 0
}

# ---- up: the whole stack, then wait until every service is healthy (worker: running, no restarts) ----------------
step_up() {
  log "Starting the stack."
  compose up -d
  local deadline=$((SECONDS + HEALTH_TIMEOUT)) status detail
  while :; do
    status=0
    detail=$(check_services) || status=$?
    if ((status == 0)); then
      log "All ready; checking the worker stays up for 20 s."
      sleep 20
      status=0
      detail=$(check_services) || status=$?
      ((status == 0)) && break
    fi
    if ((status == 2)); then
      compose ps -a >&2 || true
      compose logs --tail 80 >&2 || true
      die "A service failed: $detail"
    fi
    ((SECONDS < deadline)) || { compose ps -a >&2; die "Timed out after ${HEALTH_TIMEOUT}s: $detail"; }
    sleep 5
  done
  printf '      %s\n' "$detail"
  ok "postgres, api, web, admin, caddy healthy; worker running without restarts; migrations exited 0"
  check_data_protection_keys
}

# E2E-3: the api keeps its Data Protection key ring on the dataprotection-keys volume (DataProtection__KeysPath=/keys),
# so a new api container (every deploy) reads the same keys instead of making new ones and logs no "keys may not be
# persisted" warning.
data_protection_keys() { compose exec -T api sh -c 'ls -1 /keys 2>/dev/null | grep -E "^key-.+\.xml$" | sort' || true; }

check_data_protection_keys() {
  local before after i health=starting
  before=$(data_protection_keys)
  [[ -n $before ]] || die "api: no Data Protection key in /keys."
  if compose logs api 2>&1 | grep -q 'may not be persisted outside of the container'; then
    die "api: Data Protection still stores keys inside the container."
  fi
  log "Recreating the api container (as a deploy does); the key ring must survive."
  compose up -d --no-deps --force-recreate api >/dev/null 2>&1
  for ((i = 0; i < 60; i++)); do
    health=$(docker inspect -f '{{.State.Health.Status}}' "$(compose ps -q api)" 2>/dev/null || echo starting)
    [[ $health == healthy ]] && break
    sleep 2
  done
  [[ $health == healthy ]] || die "api is $health after being recreated."
  after=$(data_protection_keys)
  [[ $before == "$after" ]] || die "api: the Data Protection keys changed after a restart ($before → $after)."
  printf '      keys: %s\n' "$(printf '%s' "$after" | paste -sd, -)"
  ok "api: Data Protection keys persisted on the dataprotection-keys volume and kept across a new container"
}

# curl through Caddy exactly as a visitor would (https://<host>/..., standard ports), trusting Caddy's local CA. The
# curl config (CURL_HOME/.curlrc, also read by every curl call in smoke.sh) sends each site's :443 and :80 to the
# high ports Caddy publishes on 127.0.0.1, so URLs, Host headers and the absolute URLs the API returns
# (FileStorage__PublicBaseUrl) all keep their production form.
write_curl_config() {
  local https http host
  https=$(env_value E2E_HTTPS_PORT)
  http=$(env_value E2E_HTTP_PORT)
  mkdir -p "$WORK_DIR"
  compose cp caddy:/data/caddy/pki/authorities/local/root.crt "$WORK_DIR/caddy-root.crt" >/dev/null 2>&1 \
    || die "Could not copy Caddy's local CA certificate (is DOMAIN a localhost name and caddy running?)."
  {
    printf 'cacert = "%s"\n' "$WORK_DIR/caddy-root.crt"
    for host in "$(env_value DOMAIN)" "admin.$(env_value DOMAIN)" "api.$(env_value DOMAIN)" "www.$(env_value DOMAIN)"; do
      printf 'connect-to = "%s:443:127.0.0.1:%s"\n' "$host" "${https:-18443}"
      printf 'connect-to = "%s:80:127.0.0.1:%s"\n' "$host" "${http:-18080}"
    done
  } >"$WORK_DIR/.curlrc"
}

ecurl() { CURL_HOME="$WORK_DIR" curl "$@"; }

expect_status() { # expect_status NAME EXPECTED URL [curl args...]
  local name=$1 expected=$2 url=$3 got
  shift 3
  got=$(ecurl -s -o "$WORK_DIR/last-body" -D "$WORK_DIR/last-headers" -w '%{http_code}' -m 20 "$@" "$url" || true)
  [[ $got == "$expected" ]] || die "$name: HTTP $got from $url, expected $expected. $(head -c 300 "$WORK_DIR/last-body" 2>/dev/null)"
  ok "$name: $expected from $url"
}

expect_header() { # expect_header NAME REGEX (on the last response)
  grep -qiE "$2" "$WORK_DIR/last-headers" || die "$1: no header matching '$2'."
  ok "$1"
}

# ---- caddy: every site answers through the edge, with TLS from Caddy's local CA -----------------------------------
step_caddy() {
  local domain
  domain=$(env_value DOMAIN)
  write_curl_config
  printf '      Caddy CA: %s\n' "$(openssl x509 -in "$WORK_DIR/caddy-root.crt" -noout -subject 2>/dev/null)"

  expect_status "api health (api.$domain)" 200 "https://api.$domain/health"
  expect_header "api: HSTS from the Caddyfile" '^strict-transport-security: max-age=31536000'
  expect_status "api: unauthenticated admin call refused" 401 "https://api.$domain/api/v1/admin/auth/me"
  expect_status "api: private media path hidden" 404 "https://api.$domain/media/originals/x.bin"

  expect_status "web home ($domain)" 200 "https://$domain/"
  grep -qi '<html' "$WORK_DIR/last-body" || die "web home is not HTML."
  expect_header "web: CSP from the Caddyfile" "^content-security-policy: default-src 'self'"
  expect_status "web: /api/v1 proxy reaches the API" 200 "https://$domain/api/v1/serviceability?pincode=600001"

  expect_status "admin SPA (admin.$domain)" 200 "https://admin.$domain/"
  grep -qi '<div id="root"\|<script' "$WORK_DIR/last-body" || die "admin index is not the SPA."
  expect_header "admin: strict CSP (D40)" "^content-security-policy: default-src 'none'; script-src 'self'"
  expect_header "admin: noindex" '^x-robots-tag: noindex'
  expect_status "admin: SPA route falls back to index.html" 200 "https://admin.$domain/orders/does-not-exist"
  expect_status "admin: same-origin /api goes to the API" 401 "https://admin.$domain/api/v1/admin/auth/me"

  expect_status "www redirects to the apex" 301 "https://www.$domain/"
  expect_status "plain HTTP redirects to HTTPS" 308 "http://$domain/"

  if [[ $MODE == testing ]]; then
    expect_status "testing: development SMS outbox exists" 200 "https://api.$domain/api/v1/dev/sms?phone=%2B919800000000"
  else
    step_guards
  fi
}

# Production pass only: the guards of docs/runbooks/security-configuration.md §2 hold in the running stack.
step_guards() {
  local domain got name state code i
  domain=$(env_value DOMAIN)
  expect_status "production: no development SMS outbox" 404 "https://api.$domain/api/v1/dev/sms?phone=%2B919800000000"
  got=$(ecurl -s -o "$WORK_DIR/last-body" -w '%{http_code}' -m 20 -X POST "https://api.$domain/api/v1/auth/otp/request" \
    -H 'Content-Type: application/json' -H "Idempotency-Key: e2e-$(openssl rand -hex 8)" \
    --data '{"phone":"+919800000001","purpose":"LOGIN"}' || true)
  # E2E-1: the gateway (SMS_DLT_BASE_URL) is a .invalid host, so the send fails and must be a coded 503, not a 500.
  [[ $got == 503 ]] || die "production: OTP request with an unreachable DLT gateway answered $got; expected 503."
  [[ $(jq -r '.code' "$WORK_DIR/last-body" 2>/dev/null) == SMS_PROVIDER_UNAVAILABLE ]] \
    || die "production: OTP request answered 503 without code SMS_PROVIDER_UNAVAILABLE; see $WORK_DIR/last-body."
  ok "production: OTP send to an unreachable DLT gateway refused with 503 SMS_PROVIDER_UNAVAILABLE"

  # The api must refuse to START in Production with the DEVELOPMENT SMS sender (a throwaway extra container).
  name="$PROJECT-guard-$(openssl rand -hex 4)"
  compose run -d --no-deps --name "$name" -e Notifications__Sms__Provider=DEVELOPMENT api >/dev/null
  state=running
  for ((i = 0; i < 60; i++)); do
    state=$(docker inspect -f '{{.State.Status}}' "$name" 2>/dev/null || echo gone)
    [[ $state == running || $state == created ]] || break
    sleep 1
  done
  code=$(docker inspect -f '{{.State.ExitCode}}' "$name" 2>/dev/null || echo "?")
  docker logs "$name" >"$WORK_DIR/guard-api.log" 2>&1 || true
  docker rm -f "$name" >/dev/null 2>&1 || true
  [[ $state == exited && $code != 0 ]] || die "production: api with Sms Provider=DEVELOPMENT is $state (exit $code); it must refuse to start."
  grep -q 'Notifications:Sms:Provider must be DLT' "$WORK_DIR/guard-api.log" \
    || die "production: the api stopped (exit $code) but not on the SMS provider check; see $WORK_DIR/guard-api.log."
  ok "production: api refuses to start with Notifications__Sms__Provider=DEVELOPMENT (exit $code)"
}

# ---- smoke: infra/scripts/smoke.sh through Caddy (api.<domain>) ----------------------------------------------------
# testing: the full script (OTP codes from GET /api/v1/dev/sms). production: up to the OTP sign-in.
step_smoke() {
  local domain code=0 stop=false
  domain=$(env_value DOMAIN)
  [[ $MODE == testing ]] || stop=true
  [[ -f $WORK_DIR/.curlrc ]] || write_curl_config
  # The fo-e2e stack is thrown away after the run, so smoke.sh may create its city there (L-47, D319).
  CURL_HOME="$WORK_DIR" API_URL="https://api.$domain" SMOKE_STOP_BEFORE_OTP=$stop SMOKE_ALLOW_WRITES=true \
    ADMIN_EMAIL="$(env_value E2E_ADMIN_EMAIL)" ADMIN_PASSWORD="$(env_value E2E_ADMIN_PASSWORD)" \
    "$INFRA_DIR/scripts/smoke.sh" | tee "$WORK_DIR/smoke-$MODE.log" || code=$?
  ((code == 0)) || die "smoke.sh failed (exit $code; $WORK_DIR/smoke-$MODE.log; compose logs api worker)."
  ok "smoke ($MODE): $(tail -n 1 "$WORK_DIR/smoke-$MODE.log")"
}

# ---- down: remove this project's containers, network and volumes (images are kept for the next run) ---------------
step_down() {
  log "Removing project $PROJECT (containers, network, volumes)."
  compose down -v --remove-orphans
  ok "project $PROJECT removed"
}

run_steps() {
  local s
  if [[ ${KEEP:-false} != true && " $* " == *" down "* ]]; then
    trap 'code=$?; if ((code != 0)); then log "Failed; tearing down (KEEP=true keeps the stack for debugging)."; step_down || true; fi' EXIT
  fi
  for s in "$@"; do
    if [[ $s == down && ${KEEP:-false} == true ]]; then
      log "KEEP=true: leaving $PROJECT ($MODE) running. Tear down: E2E_MODE=$MODE $0 down"
      continue
    fi
    "step_$s"
  done
  trap - EXIT
  log "E2E check passed ($MODE): $*."
}

main() {
  local s
  # (No arrays here: macOS bash 3.2 treats an empty array as unbound under `set -u`.)
  for s in "$@"; do
    case $s in env | build | migrate | up | caddy | smoke | down) ;; *) die "Unknown step '$s'." ;; esac
  done
  case $MODE in production | testing | both) ;; *) die "E2E_MODE must be production, testing or both." ;; esac
  if (($# == 0)); then
    step_env
    step_build
    if [[ $MODE == both ]]; then
      MODE=production run_steps migrate up caddy smoke down
      MODE=testing run_steps migrate up caddy smoke down
    else
      run_steps migrate up caddy smoke down
    fi
    return
  fi
  [[ $MODE != both ]] || MODE=production
  [[ $1 == env ]] || step_env >/dev/null
  run_steps "$@"
}

main "$@"
