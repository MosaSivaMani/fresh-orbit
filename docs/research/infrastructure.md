# Infrastructure choices for Phase 9 (analysed 2026-10-24, E5)

Context: D21 (media on local disk), D24 (stack), D25 (Oracle Always Free ARM, then Hostinger KVM 2), CLAUDE.md
rules 13 (free and open source first) and 14 (no copied code). Runbooks: `docs/runbooks/`. Files: `infra/`,
`.github/workflows/deploy.yml`.

## 1. Proposed decisions (for the manager to log)

| # | Topic | Proposal |
|---|---|---|
| a | .NET runtime image | `mcr.microsoft.com/dotnet/aspnet:10.0.12-alpine3.24-extra` for Api, Worker and Migrations (all three need the ASP.NET Core shared framework through `FreshOrbit.Hosting`) |
| b | .NET build | SDK `10.0.102` (matches `backend/global.json`) on the **build** platform, cross-publishing `--os linux-musl -a $TARGETARCH`: no QEMU for the .NET build |
| c | Admin serving | Separate `fresh-orbit-admin` image (static files in a non-root Caddy on :8080) behind the edge Caddy, so admin deploys like any other image and the edge Caddy stays the stock image |
| d | Logs | Grafana + Loki + **Grafana Alloy** (not Promtail) in an optional `monitoring` profile; GlitchTip deferred |
| e | Offsite backups | rclone `crypt` remote over Cloudflare R2 or Backblaze B2; backups contain personal data so they are encrypted before upload |
| f | Deploy | GitHub Actions → GHCR → SSH → `deploy.sh <tag>`; health-gated with automatic return to the previous tag; migrations are expand/contract and never rolled back automatically |
| g | Network | Fixed compose subnet (`DOCKER_SUBNET`, default `172.30.0.0/24`) trusted by the API for forwarded headers (coordinator request; details in `docs/runbooks/security-configuration.md`) |

## 2. Runtime base image

| Option | Size | Shell / wget for healthchecks | Time zones (`Asia/Kolkata`) | Notes |
|---|---|---|---|---|
| `aspnet:10.0-noble-chiseled` | smallest | no | no (needs `-extra`) | healthcheck would need a helper binary |
| `aspnet:10.0-noble` | large | no wget | yes | |
| **`aspnet:10.0-alpine-extra`** | small (~86 MB compressed, amd64 api) | busybox `wget` | yes (tzdata) | SkiaSharp ships `linux-musl-x64/arm64` native assets in `SkiaSharp.NativeAssets.Linux.NoDependencies` |

Time zones matter: Settings, Reporting and Payments call `TimeZoneInfo.FindSystemTimeZoneById` with IANA ids.
`InvariantGlobalization` is already on, so ICU is not needed.

All images run as non-root: .NET UID 1654 (`app`), web UID 1000 (`node`), admin UID 65534 (`nobody`). Only the edge
Caddy runs as root inside its container (binds 80/443).

## 3. Version pins (all at least two weeks old on 2026-10-24)

| Image | Tag | Published |
|---|---|---|
| .NET SDK | `mcr.microsoft.com/dotnet/sdk:10.0.102-noble` | 2026-02 |
| ASP.NET runtime | `mcr.microsoft.com/dotnet/aspnet:10.0.12-alpine3.24-extra` | 2026-09-18 (10.0.13 images were rebuilt 2026-10-22, too new) |
| Node | `node:22.23.3-alpine3.24` | 2026-09-23 |
| Turborepo (prune) | `turbo@2.11.7` (same as root `package.json`) | — |
| Caddy | `caddy:2.11.6-alpine` | 2026-10-04 |
| PostgreSQL | `postgres:17.10-alpine3.24` | 2026-07-08 (17.11 is too new) |
| Loki | `grafana/loki:3.7.8` | 2026-09-17 |
| Alloy | `grafana/alloy:v1.20.1` | 2026-09-28 |
| Grafana | `grafana/grafana:13.2.3` | 2026-09-29 |
| rclone | `rclone/rclone:1.75.2` | 2026-10-09 |
| busybox (backup tar) | `busybox:1.37.0` | 2026-09-23 |
| GitHub Actions | `actions/checkout@v4`, `docker/setup-qemu-action@v3`, `docker/setup-buildx-action@v3`, `docker/login-action@v3`, `docker/build-push-action@v6` | major tags, as in `ci.yml` |

Bump them together, monthly, after checking release notes; a Renovate/Dependabot configuration could automate it later.

## 4. Memory budget (8 GB host)

| Service | Limit | Measured idle (local test) |
|---|---|---|
| postgres (`shared_buffers` 768 MB, `effective_cache_size` 2 GB, `max_connections` 120) | 2.5 GB | — |
| api | 1 GB | — |
| worker | 768 MB | — |
| web | 512 MB | not built yet |
| caddy | 256 MB | — |
| admin | 64 MB | 14 MB |
| loki / alloy / grafana (optional) | 512 / 256 / 256 MB | 75 / 57 / 155 MB |
| OS, Docker, page cache | ~1.9 GB | |

Oracle A1 (24 GB) has plenty of headroom; the limits are set for Hostinger KVM 2.

## 5. Monitoring options

| Option | Licence | RAM | Fit |
|---|---|---|---|
| **Grafana + Loki + Alloy** | AGPL-3.0 / AGPL-3.0 / Apache-2.0 | ~0.3 GB idle | Chosen (optional profile). Promtail reached end of life in 2026; Alloy is its replacement |
| GlitchTip | MIT | ~1 GB (own Postgres, Redis, web, worker) | Deferred: would need SDKs in the apps; Loki error queries cover launch |
| Uptime Kuma | MIT | ~100 MB | Recommended on **another** machine for server-down alerts |
| healthchecks.io | BSD-3 (hosted free tier or self-host) | — | Dead-man's switch for nightly backups (`BACKUP_PING_URL`) |

## 6. Findings during verification

1. **Npgsql GSSAPI probe on Alpine**: without Kerberos libraries Npgsql logs
   `Cannot load library libgssapi_krb5.so.2` on every new connection. The production connection string sets
   `Gss Encryption Mode=Disable` (password authentication inside the compose network). Integration item: consider
   setting it in `.env.example` too, or adding `krb5-libs` if Kerberos is ever needed.
2. **Migrations host exit code on an unhandled exception is 139** (segmentation fault during the crash) on Alpine,
   not the usual 134. It is still non-zero, so `service_completed_successfully` and `deploy.sh` treat it as failure;
   worth a look by the backend owner (a clean `return 1` on failure in `Program.cs` would avoid it).
3. **No EF migrations exist yet** (Phase 3 manager item): the migrations image runs, then fails at reference-data
   loading because tables are missing. Until the initial migration is merged, a real deploy stops at the migrations
   step (by design, nothing else changes).
4. **Media rule duplicated at the edge**: Caddy serves `api.<domain>/media/*` straight from the volume and applies the
   Api's rule (only `.webp`, `.jpg`, `.png` are public; originals 404). If `FileStorageExtensions.PublicContentTypes`
   changes, change `infra/caddy/Caddyfile` too.
5. **`FileStorage__PublicBaseUrl`** is set to `https://api.<domain>/media` so image URLs work from the web, admin and
   mobile apps alike.
6. **Docker-published ports bypass UFW**; only Caddy (80/443) and Grafana (127.0.0.1 only) publish ports.
7. **Oracle Ubuntu images** carry iptables REJECT rules in `/etc/iptables/rules.v4` that block 80/443 even with UFW
   open; `server-setup.sh` removes just those two rules.
8. **Compose `:?` checks inactive profiles too**, so the Grafana password cannot be enforced that way; it is
   documented as required before enabling the profile (Grafana is reachable only through an SSH tunnel).

## 7. Not verified here

- `web.Dockerfile` (no `apps/web` yet) and `admin.Dockerfile` against the real `apps/admin` (verified with
  `apps/playground` as a stand-in Vite app).
- `server-setup.sh` on a real Ubuntu 24.04 VM (shellcheck-clean only); Oracle/Hostinger consoles; Let's Encrypt
  issuance on a real domain; the GitHub workflow run (actionlint-clean only); R2/B2 uploads (rclone path tested with an
  in-memory remote).
