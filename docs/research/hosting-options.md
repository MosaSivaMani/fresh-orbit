# Hosting options (evaluated 2026-10-23)

Decision: D25 in `docs/decisions/log.md`. Prices change; check current pricing before buying.

## Free platforms

| Platform | Free amount | Fit for Fresh Orbit |
|---|---|---|
| Oracle Cloud Always Free | ARM VM up to 4 OCPU, 24 GB RAM, ~200 GB disk; India regions | **Chosen for staging and soft launch.** Whole compose stack fits |
| Google Cloud free tier | One e2-micro (1 GB), US regions | Too small |
| AWS free tier | Time-limited credits | Temporary only |
| Azure free account | 12 months small VM | Temporary only |
| Render / Koyeb / Railway free | Apps sleep or are capped; small DBs | Demos only |
| Vercel Hobby | Free web hosting | Not allowed for commercial use |
| Neon / Supabase free Postgres | Small DB with compute limits | Dev only; risky for orders and money |
| GitHub Student Developer Pack | Credits (DigitalOcean, Azure, others) | Useful if eligible |

### Oracle Always Free caveats
| Caveat | Meaning |
|---|---|
| Card at sign-up | Identity check; sign-ups are sometimes rejected |
| Out of capacity | Free ARM VMs in India regions are often full; retry |
| Idle reclaim | Idle free VMs can be reclaimed; Pay-As-You-Go upgrade generally avoids it while staying at ₹0 within limits (check current policy) |
| No SLA | No uptime guarantee or support |
| ARM64 | Fine: .NET, Node, Postgres all support it; build multi-arch images |

## Paid production: Hostinger

| Product | Works? | Why |
|---|---|---|
| Web / cloud hosting | No | PHP/WordPress only; no .NET, Docker, Postgres |
| VPS KVM 1 (1 vCPU, 4 GB) | Tight | Not enough for API + worker + web + Postgres |
| **VPS KVM 2 (2 vCPU, 8 GB)** | **Chosen for production** | Fits launch and first city |
| VPS KVM 4 | Later | Upgrade without code change |

Pick the India data centre if offered (else Singapore), Ubuntu 24.04 LTS.

## Server stack (both hosts)

| Layer | Tool | Licence |
|---|---|---|
| Containers | Docker + Compose | Open-source |
| HTTPS / routing | Caddy (Let's Encrypt) | Open-source |
| Database | PostgreSQL 17 | Open-source |
| Files | Local disk via `IFileStorage` (D21) | — |
| Logs / errors | Grafana + Loki or GlitchTip | Open-source |
| Security | UFW, Fail2ban, SSH keys only | Open-source |
| Deploy | GitHub Actions → GHCR → server pull | Free tier |
| DNS / protection | Cloudflare free | Free tier |
| Offsite backup | Cloudflare R2 or Backblaze B2 free tier | Free tier |
| Optional dashboard | Coolify | Open-source; uses extra RAM; not chosen |

## Unavoidable costs
Domain (yearly), production VPS, SMS per message, Google Play one-time, Apple yearly (iOS), gateway fees when online payments go live.
