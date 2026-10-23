#!/usr/bin/env bash
# Restores a Fresh Orbit backup made by backup.sh.
#
#   infra/scripts/restore.sh --list                         list local and offsite backups
#   infra/scripts/restore.sh --drill <backup>               restore into a throwaway Postgres and check it (no impact)
#   infra/scripts/restore.sh <backup> [--db-only|--media-only] [--yes]
#                                                           REPLACE the live database and/or media with the backup
#
# <backup> is a folder name under BACKUP_DIR (e.g. 20261024T213000Z), a full path, or `remote:<name>` to download it
# from BACKUP_REMOTE first. A live restore stops api, worker and web, recreates the database, restores media, then
# starts the current release again. It asks you to type the domain unless --yes is given.
set -euo pipefail
umask 077

# shellcheck source=infra/scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

usage() {
  sed -n '2,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
  exit 2
}

backup_root() { env_value BACKUP_DIR /var/backups/freshorbit; }

rclone() {
  [[ -f $FO_RCLONE_CONF ]] || die "$FO_RCLONE_CONF does not exist."
  docker run --rm \
    -v "$FO_RCLONE_CONF:/config/rclone/rclone.conf:ro" \
    -v "$(backup_root):/backup" \
    "$FO_RCLONE_IMAGE" "$@"
}

list_backups() {
  log "Local backups in $(backup_root):"
  find "$(backup_root)" -mindepth 1 -maxdepth 1 -type d -printf '%f\n' 2>/dev/null | sort || true
  local remote
  remote=$(env_value BACKUP_REMOTE)
  if [[ -n $remote ]]; then
    log "Offsite backups in $remote:"
    rclone lsf "$remote" --dirs-only
  fi
}

# resolve_backup <arg>: prints the local folder, downloading it first for remote:<name>.
resolve_backup() {
  local arg=$1 name remote
  if [[ $arg == remote:* ]]; then
    name=${arg#remote:}
    [[ $name =~ ^[A-Za-z0-9._-]+$ ]] || die "Invalid backup name '$name'."
    remote=$(env_value BACKUP_REMOTE)
    [[ -n $remote ]] || die "BACKUP_REMOTE is empty."
    log "Downloading $remote/$name"
    rclone copy "$remote/$name" "/backup/restore-$name" >&2
    printf '%s' "$(backup_root)/restore-$name"
  elif [[ -d $arg ]]; then
    (cd "$arg" && pwd)
  elif [[ -d $(backup_root)/$arg ]]; then
    printf '%s' "$(backup_root)/$arg"
  else
    die "Backup '$arg' not found (try --list)."
  fi
}

verify_backup() {
  local dir=$1
  [[ -f $dir/SHA256SUMS ]] || die "$dir has no SHA256SUMS."
  (cd "$dir" && sha256sum --check --quiet SHA256SUMS) || die "Checksum mismatch in $dir."
  log "Checksums OK for $dir"
}

drill() {
  local dir=$1 name="fo-restore-drill-$$" tables
  verify_backup "$dir"
  log "Starting throwaway Postgres ($FO_POSTGRES_IMAGE)"
  docker run -d --rm --name "$name" --network none -e POSTGRES_PASSWORD=drill -e POSTGRES_DB=drill \
    "$FO_POSTGRES_IMAGE" >/dev/null
  # shellcheck disable=SC2064 # expand $name now
  trap "docker stop '$name' >/dev/null 2>&1 || true" EXIT
  local deadline=$((SECONDS + 90))
  until docker exec "$name" pg_isready -U postgres -d drill >/dev/null 2>&1; do
    ((SECONDS < deadline)) || die "Drill Postgres did not start."
    sleep 2
  done
  sleep 2
  log "Restoring db.dump"
  docker exec -i "$name" pg_restore -U postgres -d drill --no-owner --no-privileges --exit-on-error <"$dir/db.dump"
  tables=$(docker exec "$name" psql -U postgres -d drill -At -c \
    "select count(*) from pg_tables where schemaname not in ('pg_catalog','information_schema')")
  log "Restored $tables tables. Rows per schema (estimate after ANALYZE):"
  docker exec "$name" psql -U postgres -d drill -q -c "analyze" >/dev/null
  docker exec "$name" psql -U postgres -d drill -c \
    "select schemaname, count(*) as tables, sum(n_live_tup) as rows from pg_stat_user_tables group by 1 order by 1"
  log "Media archive holds $(tar -tzf "$dir/media.tar.gz" | grep -cv '/$' || true) files."
  ((tables > 0)) || die "Drill restored no tables."
  log "Restore drill passed for $dir. Record it in docs/runbooks/backup-restore.md (drill log)."
}

restore_live() {
  local dir=$1 do_db=$2 do_media=$3 assume_yes=$4 domain answer user db
  verify_backup "$dir"
  domain=$(env_value DOMAIN)
  user=$(postgres_user)
  db=$(postgres_db)
  if [[ $assume_yes != true ]]; then
    printf 'This REPLACES the live %s%s%s on %s with %s.\nType the domain to continue: ' \
      "$([[ $do_db == true ]] && printf 'database')" "$([[ $do_db == true && $do_media == true ]] && printf ' and ')" \
      "$([[ $do_media == true ]] && printf 'media')" "$domain" "$dir" >&2
    read -r answer
    [[ $answer == "$domain" ]] || die "Not confirmed."
  fi

  log "Stopping api, worker and web"
  compose stop api worker web

  if [[ $do_db == true ]]; then
    compose up -d postgres
    wait_for_postgres 180
    log "Recreating database $db"
    compose exec -T postgres psql -U "$user" -d postgres -v ON_ERROR_STOP=1 \
      -c "drop database if exists \"$db\" with (force)" -c "create database \"$db\" owner \"$user\""
    log "Restoring db.dump"
    compose exec -T postgres pg_restore -U "$user" -d "$db" --no-owner --exit-on-error <"$dir/db.dump"
  fi

  if [[ $do_media == true ]]; then
    log "Restoring media volume"
    docker run --rm -i --network none -v "${FO_PROJECT}_media:/media" "$FO_BUSYBOX_IMAGE" \
      sh -c "find /media -mindepth 1 -delete && tar -C /media -xzf - && chown -R $FO_APP_UID:$FO_APP_UID /media" \
      <"$dir/media.tar.gz"
  fi

  log "Starting release $(resolve_image_tag)"
  compose up -d --remove-orphans
  log "Restore finished. Check: infra/scripts/logs.sh --status, then the site."
}

main() {
  [[ $# -ge 1 ]] || usage
  require_command docker sha256sum tar
  require_env_file

  case $1 in
    --list)
      list_backups
      return 0
      ;;
    --drill)
      [[ $# -eq 2 ]] || usage
      drill "$(resolve_backup "$2")"
      return 0
      ;;
    -h | --help) usage ;;
  esac

  local source=$1 do_db=true do_media=true assume_yes=false
  shift
  while (($#)); do
    case $1 in
      --db-only) do_media=false ;;
      --media-only) do_db=false ;;
      --yes) assume_yes=true ;;
      *) usage ;;
    esac
    shift
  done
  [[ $do_db == true || $do_media == true ]] || usage
  restore_live "$(resolve_backup "$source")" "$do_db" "$do_media" "$assume_yes"
}

main "$@"
