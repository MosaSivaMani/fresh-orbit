# Runbook: local end-to-end check of a deploy

`infra/scripts/e2e-check.sh` builds the five images from your checkout and runs the **production** compose stack
(`docker-compose.prod.yml`) on your machine. It then checks that a deploy works from a fresh database to a delivered
order. Run it before a release, and after any change to `infra/`, the Dockerfiles, the Migrations host or the
start-up configuration. The 2026-10-24 findings are in `docs/research/e2e-deploy-check-2026-10.md`.

## 1. Run it

```bash
infra/scripts/e2e-check.sh                       # about 5 minutes with cached images; exit 0 = everything passed
SKIP_BUILD=true infra/scripts/e2e-check.sh       # reuse the images you already built
KEEP=true E2E_MODE=testing infra/scripts/e2e-check.sh   # one pass, and leave the stack running
E2E_MODE=testing infra/scripts/e2e-check.sh down # tear down a KEEP=true stack
infra/scripts/e2e-check.sh migrate up caddy      # only some steps (env build migrate up caddy smoke down)
```

You need Docker with Compose v2.24 or later (for `!override`), `bash` (macOS bash 3.2 works), `curl`, `jq` and
`openssl`. No other tools or images: it uses only the project's own Dockerfiles and the images in the compose files.

## 2. What it isolates

| Concern | How |
|---|---|
| Other stacks on the same Docker | Compose project `fo-e2e` (`E2E_PROJECT`), never `freshorbit`. `down -v` removes only this project's containers, network and volumes. Images are kept |
| Ports 80/443 already taken | Caddy publishes only on `127.0.0.1:18080` and `127.0.0.1:18443` (`E2E_HTTP_PORT`, `E2E_HTTPS_PORT`) |
| No public DNS or Let's Encrypt | `DOMAIN=localhost`, so Caddy issues `localhost`, `admin.localhost`, `api.localhost` and `www.localhost` from its own local CA. The script copies the CA root out of the `caddy-data` volume |
| Production-shaped URLs | A curl config (`$TMPDIR/fo-e2e/.curlrc`, passed to curl through `CURL_HOME`) trusts that CA and uses `connect-to` to send `<host>:443`/`:80` to the high ports. URLs, Host headers and the absolute media URLs the API returns stay exactly as they are on a server |
| Secrets | `infra/compose/.env.e2e` is generated from the tracked `.env.e2e.example`, with random values for every `__GENERATED__` key, and is git-ignored. The SMS settings point at `https://dlt.example.invalid` |
| Images | Tagged `fo-e2e/fresh-orbit-*:local`, never with the GHCR names, so nothing can be pushed by mistake |

`docker-compose.e2e.yml` changes only the build sources and Caddy's ports. Everything else comes from the production
file: the Caddyfile, health checks, memory limits, the environment and the subnet (here `10.231.17.0/24`).

## 3. The two passes

Each pass starts from an empty database volume.

| Pass | Hosts run as | Proves |
|---|---|---|
| `production` | `APP_ENVIRONMENT=Production`, `Notifications__Sms__Provider=DLT` with throwaway DLT settings, `FileStorage__RootPath=/media` | The start-up validators accept a complete Production configuration. The guards hold: `GET /api/v1/dev/sms` returns 404, an OTP request through the real DLT adapter to the unreachable `SMS_DLT_BASE_URL` (`https://dlt.example.invalid`) answers 503 `SMS_PROVIDER_UNAVAILABLE` (E2E-1; never 500), and an api container started with `Provider=DEVELOPMENT` refuses to start with exit 1 (`Start-up refused … Notifications:Sms:Provider must be DLT …`). An `.env.e2e` generated before E2E-1 lacks the DLT gateway and template ids; the script stops and asks you to delete it. `smoke.sh` runs with `SMOKE_STOP_BEFORE_OTP=true`: staff setup, city activation, slot generation, serviceability, product list and media |
| `testing` | `APP_ENVIRONMENT=Testing` plus `docker-compose.e2e-testing.yml` (`Provider=DEVELOPMENT`) | The full `smoke.sh`: OTP codes come from `GET /api/v1/dev/sms`, then a COD order is placed, packed, dispatched and delivered, and the invoice, payment, stock and notifications are checked |

Smoke needs the testing pass because OTP codes can only be read in Development or Testing
(`security-configuration.md` §2). No Production guard is weakened: the testing override exists only in this check.

## 4. Steps and their evidence

| Step | Checks |
|---|---|
| `env` | Env file present, no placeholders left, `docker compose config` valid |
| `build` | `docker compose build` of migrations, api, worker, web and admin |
| `migrate` | Postgres has 0 tables. **Run 1** (`--create-admin`) exits 0. `platform.__ef_migrations_history` holds `20261023185953_InitialCreate`. All 16 module schemas exist. The six reference loaders logged their files, and the reference tables are non-empty. The first admin is created. **Run 2** (no options) exits 0 and every count is identical: loaders report `0 inserted … N unchanged`. A **second `--create-admin`** is refused with exit 3 |
| `up` | `compose up -d`. postgres, api, web, admin and caddy are `healthy`, the worker is `running` with 0 restarts after a 20-second soak, and the migrations container (its third run) exited 0 |
| `caddy` | Through Caddy over TLS: api `/health` 200 with HSTS; `/api/v1/admin/auth/me` 401; private `/media/...` 404. Web `/` 200 with its CSP; the web `/api/v1` proxy reaches the API. Admin `/` 200 with the strict CSP (D40) and `noindex`; SPA fallback; admin-origin `/api` reaches the API. `www` returns 301; HTTP returns 308 to HTTPS. Then the guards for the current pass |
| `smoke` | `infra/scripts/smoke.sh` against `https://api.localhost` |
| `down` | `compose down -v --remove-orphans`. It also runs automatically when a step fails, unless `KEEP=true` |

Logs go to `$TMPDIR/fo-e2e/`: `migrations-run{1,2,3}.log`, `smoke-<pass>.log` and `guard-api.log`.

## 5. Running `deploy.sh` against the e2e stack

`lib.sh` reads `FO_PROJECT` (default `freshorbit`) and `FO_COMPOSE_OVERRIDE`. With those set, `deploy.sh` can drive
the same stack: `FO_PROJECT=fo-e2e FO_ENV_FILE=infra/compose/.env.e2e FO_COMPOSE_OVERRIDE=infra/compose/docker-compose.e2e.yml
SKIP_PULL=true SKIP_IMAGE_PRUNE=true infra/scripts/deploy.sh local`. This works only on Linux: `deploy.sh` needs
`flock` and bash 4 or later (`mapfile`), and macOS has neither. Running it in a Linux container on the Mac would
need an image the project does not use yet, so it was not done there.

## 6. Troubleshooting

| Symptom | Check |
|---|---|
| `compose config is invalid` with `!override` | Compose older than v2.24: update Docker Desktop |
| Port 18080/18443 in use | Set `E2E_HTTP_PORT` and `E2E_HTTPS_PORT` in `.env.e2e` |
| Subnet clash on `up` | Change `DOCKER_SUBNET` in `.env.e2e` |
| `Could not copy Caddy's local CA certificate` | `DOMAIN` must be a `localhost` name, and caddy must be running (`compose ps`) |
| A smoke step fails | Read `smoke-<pass>.log`, then `KEEP=true` and `docker compose -p fo-e2e … logs api worker` |
| Login/OTP 429 after many runs | Rate limits are per client IP. Wait 5–10 minutes, or `down` and start again |
