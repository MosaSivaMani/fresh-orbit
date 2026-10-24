# Runbook: releases, rollback, logs and monitoring

One server runs the whole stack with Docker Compose (`infra/compose/docker-compose.prod.yml`): Caddy (HTTPS), api,
worker, migrations (one-shot), web, admin, PostgreSQL 17, and optionally Grafana + Loki + Alloy. Images are built by
`.github/workflows/deploy.yml` for linux/amd64 and linux/arm64 and pushed to GHCR as
`ghcr.io/mosasivamani/fresh-orbit-{api,worker,migrations,web,admin}:<tag>`.

Server setup: `oracle-setup.md`. Backups: `backup-restore.md`. Security settings (SMS DLT credentials,
forwarded-headers subnet): `security-configuration.md`.

**Before a release, run the local end-to-end check** (`e2e-check.md`): `infra/scripts/e2e-check.sh` builds the five
images, runs this compose file on a fresh database (project `fo-e2e`, Caddy on 127.0.0.1:18443 with its local CA),
proves migrations + reference data + idempotency, health behind Caddy, the Production guards, and `smoke.sh`, then
removes everything. Last result (2026-10-24): all green; findings in `docs/research/e2e-deploy-check-2026-10.md`.

Notes from that check that matter on a server:
- In Production, OTP sign-in returns 500 until the DLT provider client is written (`security-configuration.md` §2);
  `smoke.sh` against a Production server therefore needs `SMOKE_STOP_BEFORE_OTP=true`.
- `smoke.sh` follows the absolute image URLs the API returns behind Caddy (`FileStorage__PublicBaseUrl`).
- `deploy.sh` now prunes only `${IMAGE_REGISTRY}/fresh-orbit-*` images (`SKIP_IMAGE_PRUNE=true` to skip pruning);
  `FO_PROJECT` / `FO_COMPOSE_OVERRIDE` exist only for the e2e check, leave them unset on servers.

## 1. One-time GitHub setup (repository owner)

1. Settings → Environments → create **`staging`** (and later **`production`**). For a manual approval before each
   deploy, tick **Required reviewers** and add yourself.
2. In each environment add **secrets**:

   | Secret | Value |
   |---|---|
   | `DEPLOY_HOST` | server IP or host name |
   | `DEPLOY_USER` | `deploy` |
   | `DEPLOY_SSH_KEY` | private key of a key pair used **only** by CI (its `.pub` is in `/home/deploy/.ssh/authorized_keys`) |
   | `DEPLOY_KNOWN_HOSTS` | output of `ssh-keyscan -t ed25519 <host>` (recommended; without it the first connection is trusted blindly) |
   | `DEPLOY_SSH_PORT` | optional, default 22 |

   and **variables**: `DOMAIN` (e.g. `freshorbit.in`), optional `DEPLOY_PATH` (default `/opt/freshorbit`).

   **Repository** variables (Settings → Secrets and variables → Actions → Variables): `SITE_URL_STAGING` and
   `SITE_URL_PRODUCTION` (e.g. `https://staging.freshorbit.in`, `https://freshorbit.in`). The website inlines its
   public URL (`NEXT_PUBLIC_SITE_URL`: canonical links, sitemap, robots) at build time, so the build job needs it
   before any environment is chosen. The admin image needs no URL (see §7).
3. Application secrets (database password, JWT key, SMTP, SMS) never go to GitHub: they live only in
   `/opt/freshorbit/infra/compose/.env.prod` on the server (template: `infra/compose/.env.prod.example`).
4. GHCR packages are private with the repository. The workflow logs the server in with its short-lived token for each
   deploy and logs out afterwards. For manual `deploy.sh <new-tag>` runs, log in once on the server with a personal
   access token that has only `read:packages`: `docker login ghcr.io -u <github-user>`.

## 2. Releasing

| How | What happens |
|---|---|
| `git tag v1.4.0 && git push origin v1.4.0` | Builds all five images as `v1.4.0` (site URL: staging), deploys to `staging` |
| Actions → Deploy → Run workflow on ref `v1.4.0`, environment `production`, tag empty | Builds that ref as `v1.4.0-prod` (site URL: production), deploys to `production` |
| Actions → Deploy → Run workflow on a branch, tag empty | Builds the commit as `sha-<7 chars>` (`-prod` for production), deploys to the chosen environment |
| Actions → Deploy → Run workflow, tag `v1.3.2` / `v1.3.2-prod` | No build; redeploys an existing tag (rollback to an older release) |

Because the web image carries its site URL, only `-prod` tags can go to production and only non-`-prod` tags to
staging; the workflow refuses the other combinations. The .NET and admin images are identical in both builds.

The deploy job copies `infra/` (compose, Caddy, scripts, monitoring) to the server, then runs
`infra/scripts/deploy.sh <tag>`, which:

1. pulls the tag's images;
2. starts Postgres and waits for it;
3. takes a **local pre-deploy backup** (skipped on the very first deploy);
4. runs **migrations** (`--sample` too when `SEED_SAMPLE_DATA=true`, staging only; refused in Production). If they
   fail, nothing else changes and the old release keeps running;
5. `docker compose up -d` the new release and waits up to 4 minutes for every service to be running and healthy
   (`/health` for api, `/` for web and admin, Caddy's admin endpoint, `pg_isready`), and for the worker to stay up;
6. records the tag in `/opt/freshorbit/.deploy/current-tag` (the previous one in `previous-tag`), removes older
   images (keeps current and previous).

If step 5 fails, it starts the previous tag again and exits non-zero (the workflow run turns red).

**Migrations are not rolled back.** Write them expand/contract: add columns/tables first, switch code in a later
release, drop old columns later still, so the previous release always runs on the new schema. If a schema change must
be undone, restore the pre-deploy backup (`backup-restore.md` §4).

## 3. Rollback and manual operations (on the server, as `deploy`)

```bash
cd /opt/freshorbit
infra/scripts/deploy.sh --rollback          # previous tag; uses images already on the host, no registry login
infra/scripts/deploy.sh v1.3.2              # any tag (needs a GHCR login if the images are not on the host)
SKIP_PULL=true infra/scripts/deploy.sh v1.3.2   # images already present (e.g. registry outage)
infra/scripts/logs.sh --status              # containers, health, memory, disk
```

Manual compose commands need the env file and a tag:

```bash
cd /opt/freshorbit/infra/compose
IMAGE_TAG=$(cat ../../.deploy/current-tag) docker compose --env-file .env.prod -f docker-compose.prod.yml ps
```

Changing only `.env.prod` (e.g. SMTP): run `infra/scripts/deploy.sh "$(cat .deploy/current-tag)"`; compose
recreates only the services whose settings changed.

## 4. Logs

```bash
infra/scripts/logs.sh                       # follow everything (last 100 lines each)
infra/scripts/logs.sh api worker            # follow some services
infra/scripts/logs.sh --since 2h -n 1000 --no-follow api
infra/scripts/logs.sh --errors --since 1h   # api/worker Error and Fatal lines (Serilog JSON)
```

Container logs are JSON files rotated by Docker (10 MB × 5 per container). The .NET hosts log Serilog compact JSON:
`@l` is the level (absent = Information), `@m` the message, `@x` the exception.

## 5. Monitoring (optional profile)

Measured idle use on a test run: Loki ~75 MB, Alloy ~57 MB, Grafana ~155 MB (limits 512/256/256 MB). It fits the
8 GB budget; turn it off first if memory gets tight.

1. In `.env.prod`: `COMPOSE_PROFILES=monitoring`, a strong `GRAFANA_ADMIN_PASSWORD`, and for e-mail alerts
   `GRAFANA_SMTP_ENABLED=true` plus `ALERT_EMAIL`. Redeploy the current tag.
2. Grafana listens on the server's loopback only: `ssh -L 3001:127.0.0.1:3001 deploy@<server>` then open
   http://localhost:3001. Explore → Loki: `{service="api"}`, `{service=~"api|worker", level="Error"}`,
   `{service="caddy"} | json | status >= 500`.
3. Provisioned alerts (`infra/monitoring/alerting/alerts.yml`): **Backend errors** (any Error/Fatal from api or
   worker in 5 minutes) and **Upstream failures at Caddy** (more than five 502–504 in 5 minutes, for 2 minutes).
4. **Server-down alerts must come from outside the server.** Use a free external HTTP check on
   `https://api.<domain>/health` (e.g. Uptime Kuma, MIT, on another machine, or a free hosted uptime monitor), and a
   dead-man's switch for backups (`BACKUP_PING_URL`, e.g. a free healthchecks.io check; healthchecks.io is BSD-licensed
   and can be self-hosted).
5. GlitchTip (error tracking, MIT) was considered; it needs its own Postgres, Redis, web and worker (~1 GB), so it is
   not in the profile. Revisit if Loki error queries are not enough.

## 6. Troubleshooting

| Symptom | Check |
|---|---|
| Workflow fails at "Configure SSH" | Environment secrets set? `DEPLOY_KNOWN_HOSTS` matches the server key? |
| `deploy.sh`: `Missing .../.env.prod` | Create it from `.env.prod.example` (`oracle-setup.md` §7) |
| compose: `required variable X is missing a value` | Fill X in `.env.prod`; every required key is marked in the example |
| Migrations failed | `logs.sh migrations --no-follow`; fix and release again; old release is still running. One `Failed executing DbCommand` Error on a brand-new database (EF reads its history table before creating it) is expected; the run still exits 0 |
| api/worker exit right after start (exit code 139) | A start-up validator refused the configuration: `logs.sh api --no-follow -n 50` shows `OptionsValidationException` naming the key (`security-configuration.md`) |
| api `unhealthy` | `logs.sh api --no-follow -n 300`; `/health` checks the database connection |
| Caddy cannot get certificates | DNS A records point at the server? Ports 80/443 open in the Oracle security list and UFW? Cloudflare grey cloud for the first issuance |
| 502 from Caddy | `logs.sh --status`: is the upstream (api/web/admin) running? |
| Rate limits hit everyone at once | `DOCKER_SUBNET` / forwarded-headers settings (`security-configuration.md`) |
| Disk full | `docker system df`; old backups in `BACKUP_DIR`; `docker image prune` |

## 7. How the web and admin apps are served

| | Website `apps/web` (`@fresh/web`, Next.js) | Admin `apps/admin` (`@fresh/admin`, Vite SPA) |
|---|---|---|
| Image | Next.js standalone server, Node 22, UID 1000 | static files in a non-root Caddy (UID 65534) on :8080 |
| Build switch | `NEXT_OUTPUT=standalone` (set by the Dockerfile) turns on `output: "standalone"` traced from the repository root in `next.config.ts`; local `next dev`/`next start` unchanged | none |
| Build-time values | `NEXT_PUBLIC_SITE_URL` (build arg from `SITE_URL_*`) | none (`VITE_API_ORIGIN` stays empty) |
| Runtime values | `FRESH_API_ORIGIN=http://api:8080` (compose) | none |
| How the browser reaches the API | Same origin only: the Next.js `/api/v1/*` proxy and `/bff/auth/*` handlers call the API inside the compose network (`docs/design/web.md` §4) | Same origin: Caddy routes `admin.<domain>/api/*` and `/media/*` to the API (shared `api_routes` snippet), so no CORS and one image for every environment |
| CSP (Caddyfile) | `script-src 'self' 'unsafe-inline'` (Next.js inline bootstrap), `connect-src 'self'`, images also from `api.<domain>` | Strict (D40): `default-src 'none'`, `script-src 'self'` (no inline, no third-party, no eval), `style-src 'self'`, `connect-src 'self'`, `require-trusted-types-for 'script'` |
| Health | `GET /` | `GET /` |

Verified on 2026-10-24 with the real apps through Caddy in headless Chromium: both pages render with no CSP
violations, except one harmless report on the admin: zod's start-up probe `Function("")` (in a try/catch; zod then
runs without JIT). The admin team can silence it with `z.config({ jitless: true })`.

Rules for app changes:
- Any new third-party origin (fonts, analytics, maps, payment gateway) must be added to the site's CSP in
  `infra/caddy/Caddyfile` and agreed with the manager; the admin's CSP should stay without third-party scripts.
- Upload paths with bigger bodies need a matching `request_body` rule in the Caddyfile (product images 6 MB, complaint
  photos 21 MB, everything else 1 MB).
- Both images are built with `turbo prune <package> --docker` and `pnpm install --frozen-lockfile`; keep
  `pnpm-lock.yaml` current. Package names/folders can be overridden with build args (`WEB_PACKAGE`/`WEB_DIR`,
  `ADMIN_PACKAGE`/`ADMIN_DIR`).
