# Runbook: move from Oracle to a Hostinger VPS (or any new server)

Decision D25: production moves to a Hostinger VPS **KVM 2** (2 vCPU, 8 GB RAM) when Oracle Always Free is not reliable
enough or the store outgrows it. The same compose file, images (multi-arch) and scripts run there; only the server and
DNS change. The same steps rebuild a lost Oracle VM. Plan for 30–60 minutes of downtime; target a quiet hour.

## 0. A day before

- Lower the TTL of the A records (`@`, `www`, `admin`, `api`) in Cloudflare to **5 minutes** (proxied records switch
  immediately anyway).
- Check that last night's offsite backup exists and the latest restore drill passed (`backup-restore.md`).
- Note the running tag: `cat /opt/freshorbit/.deploy/current-tag` on the old server.
- Tell staff about the maintenance window; pause order intake if the business needs it.

## 1. Buy and prepare the VPS

1. Hostinger → VPS → **KVM 2**, data centre **India** if offered (else the nearest, e.g. Singapore), OS template
   **Ubuntu 24.04** (plain, no control panel), add your SSH public key, set a strong root password (it will be
   disabled for SSH).
2. Hostinger panel → VPS → Firewall (if enabled): allow TCP 22, 80, 443 and UDP 443.
3. Hostinger only gives `root`, so grant the deploy user sudo:

   ```bash
   scp infra/scripts/server-setup.sh root@<new-ip>:/tmp/
   ssh root@<new-ip> "bash /tmp/server-setup.sh --ssh-key '$(cat ~/.ssh/id_ed25519.pub)' --sudo --swap 4G"
   ssh deploy@<new-ip>     # must work before you close the root session
   ```

   KVM 2 has one disk; leave `--docker-data-root` out.

## 2. Configure the new server

From the repository root on your laptop (same tag as production):

```bash
git checkout <current-tag>
tar -czf - infra/compose/docker-compose.prod.yml infra/compose/.env.prod.example infra/caddy infra/scripts \
  infra/monitoring | ssh deploy@<new-ip> 'tar -xzf - -C /opt/freshorbit'
```

Copy the secrets from the old server (or the password manager), server to server through your laptop:

```bash
ssh deploy@<old-ip> 'cat /opt/freshorbit/infra/compose/.env.prod' | \
  ssh deploy@<new-ip> 'umask 077; cat > /opt/freshorbit/infra/compose/.env.prod'
ssh deploy@<old-ip> 'cat /opt/freshorbit/infra/compose/rclone.conf' | \
  ssh deploy@<new-ip> 'umask 077; cat > /opt/freshorbit/infra/compose/rclone.conf'
```

Edit `.env.prod` on the new server if anything differs: `APP_ENVIRONMENT=Production` and `SEED_SAMPLE_DATA=false`
for production, `BACKUP_REMOTE=offsite:production`, `COMPOSE_PROFILES` (monitoring is optional on 8 GB; see the
memory budget at the top of the compose file). Security settings: `security-configuration.md`.

Log in to GHCR (`docker login ghcr.io -u <github-user>`, token with `read:packages`) and pull the images:
`cd /opt/freshorbit && IMAGE_TAG=<tag> docker compose --env-file infra/compose/.env.prod -f infra/compose/docker-compose.prod.yml pull`.

## 3. Cut-over

1. **Old server**, stop writes and take a final backup:

   ```bash
   cd /opt/freshorbit/infra/compose
   IMAGE_TAG=$(cat ../../.deploy/current-tag) docker compose --env-file .env.prod -f docker-compose.prod.yml stop api worker web
   /opt/freshorbit/infra/scripts/backup.sh --label final-move     # uploads to the offsite remote
   ```

2. **New server**, restore it and start the same tag:

   ```bash
   cd /opt/freshorbit
   infra/scripts/restore.sh --list
   infra/scripts/deploy.sh <tag>                                  # creates volumes, runs migrations on an empty DB
   infra/scripts/restore.sh remote:<stamp>-final-move --yes      # replaces DB and media with the final backup
   infra/scripts/deploy.sh <tag>                                  # re-runs migrations (no-op) and health checks
   ```

   (Instead of the offsite copy you can stream the folder directly:
   `ssh deploy@<old-ip> 'tar -C /var/backups/freshorbit -cf - <stamp>-final-move' | ssh deploy@<new-ip> 'tar -C /var/backups/freshorbit -xf -'`
   and restore it by name.)

3. **Smoke test before DNS**, from your laptop, pinning the names to the new IP:

   ```bash
   curl --resolve api.<domain>:443:<new-ip> https://api.<domain>/health
   ```

   Caddy may not have certificates yet (it gets them once DNS points at it); `-k` is fine for this check. For a full
   browser test, add the three names to your laptop's hosts file temporarily.

4. **Switch DNS**: Cloudflare → change the A records to the new IP. Caddy obtains certificates within a minute or two
   (records grey, or proxied with Full (strict) as in `oracle-setup.md` §5).
5. Check: website, admin login, API `/health`, a test order on a staff account, product images load from
   `api.<domain>/media/...`, an OTP SMS arrives, an e-mail arrives.

## 4. After the move

- GitHub → Settings → Environments → `production` (or `staging`): update `DEPLOY_HOST` and `DEPLOY_KNOWN_HOSTS`
  (`ssh-keyscan -t ed25519 <new-ip>`); run the Deploy workflow once with the current tag to prove the pipeline.
- Install the backup cron on the new server (`backup-restore.md` §3); run `restore.sh --drill` on tonight's backup.
- Update external uptime checks and the backup dead-man's switch if they point at the IP.
- Keep the old server **stopped but not deleted** for a week, then delete it (Oracle: terminate instance and the
  block volume after copying anything you still need).
- Raise the DNS TTL back to Auto.
- Record the move in `docs/team/progress.md` and the decision log.

## Rolling back the move

Until the old server is deleted: point DNS back at the old IP and start its services
(`docker compose ... start api worker web`). Any orders placed on the new server after the cut-over exist only
there; export them before switching back, or restore a backup from the new server onto the old one
(`restore.sh remote:<stamp>`).
