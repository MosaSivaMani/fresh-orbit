# Runbook: backups, restore and the monthly restore drill

What is backed up: the PostgreSQL database (all modules and the Hangfire schema) and the media volume (product
images; decision D21). Not backed up: container images (rebuilt from git tags), Caddy certificates (re-issued
automatically), `.env.prod` and `rclone.conf` (keep them in the team password manager).

Scripts: `infra/scripts/backup.sh`, `infra/scripts/restore.sh`. Settings in `.env.prod`: `BACKUP_DIR`,
`BACKUP_RETENTION_DAYS`, `BACKUP_REMOTE`, `BACKUP_REMOTE_RETENTION_DAYS`, `BACKUP_PING_URL`.

## 1. What a backup contains

`$BACKUP_DIR/<UTC stamp>[-label]/`:

| File | Content |
|---|---|
| `db.dump` | `pg_dump --format=custom` (compressed); verified with `pg_restore --list` when written |
| `media.tar.gz` | the whole media volume |
| `manifest.txt` | time, release tag, host, Postgres version |
| `SHA256SUMS` | checksums, checked before any restore |

Local copies are kept `BACKUP_RETENTION_DAYS` (default 7); `deploy.sh` also makes a local `pre-<tag>` backup before
each migration. Offsite copies are uploaded with rclone (MIT, run as the pinned `rclone/rclone` image) and kept
`BACKUP_REMOTE_RETENTION_DAYS` (default 30).

## 2. Offsite storage setup (once)

Both options have a free tier of about 10 GB (check current limits); the dumps of a single-city store are small.
Use a **bucket-scoped key** that can only read and write that bucket. Never commit keys.

**Backups contain customer personal data** (names, phone numbers, addresses). Encrypt them before they leave the
server with an rclone `crypt` remote, as below.

Create `/opt/freshorbit/infra/compose/rclone.conf` (owner `deploy`, `chmod 600`; ignored by git):

Cloudflare R2 (no egress fees): Cloudflare → R2 → create bucket `freshorbit-backups` → Manage API tokens → create a
token with **Object Read & Write** for that bucket only. Note the access key id, secret and account id.

```ini
[r2]
type = s3
provider = Cloudflare
access_key_id = <R2 access key id>
secret_access_key = <R2 secret access key>
endpoint = https://<cloudflare-account-id>.r2.cloudflarestorage.com
acl = private
no_check_bucket = true

[offsite]
type = crypt
remote = r2:freshorbit-backups
password = <output of: rclone obscure 'a long random passphrase'>
password2 = <output of: rclone obscure 'a second long random passphrase'>
```

Backblaze B2 alternative: B2 → create private bucket `freshorbit-backups` → Application Keys → key restricted to that
bucket (read and write).

```ini
[b2]
type = b2
account = <keyID>
key = <applicationKey>
hard_delete = true

[offsite]
type = crypt
remote = b2:freshorbit-backups
password = <rclone obscure ...>
password2 = <rclone obscure ...>
```

`rclone obscure` can be run without installing rclone:
`docker run --rm rclone/rclone:1.75.2 obscure 'passphrase'`. **Store both passphrases in the password manager: without
them the offsite backups cannot be read.**

Then in `.env.prod`: `BACKUP_REMOTE=offsite:staging` (or `offsite:production`). Test:
`infra/scripts/backup.sh` then `infra/scripts/restore.sh --list`.

Optional: add a bucket lifecycle rule (R2 "Object lifecycle", B2 "Lifecycle settings") deleting objects older than
~35 days as a second safety net for retention.

## 3. Nightly schedule

As root on the server (21:30 UTC = 03:00 IST, before the 22:30 UTC reboot window):

```bash
cat >/etc/cron.d/freshorbit-backup <<'EOF'
SHELL=/bin/bash
PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
30 21 * * * deploy /opt/freshorbit/infra/scripts/backup.sh 2>&1 | logger -t freshorbit-backup
EOF
```

Read the log with `journalctl -t freshorbit-backup --since yesterday`. Set `BACKUP_PING_URL` to a dead-man's-switch
check (e.g. healthchecks.io, free) that alerts when no successful backup arrives within 26 hours.

## 4. Restore

```bash
cd /opt/freshorbit
infra/scripts/restore.sh --list                             # local and offsite backups
infra/scripts/restore.sh 20261024T213000Z                   # database and media from a local backup
infra/scripts/restore.sh remote:20261024T213000Z            # download from offsite first
infra/scripts/restore.sh 20261024T213000Z-pre-v1.4.0 --db-only   # undo a bad migration
```

A live restore checks the checksums, asks you to type the domain (or `--yes`), stops api, worker and web, drops and
recreates the database, restores it, replaces the media volume, then starts the current release again. Downtime is a
few minutes for a small database. If the restored data needs an older release (schema), deploy that tag after the
restore: `infra/scripts/deploy.sh <tag>`.

After a restore: `infra/scripts/logs.sh --status`, open the site and the admin, place a test order on staging.

## 5. Monthly restore drill

A backup is only proven by restoring it. On the first working day of each month (owner: whoever holds the ops role):

1. `infra/scripts/restore.sh --list` and pick last night's **offsite** backup.
2. `infra/scripts/restore.sh --drill remote:<name>`. This downloads it, checks checksums, restores the dump into a
   throwaway Postgres container (no network, live system untouched), prints tables and row counts per schema, and
   lists the media archive. It must end with `Restore drill passed`.
3. Compare the row counts with the live database roughly (orders, customers, products should be close).
4. Every 3 months, do a full drill on a spare VM instead (`move-to-hostinger.md` steps 2–5 with a test domain) and
   time it: that is the real recovery time.
5. Add a row below and commit it.

### Drill log

| Date (UTC) | Backup | Kind | Result | Time taken | By |
|---|---|---|---|---|---|
| 2026-10-24 | local test backup on a developer machine | `--drill` and live restore (scripted test) | Passed: tables and rows restored, media restored, checksum tamper detected | < 1 min | E5 |
