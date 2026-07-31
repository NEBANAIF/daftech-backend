#!/usr/bin/env bash
#
# restore-database.sh — Restores a DAFTECH CRM Postgres database from a backup
# produced by backup-database.sh. Intended for disaster recovery.
#
# Note: if you're hosting on Neon, prefer Neon's own branch/point-in-time
# restore for most recovery scenarios — this script is for restoring from
# an off-site custom-format pg_dump instead.
#
# Usage:
#   ./restore-database.sh /var/backups/daftech-crm/daftech_crm-20260731-020000.sql.gz
#
# Required environment variables:
#   DB_HOST       Postgres host                    (default: localhost)
#   DB_PORT       Postgres port                     (default: 5432)
#   DB_NAME       Database name                     (default: daftech_crm)
#   DB_USER       Postgres user                     (required)
#   DB_PASSWORD   Postgres password                  (required)
#
# This script is destructive: it overwrites DB_NAME with the contents of the
# backup file. It asks for interactive confirmation unless FORCE=1 is set
# (e.g. for scripted DR drills).
#
# Exit codes: 0 success, 1 missing config, 2 restore failed.

set -euo pipefail

DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-5432}"
DB_NAME="${DB_NAME:-daftech_crm}"
DB_USER="${DB_USER:-}"
DB_PASSWORD="${DB_PASSWORD:-}"

LOG_PREFIX="[restore-database]"
log()  { echo "${LOG_PREFIX} $(date '+%Y-%m-%d %H:%M:%S') - $*"; }
fail() { log "ERROR: $*"; exit "${2:-1}"; }

BACKUP_FILE="${1:-}"
[ -n "$BACKUP_FILE" ] || fail "Usage: $0 <path-to-backup.sql.gz>" 1
[ -f "$BACKUP_FILE" ] || fail "Backup file not found: ${BACKUP_FILE}" 1
[ -n "$DB_USER" ]     || fail "DB_USER is not set" 1
[ -n "$DB_PASSWORD" ] || fail "DB_PASSWORD is not set" 1
command -v pg_restore >/dev/null 2>&1 || fail "pg_restore not found on PATH" 1
command -v gunzip     >/dev/null 2>&1 || fail "gunzip not found on PATH" 1

if [ "${FORCE:-0}" != "1" ]; then
    echo "This will OVERWRITE database '${DB_NAME}' on ${DB_HOST}:${DB_PORT}"
    echo "with the contents of: ${BACKUP_FILE}"
    read -r -p "Type the database name to confirm: " CONFIRM
    [ "$CONFIRM" = "$DB_NAME" ] || fail "Confirmation did not match — aborting" 1
fi

log "Restoring '${DB_NAME}' from ${BACKUP_FILE}"

TMP_DUMP="$(mktemp)"
trap 'rm -f "$TMP_DUMP"' EXIT
gunzip -c "$BACKUP_FILE" > "$TMP_DUMP"

if PGPASSWORD="$DB_PASSWORD" pg_restore \
    --host="$DB_HOST" \
    --port="$DB_PORT" \
    --username="$DB_USER" \
    --dbname="$DB_NAME" \
    --clean --if-exists --no-owner --no-privileges \
    "$TMP_DUMP"; then
    log "Restore completed successfully"
else
    fail "Restore failed — database may be in a partially-restored state" 2
fi

log "Done. Consider running scripts/migration-run.sh to apply any migrations newer than this backup."
exit 0
