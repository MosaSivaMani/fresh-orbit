# Runbook: staging server on Oracle Cloud Always Free

Decision D25: staging and soft launch run on an Oracle Cloud Always Free ARM (Ampere A1) VM in India, with the same
Docker Compose + Caddy stack as production. Allow about 2 hours (plus any waiting for capacity).

Related: `deploy.md` (releases), `backup-restore.md`, `move-to-hostinger.md`. Files: `infra/`.

## 1. Account

1. Sign up at https://signup.cloud.oracle.com with the business e-mail. A card is needed for identity checks; Always
   Free resources are not charged.
2. **Home region is permanent** and Always Free compute only runs there. Choose **India West (Mumbai)**
   `ap-mumbai-1` or **India South (Hyderabad)** `ap-hyderabad-1`.
3. Turn on multi-factor authentication for the Oracle login (Identity → your user → MFA).
4. Recommended: upgrade the account to **Pay As You Go**. It stays ₹0 while you stay inside the Always Free limits,
   makes A1 capacity much easier to get, and stops Oracle reclaiming "idle" Always Free instances. Set a budget alert
   (Billing → Budgets, e.g. ₹100) so any accidental paid resource is noticed at once.

Always Free limits to stay inside (check the current list in the Oracle docs): A1 Flex up to 4 OCPU and 24 GB RAM in
total; 200 GB block storage in total (boot volumes included); 10 TB outbound traffic a month.

## 2. Network (VCN)

1. Networking → Virtual Cloud Networks → **Start VCN Wizard** → "Create VCN with Internet Connectivity". Name
   `freshorbit-vcn`, keep the default CIDRs.
2. Open the **public subnet's security list** → Add Ingress Rules (source `0.0.0.0/0` unless noted):

   | Protocol | Port | Source | Why |
   |---|---|---|---|
   | TCP | 22 | your office/home IP `/32` if it is fixed, else `0.0.0.0/0` | SSH (keys only, fail2ban) |
   | TCP | 80 | 0.0.0.0/0 | Let's Encrypt HTTP challenge, redirect to HTTPS |
   | TCP | 443 | 0.0.0.0/0 | HTTPS |
   | UDP | 443 | 0.0.0.0/0 | HTTP/3 (optional) |

   Nothing else: Postgres, Grafana and the containers are never exposed.

## 3. Instance

1. Compute → Instances → **Create instance**, name `freshorbit-staging`.
2. Image: **Canonical Ubuntu 24.04** (the aarch64 build is picked automatically for A1).
3. Shape: **Ampere → VM.Standard.A1.Flex**, **4 OCPU, 24 GB** (or 2 OCPU / 12 GB if capacity is short; resize later).
4. Networking: the VCN and **public subnet** above, "Assign a public IPv4 address".
5. SSH keys: paste **your** public key (`~/.ssh/id_ed25519.pub`; create one with `ssh-keygen -t ed25519`).
6. Boot volume: 50 GB is enough when you add the block volume below (else set ~100 GB here).
7. Create. Note the public IP. Under the instance's VNIC → IPv4 addresses you can make the IP **reserved** so it
   survives a stop/recreate (if your tenancy offers a free reserved IP).

### "Out of capacity" tips

Free A1 capacity in the India regions is often exhausted. In order of effectiveness:

- Upgrade to Pay As You Go (section 1). This fixes it for most people.
- Try another **availability domain / fault domain** in the create dialog (Mumbai and Hyderabad have one AD, but the
  fault domain choice still matters sometimes).
- Ask for a **smaller shape** (1–2 OCPU, 6–12 GB) and resize the instance later (Edit → Shape) when capacity appears.
- Retry at quiet hours (early morning IST, weekends). If you script retries with the OCI CLI
  (`oci compute instance launch ...`), keep at least a few minutes between attempts; hammering the API can get the
  account flagged.

## 4. Block volume (data disk)

Keeps Docker data (Postgres, media, images) off the boot disk so the instance can be rebuilt without losing data.

1. Storage → Block Volumes → Create: `freshorbit-data`, same availability domain, **100 GB**, default performance.
2. Instance → Attached block volumes → Attach: the volume, attachment type **Paravirtualized**, read/write.
3. On the server (`ssh ubuntu@<ip>`):

   ```bash
   lsblk                                   # the new disk is usually /dev/sdb with no partitions
   sudo mkfs.ext4 -L freshorbit-data /dev/sdb
   sudo mkdir -p /mnt/data
   echo 'LABEL=freshorbit-data /mnt/data ext4 defaults,noatime,_netdev,nofail 0 2' | sudo tee -a /etc/fstab
   sudo mount -a && df -h /mnt/data
   ```

   Double-check the device name with `lsblk` before `mkfs`: it erases the disk.

4. Enable volume backups if you like (Block Volume → Backup policies; the free tier includes a few backups), but the
   real backups are the nightly offsite ones (`backup-restore.md`).

## 5. DNS on Cloudflare (free plan)

1. Add the domain to Cloudflare, then change the nameservers at the registrar to the two Cloudflare gives you.
2. DNS records (all pointing at the server IP):

   | Type | Name | Content | Proxy |
   |---|---|---|---|
   | A | `@` | server IP | DNS only (grey) at first |
   | A | `www` | server IP | DNS only at first |
   | A | `admin` | server IP | DNS only at first |
   | A | `api` | server IP | DNS only at first |

   For a separate staging domain, use e.g. `staging.example.in`, `admin.staging.example.in`, `api.staging.example.in`
   and set `DOMAIN=staging.example.in`.
3. Leave the records grey until Caddy has issued certificates (first deploy). Then, optionally, switch them to
   **Proxied** (orange) for Cloudflare's DDoS protection and caching:
   - SSL/TLS → Overview → **Full (strict)**.
   - SSL/TLS → Edge Certificates → keep **"Always Use HTTPS" off** (Caddy redirects itself; this keeps the HTTP
     challenge working for renewals).
   - Set `TRUSTED_PROXIES` in `.env.prod` to Cloudflare's ranges (https://www.cloudflare.com/ips/, space separated,
     plus `private_ranges`) so the API sees real client IPs for rate limits; redeploy.
   - The mobile app talks to `api.<domain>`; Cloudflare's "Bot Fight Mode" can block it, so leave that off.

## 6. Harden the server and install Docker

From your laptop:

```bash
scp infra/scripts/server-setup.sh ubuntu@<ip>:/tmp/
ssh ubuntu@<ip> "sudo bash /tmp/server-setup.sh --ssh-key '$(cat ~/.ssh/id_ed25519.pub)' \
  --docker-data-root /mnt/data/docker --swap 4G"
```

(`$(cat ...)` runs on your laptop and passes your public key text to the server.)

The script: UTC timezone, package upgrades, `deploy` user (SSH key only, docker group), SSH without passwords or
root, UFW (22, 80, 443), fail2ban, 4 GB swap, unattended security upgrades with a reboot window at 22:30 UTC
(04:00 IST), Docker Engine + compose plugin with log rotation, and removal of Oracle's stock iptables REJECT rules.
Oracle's `ubuntu` user keeps working (it has sudo); the `deploy` user is for deployments.

**Before closing the root session**, open a new terminal and check `ssh deploy@<ip>` works.

## 7. First deploy

1. Copy the infra files and create the secrets file (from the repository root on your laptop):

   ```bash
   tar -czf - infra/compose/docker-compose.prod.yml infra/compose/.env.prod.example infra/caddy infra/scripts \
     infra/monitoring | ssh deploy@<ip> 'tar -xzf - -C /opt/freshorbit'
   ssh deploy@<ip>
   cd /opt/freshorbit/infra/compose
   cp .env.prod.example .env.prod && chmod 600 .env.prod
   openssl rand -hex 24   # → POSTGRES_PASSWORD
   openssl rand -hex 32   # → JWT_SIGNING_KEY
   nano .env.prod         # DOMAIN, ACME_EMAIL, secrets, APP_ENVIRONMENT=Staging, SEED_SAMPLE_DATA=true for staging
   ```

   The security settings (SMS DLT credentials `SMS_DLT_*`, and the compose subnet `DOCKER_SUBNET` that the API trusts
   for forwarded headers) are explained in `security-configuration.md`. The API and Worker refuse to start outside
   Development without the SMS values, so get the DLT provider credentials before the first deploy.

   Store every secret in the team password manager as well. Losing `JWT_SIGNING_KEY` only logs users out; losing
   `POSTGRES_PASSWORD` is recoverable; losing the rclone crypt password makes offsite backups unreadable.
2. Offsite backups: create `rclone.conf` next to `.env.prod` (`backup-restore.md` §2) and set `BACKUP_REMOTE`.
3. GitHub (`deploy.md` §1): environment `staging` with secrets `DEPLOY_HOST`, `DEPLOY_USER=deploy`,
   `DEPLOY_SSH_KEY` (a **separate** key pair for CI: `ssh-keygen -t ed25519 -f freshorbit-ci -C ci`, add the `.pub`
   to `/home/deploy/.ssh/authorized_keys`), `DEPLOY_KNOWN_HOSTS` (`ssh-keyscan -t ed25519 <ip>`), and variables
   `DOMAIN`, `PUBLIC_API_BASE_URL`.
4. Deploy: push a tag (`git tag v0.1.0 && git push origin v0.1.0`) or run the **Deploy** workflow manually.
5. Check: `https://<domain>`, `https://admin.<domain>`, `https://api.<domain>/health` → `Healthy`;
   on the server `infra/scripts/logs.sh --status`.
6. Install the nightly backup cron and run the first restore drill (`backup-restore.md` §3, §5).

## 8. Keep it alive

- Oracle may reclaim Always Free instances that look idle for 7 days (very low CPU, network and memory use). Pay As
  You Go accounts are exempt; otherwise the running stack plus nightly backups normally keeps usage above the limits.
- Watch the e-mail for Oracle notices about the tenancy.
- If the instance is lost, follow `move-to-hostinger.md` (the same steps restore onto a new Oracle VM).
