#!/usr/bin/env bash
# Backs up the Fresh Orbit database and media files, keeps local copies and uploads them offsite.
#
#   infra/scripts/backup.sh                       nightly backup (cron), uploads when BACKUP_REMOTE is set
#   infra/scripts/backup.sh --local-only          no upload (deploy.sh uses it before migrations)
#   infra/scripts/backup.sh --label pre-v1.4.0    suffix for the backup folder name
#
# Output: $BACKUP_DIR/<UTC stamp>[-label]/{db.dump, media.tar.gz, SHA256SUMS, manifest.txt}
#   db.dump       pg_dump custom format (compressed; restore with restore.sh or pg_restore)
#   media.tar.gz  the media volume (product images; D21)
# Offsite: rclone (MIT) in a container, remote defined in infra/compose/rclone.conf (Cloudflare R2 or Backblaze B2;
# see docs/runbooks/backup-restore.md). Settings come from .env.prod: BACKUP_DIR, BACKUP_RETENTION_DAYS,
# BACKUP_REMOTE, BACKUP_REMOTE_RETENTION_DAYS, BACKUP_PING_URL.
set -euo pipefail
umask 077

# shellcheck source=infra/scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

usage() {
  sed -n '2,14p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
  exit 2
}

main() {
  local local_only=false label=""
  while (($#)); do
    case $1 in
      --local-only) local_only=true ;;
      --label)
        [[ $# -ge 2 ]] || usage
        label=$2
        shift
        ;;
      -h | --help) usage ;;
      *) usage ;;
    esac
    shift
  done
  [[ -z $label || $label =~ ^[A-Za-z0-9._-]+$ ]] || die "Invalid label '$label'."

  require_command docker sha256sum
  require_env_file

  local backup_root retention remote remote_retention ping_url
  backup_root=$(env_value BACKUP_DIR /var/backups/freshorbit)
  retention=$(env_value BACKUP_RETENTION_DAYS 7)
  remote=$(env_value BACKUP_REMOTE)
  remote_retention=$(env_value BACKUP_REMOTE_RETENTION_DAYS 30)
  ping_url=$(env_value BACKUP_PING_URL)

  local stamp name dir
  stamp=$(date -u +%Y%m%dT%H%M%SZ)
  name=$stamp${label:+-$label}
  dir=$backup_root/$name
  mkdir -p "$dir"
  trap 'log "Backup failed; removing incomplete $dir"; rm -rf "$dir"' ERR

  log "Dumping database to $dir/db.dump"
  compose exec -T postgres pg_dump -U "$(postgres_user)" -d "$(postgres_db)" --format=custom --compress=6 \
    >"$dir/db.dump"
  # A dump that pg_restore cannot list is useless; fail now rather than at restore time.
  compose exec -T postgres pg_restore --list <"$dir/db.dump" >/dev/null

  log "Archiving media volume to $dir/media.tar.gz"
  docker run --rm --network none -v "${FO_PROJECT}_media:/media:ro" "$FO_BUSYBOX_IMAGE" \
    tar -C /media -czf - . >"$dir/media.tar.gz"

  {
    printf 'created_utc=%s\n' "$stamp"
    printf 'image_tag=%s\n' "$(resolve_image_tag)"
    printf 'host=%s\n' "$(hostname)"
    printf 'postgres=%s\n' "$(compose exec -T postgres postgres --version | tr -d '\r')"
  } >"$dir/manifest.txt"
  (cd "$dir" && sha256sum db.dump media.tar.gz manifest.txt >SHA256SUMS)
  trap - ERR
  log "Local backup complete: $dir ($(du -sh "$dir" | cut -f1))"

  if [[ $local_only == false && -n $remote ]]; then
    [[ -f $FO_RCLONE_CONF ]] || die "BACKUP_REMOTE is set but $FO_RCLONE_CONF does not exist."
    log "Uploading to $remote/$name"
    rclone copy "/backup/$name" "$remote/$name" --immutable
    log "Removing offsite backups older than ${remote_retention} days"
    # The upload already succeeded; a failed clean-up must not fail the backup.
    if ! { rclone delete "$remote" --min-age "${remote_retention}d" && rclone rmdirs "$remote" --leave-root; }; then
      log "WARNING: offsite retention clean-up failed; old backups may remain in $remote."
    fi
  elif [[ $local_only == false ]]; then
    log "WARNING: BACKUP_REMOTE is empty; this backup exists only on this server."
  fi

  log "Removing local backups older than ${retention} days"
  find "$backup_root" -mindepth 1 -maxdepth 1 -type d -mtime "+$retention" -exec rm -rf {} +

  if [[ $local_only == false && -n $ping_url ]]; then
    curl -fsS -m 10 --retry 3 -o /dev/null "$ping_url" || log "WARNING: could not ping BACKUP_PING_URL."
  fi
}

# rclone in a pinned container: the config is mounted read-only, the backup folder read-only at /backup.
rclone() {
  docker run --rm \
    -v "$FO_RCLONE_CONF:/config/rclone/rclone.conf:ro" \
    -v "$(env_value BACKUP_DIR /var/backups/freshorbit):/backup:ro" \
    "$FO_RCLONE_IMAGE" "$@"
}

main "$@"
