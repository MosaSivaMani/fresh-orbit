# Runbook: go-live on a new production server with an empty database

How to install Fresh Orbit for real customers on a brand-new Hostinger VPS (D127), with no old server to copy from
(gap audit L-32, F-15; DNS from L-59, mail DNS L-48, alerts L-29, L-30, L-44). The database starts empty: the
migrations create the schema and load the reference data (`docs/product/reference-data.md`), never sample data
(rule 16). Everything else (the first admin, the business profile, the first city and store) is entered by hand in
the admin dashboard.

Other runbooks this one uses: `move-to-hostinger.md` (buying the VPS), `deploy.md` (GitHub setup, releases),
`security-configuration.md` (SMS DLT and the other start-up settings), `backup-restore.md` (offsite backups),
`play-store.md` and `mobile-release.md` (the app). Plan one working day for §1–§7 and keep a day free before opening.

Every command below was checked against the scripts on `main` (`infra/scripts/*.sh`, `infra/compose/`); run them
from the repository root on your own computer (not the company laptop, CLAUDE.md rule 18), or on the server where
marked.

## 0. Have these ready (owner)

| Item | Goes into | Gap |
|---|---|---|
| Domain on Cloudflare | `DOMAIN`, DNS (§2) | L-1 |
| Hostinger KVM 2, India data centre if offered | §1 | L-2 |
| DLT registration done: PE id, header, 13 template ids, link domain whitelisted, Fast2SMS API key | `SMS_DLT_*` | L-3, L-31 |
| SMTP provider account with the domain verified | `SMTP_*`, mail DNS (§2.2) | L-48, L-49 |
| Legal name, registered address, GSTIN, FSSAI licence of the store | Business profile and store (§6) | L-4, L-5 |
| CA-confirmed GST rates per product kind | Tax rules (§6) | L-13 |
| Grievance officer name and e-mail | `FRESH_GRIEVANCE_OFFICER_*` | L-7 |
| Offsite bucket (Cloudflare R2 or Backblaze B2) and a bucket-scoped key | `rclone.conf` (§4) | L-10 |
| A free healthchecks.io check (backups) and a free uptime monitor (site) | `BACKUP_PING_URL`, §8 | L-30, L-44 |
| The team password manager, named | Every secret below | L-66 |
| Launch city, store address and coordinates, pincodes, delivery slots, products, prices, opening stock | §6 | |

## 1. Server

1. Buy the VPS as in `move-to-hostinger.md` §1 step 1 (KVM 2, Ubuntu 24.04, your SSH key) and open TCP 22, 80, 443
   and UDP 443 in Hostinger's firewall if it is on.
2. Harden it and install Docker. Hostinger gives only `root`, so the deploy user gets sudo; KVM 2 has one disk, so no
   `--docker-data-root`:

   ```bash
   scp infra/scripts/server-setup.sh root@<server-ip>:/tmp/
   ssh root@<server-ip> "bash /tmp/server-setup.sh --ssh-key '$(cat ~/.ssh/id_ed25519.pub)' --sudo --swap 4G"
   ssh deploy@<server-ip>     # in a NEW terminal; must work before you close the root session
   ```

   The script sets UTC, the `deploy` user, SSH keys only (no root, no passwords), UFW, fail2ban, swap, unattended
   upgrades (reboot window 22:30 UTC), Docker with log rotation, and creates `/opt/freshorbit` and
   `/var/backups/freshorbit`. Then continue here.

## 2. DNS (Cloudflare, free plan)

### 2.1 Web records

1. Add the domain to Cloudflare and change the nameservers at the registrar to the two Cloudflare gives you.
2. Records, all to the server IP, **DNS only (grey cloud)** until Caddy has its certificates (§5):

   | Type | Name | Content |
   |---|---|---|
   | A | `@` | server IP |
   | A | `www` | server IP |
   | A | `admin` | server IP |
   | A | `api` | server IP |
   | CAA | `@` | `0 issue "letsencrypt.org"` (only Let's Encrypt may issue certificates for the domain) |

   Add AAAA records only if the VPS has an IPv6 address and UFW allows it (`server-setup.sh` opens the ports for
   both).
3. After the first deploy has certificates you may switch the four A records to **Proxied** (orange) as in
   `oracle-setup.md` §5 step 3: SSL/TLS **Full (strict)**, "Always Use HTTPS" **off**, `TRUSTED_PROXIES` set to
   Cloudflare's ranges plus `private_ranges`, Bot Fight Mode **off** (it blocks the app). With proxied records,
   Cloudflare issues its own edge certificates; add `0 issue "pki.goog"`, `0 issue "letsencrypt.org"` and
   `0 issue "digicert.com"` CAA records or remove the CAA record, as Cloudflare's dashboard advises.

### 2.2 Mail records (order and staff e-mails must not land in spam)

Use the exact values the SMTP provider shows when you verify the domain; the table shows the shape.

| Type | Name | Content |
|---|---|---|
| TXT | `@` | SPF: `v=spf1 include:<provider's SPF domain> ~all`. One SPF record only; merge if one exists |
| TXT or CNAME | `<selector>._domainkey` | DKIM key from the provider |
| TXT | `_dmarc` | `v=DMARC1; p=none; rua=mailto:<owner's address>` to start; move to `p=quarantine` after two weeks of clean reports |
| TXT / CNAME | as the provider says | Domain verification |

Send `SMTP_FROM_ADDRESS` from this domain (for example `orders@<domain>`), never from a free-mail address.

## 3. Files and settings on the server

1. Copy the infra files (from the repository root, at the commit you will deploy):

   ```bash
   tar -czf - infra/compose/docker-compose.prod.yml infra/compose/.env.prod.example infra/caddy infra/scripts \
     infra/monitoring | ssh deploy@<server-ip> 'tar -xzf - -C /opt/freshorbit'
   ```

2. On the server, create the settings file and the two secrets:

   ```bash
   cd /opt/freshorbit/infra/compose
   cp .env.prod.example .env.prod && chmod 600 .env.prod
   openssl rand -hex 24   # → POSTGRES_PASSWORD
   openssl rand -hex 32   # → JWT_SIGNING_KEY
   nano .env.prod
   ```

3. Fill every value below. Save each secret in the password manager as you go.

   | Key | Production value |
   |---|---|
   | `APP_ENVIRONMENT` | **`Production`** (the template's value; never `Staging` here: sample data and the Fake gateway are refused only in Production) |
   | `SEED_SAMPLE_DATA` | **`false`** (the Migrations host refuses `--sample` in Production anyway) |
   | `DOMAIN`, `ACME_EMAIL` | the domain; an address that reads certificate expiry mail |
   | `TRUSTED_PROXIES` | `private_ranges` while DNS is grey (§2.1) |
   | `POSTGRES_PASSWORD`, `JWT_SIGNING_KEY` | from step 2 |
   | `BUSINESS_NAME`, `SMTP_FROM_NAME` | `Fresh Orbitt` (D170, the template's value since L-75). It goes into every SMS, so keep it as the owner registered the brand on DLT |
   | `SUPPORT_PHONE`, `SUPPORT_EMAIL` | the store's support contacts |
   | `FRESH_GRIEVANCE_OFFICER_NAME`, `_DESIGNATION`, `_EMAIL`, `FRESH_GRIEVANCE_RESPONSE_TIME` | from the owner (shown on the website only when name, e-mail and response time are all set) |
   | `SMTP_HOST`, `SMTP_PORT`, `SMTP_USE_TLS`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM_ADDRESS` | from the SMTP provider. An empty `SMTP_HOST` only logs e-mails (L-19), so never leave it empty here |
   | `SMS_DLT_API_KEY`, `SMS_DLT_SENDER_ID`, `SMS_DLT_PRINCIPAL_ENTITY_ID`, all 13 `SMS_DLT_TEMPLATE_*` | from the DLT registration (`security-configuration.md` §2.1). The api and worker refuse to start without them |
   | `COMPOSE_PROFILES` | `monitoring` (alerts, L-29) |
   | `GRAFANA_ADMIN_PASSWORD` | strong, required with the profile |
   | `GRAFANA_SMTP_ENABLED`, `ALERT_EMAIL` | `true`; the owner's or on-call address |
   | `BACKUP_REMOTE` | the crypt remote, e.g. `offsite:production` (§4) |
   | `BACKUP_PING_URL` | the healthchecks.io ping URL (required here, L-44) |
   | `ERROR_REPORTING_DSN` | empty, or a GlitchTip project DSN |

4. Check the two values that keep sample data out:

   ```bash
   grep -E '^(APP_ENVIRONMENT|SEED_SAMPLE_DATA)=' .env.prod    # must print Production and false
   ```

## 4. Offsite backups

1. Create `/opt/freshorbit/infra/compose/rclone.conf` with the bucket and the `crypt` remote exactly as in
   `backup-restore.md` §2 (`chmod 600`). Keep both crypt passwords in the password manager: without them the
   offsite backups cannot be read.
2. Install the nightly cron as root (`backup-restore.md` §3). Run the first backup after the first deploy (§5).

## 5. GitHub and the first deploy

1. GitHub environment **`production`** (`deploy.md` §1): **Required reviewers** = the owner; secrets `DEPLOY_HOST`,
   `DEPLOY_USER=deploy`, `DEPLOY_SSH_KEY` (a separate CI key pair:
   `ssh-keygen -t ed25519 -f freshorbit-ci -C ci`, its `.pub` added to `/home/deploy/.ssh/authorized_keys` on the
   server), **`DEPLOY_KNOWN_HOSTS`** (`ssh-keyscan -t ed25519 <server-ip>`; the deploy refuses production without it,
   L-60); variable `DOMAIN`. Repository variable `SITE_URL_PRODUCTION` = `https://<domain>`.
2. Deploy: **Actions** › **Deploy** › **Run workflow**, branch `main`, environment `production`, tag empty. It waits
   for green CI on that commit, builds `sha-<7 chars>-prod`, copies `infra/` and runs `infra/scripts/deploy.sh` on
   the server. On an empty server, `deploy.sh` skips the pre-deploy backup, creates the volumes, runs the
   migrations (schema and reference data; no `--sample`, because `SEED_SAMPLE_DATA=false`), starts everything and
   waits for health. Caddy then gets the certificates (DNS must already point at the server, grey cloud).
   Do not push a `v*` tag for this: a tag push deploys to `staging`, which does not exist after D127 (L-17).
3. On the server: `infra/scripts/logs.sh --status` (everything running and healthy), and
   `curl -fsS https://api.<domain>/health` prints `Healthy`.
4. First backup and drill:

   ```bash
   cd /opt/freshorbit
   infra/scripts/backup.sh --label first-deploy
   infra/scripts/restore.sh --list                     # the backup is local and on the remote
   infra/scripts/restore.sh --drill remote:<name from the list>
   ```

   Add the drill to the log in `backup-restore.md` §5. The healthchecks.io check turns green.

## 6. First admin and the store's data

1. **First admin**, on the server. The Migrations host creates the first ADMIN when no active admin exists. The
   password needs at least 12 characters and must not be a common one:

   ```bash
   cd /opt/freshorbit/infra/compose
   read -rs ADMIN_PASSWORD            # typed, not echoed, and not in the shell history
   IMAGE_TAG=$(cat ../../.deploy/current-tag) docker compose --env-file .env.prod -f docker-compose.prod.yml \
     run --rm migrations --create-admin --email <owner e-mail> --name "<Owner name>" --password "$ADMIN_PASSWORD"
   unset ADMIN_PASSWORD
   ```

   The command logs the e-mail and the new staff id, never the password (details and refusals:
   `security-configuration.md` §5 step 2). Sign in at `https://admin.<domain>`.
2. **Staff:** Settings › Staff & roles: one account per person, with only the roles and stores they need. No shared
   logins.
3. **Business profile** (Settings › Business profile): legal name, trade name `Fresh Orbitt`, registered address,
   support phone and e-mail, time zone `Asia/Kolkata`, **GSTIN** and **FSSAI licence number**. Invoices are refused
   without a GSTIN (L-4), and the website footer shows these details (D300).
4. **Order policies** and **Number series** (Settings): the reference data created the default rows; check the
   invoice and order number prefixes and the financial year before the first order, because numbers are never
   reused once issued.
5. **Tax rules** (Catalog › Tax rules): the GST rate per tax category, as the CA confirmed (L-13). The reference
   data ships the categories only, never rates.
6. **City** (Business › Cities & Locations): create the launch city as DRAFT with its code (UPPER_SNAKE), name,
   two-letter state code, state name and time zone. No city name is in the code (rule 7).
7. **Store** (on the city): a fulfillment location with address, pincode, latitude and longitude, phone,
   **FSSAI licence**, cold storage ticked and the weekly opening hours; then set it ACTIVE (LOC-R11 lists what it
   needs).
8. **Service area** for that store: delivery fee, minimum order, free-delivery threshold, and every pincode it
   serves.
9. **Delivery** › slot templates for the store (delivery windows, cut-offs, capacity).
10. **Products** (Catalog › Products): categories, products, variants, preparations and pictures.
11. **Pricing**: a price for every variant at the store and a charge for each default preparation. No price means
    not for sale there (rule 7).
12. **Inventory**: a stock item per variant at the store and the opening stock (Receive stock).
13. **Activate the city:** the city page's readiness list must show every check done (store, service area, pincode,
    slot template, price, stock; LOC-R6); then **Activate**.

## 7. Production checks before opening

1. Read-only smoke test from the server (it writes nothing, D319):

   ```bash
   cd /opt/freshorbit
   API_URL=https://api.<domain> SMOKE_READ_ONLY=true SMOKE_PINCODE=<a launch pincode> infra/scripts/smoke.sh
   ```

   Never run `smoke.sh` without `SMOKE_READ_ONLY=true` here: the full run would create a "Smoke" city and product
   that cannot be purged.
2. Guards: `curl -s -o /dev/null -w '%{http_code}\n' https://api.<domain>/api/v1/dev/sms` prints `404` (the
   development SMS outbox exists only in Development, Testing and Demo).
3. In a browser: `https://<domain>` shows the city's products and, in the footer, the legal name, address, GSTIN,
   FSSAI number and grievance officer; `https://www.<domain>` and `http://` redirect to HTTPS; the admin signs in.
4. SMS: request an OTP for your own phone; the SMS arrives and the api log shows it was accepted
   (`security-configuration.md` §2.1 step 6).
5. One real order: sign in with a staff member's own phone, order something small for the launch pincode, pay it
   cash on delivery, and take it through packing, dispatch and delivery in the admin. Check the order SMS and
   e-mails arrive, the invoice is issued with the GSTIN, and the payment shows as collected. This is a real sale in
   the books; keep it.
6. Search engines: `https://<domain>/robots.txt` allows crawling (Production only, D301).

## 8. Alerts and the first week

- **External uptime monitor** (free hosted, or Uptime Kuma on another machine): HTTPS checks on
  `https://api.<domain>/health`, `https://<domain>/` and `https://admin.<domain>/`, every 5 minutes, with a
  certificate-expiry warning (L-63), alerting the owner by e-mail and SMS or app (L-30). Server-down alerts must come
  from outside the server.
- **Grafana** (loopback only, `deploy.md` §5): sign in once through the SSH tunnel and send a test alert e-mail.
- **Backups:** the cron runs at 21:30 UTC; next morning `journalctl -t freshorbit-backup --since yesterday` on the
  server and a green healthchecks.io check.
- **Incident roles** filled in `incident-response.md` §1; the team knows where it is.
- **App:** build the Play bundle with `mobile-release.yml` (repository variables `API_BASE_URL=https://api.<domain>`,
  `WEBSITE_BASE_URL=https://<domain>`, `APP_LINK_HOST=<domain>`; `mobile-release.md` "Store build") and fill
  the Play Console forms from `play-store.md`.
- Record the go-live (date, tag, server, who did it) in `docs/team/progress.md` and any choice made on the way in
  `docs/decisions/log.md`.
