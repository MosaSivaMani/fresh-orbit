#!/usr/bin/env bash
# One-time hardening and Docker setup for a fresh Ubuntu 24.04 server (ARM64 on Oracle, x86-64 on Hostinger).
# Run as root (or with sudo) over SSH. Safe to run again.
#
#   sudo ./server-setup.sh --ssh-key "ssh-ed25519 AAAA... you@laptop" [options]
#   sudo ./server-setup.sh --ssh-key-file /root/.ssh/authorized_keys [options]
#
# Options:
#   --user NAME              deploy user (default: deploy). Gets the SSH key and the docker group.
#   --sudo                   also give the deploy user passwordless sudo (needed on Hostinger, where root is the
#                            only other login and root SSH is disabled by this script).
#   --swap SIZE              swap file size if none exists (default: 4G; 0 = none)
#   --docker-data-root DIR   keep Docker data (images, volumes incl. Postgres and media) on DIR, e.g. an attached
#                            block volume mounted at /mnt/data (default: Docker's /var/lib/docker)
#   --app-dir DIR            deployment folder (default: /opt/freshorbit)
#   --backup-dir DIR         local backup folder (default: /var/backups/freshorbit)
#   --no-auto-reboot         do not let unattended-upgrades reboot at 22:30 UTC (04:00 IST) when a kernel needs it
#
# What it does: timezone UTC; apt upgrade; deploy user with SSH key only; SSH: no passwords, no root login;
# UFW (22, 80, 443/tcp+udp); fail2ban for SSH; swap; unattended security upgrades; Docker Engine + compose plugin
# from Docker's apt repository with log rotation; on Oracle images, removes the stock iptables REJECT rules that
# would otherwise block 80/443.
set -euo pipefail

DEPLOY_USER=deploy
SSH_KEY=""
SSH_KEY_FILE=""
GRANT_SUDO=false
SWAP_SIZE=4G
DOCKER_DATA_ROOT=""
APP_DIR=/opt/freshorbit
BACKUP_DIR=/var/backups/freshorbit
AUTO_REBOOT=true

log() { printf '\n==> %s\n' "$*"; }
die() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}
usage() {
  sed -n '2,22p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
  exit 2
}

parse_args() {
  while (($#)); do
    case $1 in
      --user)
        DEPLOY_USER=${2:?}
        shift
        ;;
      --ssh-key)
        SSH_KEY=${2:?}
        shift
        ;;
      --ssh-key-file)
        SSH_KEY_FILE=${2:?}
        shift
        ;;
      --sudo) GRANT_SUDO=true ;;
      --swap)
        SWAP_SIZE=${2:?}
        shift
        ;;
      --docker-data-root)
        DOCKER_DATA_ROOT=${2:?}
        shift
        ;;
      --app-dir)
        APP_DIR=${2:?}
        shift
        ;;
      --backup-dir)
        BACKUP_DIR=${2:?}
        shift
        ;;
      --no-auto-reboot) AUTO_REBOOT=false ;;
      -h | --help) usage ;;
      *) die "Unknown option $1 (see --help)." ;;
    esac
    shift
  done
  [[ $DEPLOY_USER =~ ^[a-z_][a-z0-9_-]{0,31}$ ]] || die "Invalid user name '$DEPLOY_USER'."
  [[ -n $SSH_KEY || -n $SSH_KEY_FILE ]] || die "Give --ssh-key or --ssh-key-file; password login will be disabled."
}

check_system() {
  [[ $EUID -eq 0 ]] || die "Run as root (sudo)."
  # shellcheck source=/dev/null
  . /etc/os-release
  [[ ${ID:-} == ubuntu ]] || die "This script supports Ubuntu only (found ${ID:-unknown})."
  [[ ${VERSION_ID:-} == 24.04 ]] || printf 'WARNING: tested on Ubuntu 24.04, found %s.\n' "${VERSION_ID:-?}" >&2
}

base_packages() {
  log "Timezone UTC, packages, upgrades"
  timedatectl set-timezone UTC
  export DEBIAN_FRONTEND=noninteractive
  apt-get update -q
  apt-get -y -q -o Dpkg::Options::=--force-confdef -o Dpkg::Options::=--force-confold upgrade
  apt-get -y -q install ca-certificates curl gnupg ufw fail2ban python3-systemd unattended-upgrades \
    apt-listchanges jq rsync util-linux
}

deploy_user() {
  log "Deploy user '$DEPLOY_USER' (SSH key only)"
  if ! id "$DEPLOY_USER" >/dev/null 2>&1; then
    adduser --disabled-password --gecos "" "$DEPLOY_USER"
  fi
  passwd -l "$DEPLOY_USER" >/dev/null
  local home ssh_dir keys
  home=$(getent passwd "$DEPLOY_USER" | cut -d: -f6)
  ssh_dir=$home/.ssh
  install -d -m 700 -o "$DEPLOY_USER" -g "$DEPLOY_USER" "$ssh_dir"
  keys=$ssh_dir/authorized_keys
  touch "$keys"
  if [[ -n $SSH_KEY_FILE ]]; then
    [[ -s $SSH_KEY_FILE ]] || die "$SSH_KEY_FILE is empty or missing."
    cat "$SSH_KEY_FILE" >>"$keys"
  fi
  [[ -z $SSH_KEY ]] || printf '%s\n' "$SSH_KEY" >>"$keys"
  sort -u -o "$keys" "$keys"
  chown "$DEPLOY_USER:$DEPLOY_USER" "$keys"
  chmod 600 "$keys"
  grep -Eq '^(ssh-(ed25519|rsa)|ecdsa-sha2-|sk-)' "$keys" || die "No valid public key in $keys."

  if [[ $GRANT_SUDO == true ]]; then
    printf '%s ALL=(ALL) NOPASSWD:ALL\n' "$DEPLOY_USER" >"/etc/sudoers.d/90-$DEPLOY_USER"
    chmod 440 "/etc/sudoers.d/90-$DEPLOY_USER"
    visudo -cf "/etc/sudoers.d/90-$DEPLOY_USER" >/dev/null
  fi
}

harden_ssh() {
  log "SSH: keys only, no root login"
  # sshd uses the FIRST value it reads; 10- sorts before cloud-init's 50-cloud-init.conf.
  cat >/etc/ssh/sshd_config.d/10-freshorbit.conf <<'EOF'
PasswordAuthentication no
KbdInteractiveAuthentication no
PermitRootLogin no
PubkeyAuthentication yes
PermitEmptyPasswords no
MaxAuthTries 3
LoginGraceTime 30
X11Forwarding no
AllowAgentForwarding no
ClientAliveInterval 300
ClientAliveCountMax 2
EOF
  sshd -t
  systemctl reload ssh || systemctl restart ssh
}

oracle_iptables() {
  # Oracle's Ubuntu images ship /etc/iptables/rules.v4 ending INPUT and FORWARD with a REJECT rule, loaded by
  # netfilter-persistent. With UFW managing the firewall those rules only get in the way of 80/443.
  if [[ -f /etc/iptables/rules.v4 ]] && grep -q 'REJECT --reject-with icmp-host-prohibited' /etc/iptables/rules.v4; then
    log "Oracle image: removing stock iptables REJECT rules (UFW manages the firewall)"
    cp /etc/iptables/rules.v4 "/etc/iptables/rules.v4.oracle-backup.$(date -u +%Y%m%d%H%M%S)"
    iptables -D INPUT -j REJECT --reject-with icmp-host-prohibited 2>/dev/null || true
    iptables -D FORWARD -j REJECT --reject-with icmp-host-prohibited 2>/dev/null || true
    sed -i '/REJECT --reject-with icmp-host-prohibited/d' /etc/iptables/rules.v4
    systemctl disable netfilter-persistent >/dev/null 2>&1 || true
  fi
}

firewall() {
  log "UFW: allow 22 (rate-limited), 80, 443/tcp, 443/udp"
  ufw default deny incoming
  ufw default allow outgoing
  ufw limit 22/tcp comment 'SSH'
  ufw allow 80/tcp comment 'HTTP (ACME, redirect)'
  ufw allow 443/tcp comment 'HTTPS'
  ufw allow 443/udp comment 'HTTP/3'
  ufw --force enable
  # Note: ports published by Docker bypass UFW. The compose file publishes only 80/443 (and Grafana on 127.0.0.1).
}

fail2ban_setup() {
  log "fail2ban for SSH"
  cat >/etc/fail2ban/jail.d/freshorbit.local <<'EOF'
[DEFAULT]
backend = systemd
bantime = 1h
findtime = 10m
maxretry = 5
bantime.increment = true

[sshd]
enabled = true
EOF
  systemctl enable --now fail2ban
  systemctl restart fail2ban
}

swap_setup() {
  [[ $SWAP_SIZE != 0 ]] || return 0
  if swapon --show --noheadings | grep -q .; then
    log "Swap already present"
  else
    log "Swap file $SWAP_SIZE"
    fallocate -l "$SWAP_SIZE" /swapfile
    chmod 600 /swapfile
    mkswap /swapfile >/dev/null
    swapon /swapfile
    grep -q '^/swapfile ' /etc/fstab || printf '/swapfile none swap sw 0 0\n' >>/etc/fstab
  fi
  printf 'vm.swappiness=10\nvm.vfs_cache_pressure=50\n' >/etc/sysctl.d/90-freshorbit.conf
  sysctl --system >/dev/null
}

unattended_upgrades() {
  log "Unattended security upgrades (auto reboot: $AUTO_REBOOT)"
  cat >/etc/apt/apt.conf.d/20auto-upgrades <<'EOF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
APT::Periodic::AutocleanInterval "7";
EOF
  cat >/etc/apt/apt.conf.d/52freshorbit-unattended <<EOF
Unattended-Upgrade::Automatic-Reboot "$AUTO_REBOOT";
Unattended-Upgrade::Automatic-Reboot-Time "22:30";
Unattended-Upgrade::Remove-Unused-Dependencies "true";
EOF
  systemctl enable --now unattended-upgrades
}

docker_setup() {
  log "Docker Engine and compose plugin"
  if ! command -v docker >/dev/null 2>&1; then
    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
    chmod a+r /etc/apt/keyrings/docker.asc
    # shellcheck source=/dev/null
    . /etc/os-release
    printf 'deb [arch=%s signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu %s stable\n' \
      "$(dpkg --print-architecture)" "${UBUNTU_CODENAME:-$VERSION_CODENAME}" >/etc/apt/sources.list.d/docker.list
    apt-get update -q
    apt-get -y -q install docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
  fi

  install -d -m 0755 /etc/docker
  local data_root_json=""
  if [[ -n $DOCKER_DATA_ROOT ]]; then
    mountpoint -q "$(dirname "$DOCKER_DATA_ROOT")" || mountpoint -q "$DOCKER_DATA_ROOT" ||
      printf 'WARNING: %s is not on a mounted volume; check the block volume mount.\n' "$DOCKER_DATA_ROOT" >&2
    install -d -m 0711 "$DOCKER_DATA_ROOT"
    data_root_json=$(printf ',\n  "data-root": "%s"' "$DOCKER_DATA_ROOT")
  fi
  local daemon_json
  daemon_json=$(
    cat <<EOF
{
  "log-driver": "json-file",
  "log-opts": { "max-size": "10m", "max-file": "5" },
  "live-restore": true${data_root_json}
}
EOF
  )
  if [[ ! -f /etc/docker/daemon.json ]] || [[ $(cat /etc/docker/daemon.json) != "$daemon_json" ]]; then
    printf '%s\n' "$daemon_json" >/etc/docker/daemon.json
    systemctl restart docker
  fi
  systemctl enable --now docker containerd
  usermod -aG docker "$DEPLOY_USER"
}

folders() {
  log "Folders $APP_DIR and $BACKUP_DIR"
  install -d -m 0750 -o "$DEPLOY_USER" -g "$DEPLOY_USER" "$APP_DIR" "$APP_DIR/infra" "$APP_DIR/.deploy"
  install -d -m 0700 -o "$DEPLOY_USER" -g "$DEPLOY_USER" "$BACKUP_DIR"
}

summary() {
  log "Done"
  cat <<EOF
Next steps (docs/runbooks/oracle-setup.md):
  1. From your laptop, in a NEW terminal, check you can log in:  ssh $DEPLOY_USER@<server-ip>
     Keep this root session open until that works.
  2. Copy infra/ to $APP_DIR/infra and create $APP_DIR/infra/compose/.env.prod (chmod 600).
  3. Log in to GHCR once:  docker login ghcr.io -u <github-user>   (token with read:packages)
  4. Install the nightly backup cron (docs/runbooks/backup-restore.md) and deploy:  infra/scripts/deploy.sh <tag>
Docker: $(docker --version)
Compose: $(docker compose version --short)
EOF
}

main() {
  parse_args "$@"
  check_system
  base_packages
  deploy_user
  harden_ssh
  oracle_iptables
  firewall
  fail2ban_setup
  swap_setup
  unattended_upgrades
  docker_setup
  folders
  summary
}

main "$@"
