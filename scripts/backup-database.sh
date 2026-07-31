#!/usr/bin/env bash
#
# backup-database.sh — Dumps the DAFTECH CRM Postgres database, compresses it,
# prunes backups older than 7 days, and optionally uploads to S3 or Azure Blob.
#
# Note: if you're hosting on Neon, Neon already keeps point-in-time restore
# and branch-based snapshots server-side — this script is for an extra
# off-site copy, not a replacement for that.
#
# Usage:
#   ./backup-database.sh
#
# Required environment variables:
#   DB_HOST       Postgres host                   (default: localhost)
#   DB_PORT       Postgres port                    (default: 5432)
#   DB_NAME       Database name                    (default: daftech_crm)
#   DB_USER       Postgres user                    (required)
#   DB_PASSWORD   Postgres password                 (required)
#
# Optional environment variables:
#   BACKUP_DIR         Where backups are stored   (default: /var/backups/daftech-crm)
#   RETENTION_DAYS      Days of backups to keep   (default: 7)
#   S3_BUCKET           If set, upload to s3://$S3_BUCKET/ via `aws s3 cp`
#   AZURE_STORAGE_ACCOUNT / AZURE_CONTAINER
#                        If both set, upload via `az storage blob upload`
#
# Exit codes: 0 success, 1 missing config, 2 dump failed, 3 upload failed.

set -euo pipefail

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------
DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-5432}"
DB_NAME="${DB_NAME:-daftech_crm}"
DB_USER="${DB_USER:-}"
DB_PASSWORD="${DB_PASSWORD:-}"

BACKUP_DIR="${BACKUP_DIR:-/var/backups/daftech-crm}"
RETENTION_DAYS="${RETENTION_DAYS:-7}"

TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP_FILE="${BACKUP_DIR}/${DB_NAME}-${TIMESTAMP}.sql.gz"
LOG_PREFIX="[backup-database]"

log()  { echo "${LOG_PREFIX} $(date '+%Y-%m-%d %H:%M:%S') - $*"; }
fail() { log "ERROR: $*"; exit "${2:-1}"; }

# ---------------------------------------------------------------------------
# Pre-flight checks
# ---------------------------------------------------------------------------
[ -n "$DB_USER" ]     || fail "DB_USER is not set" 1
[ -n "$DB_PASSWORD" ] || fail "DB_PASSWORD is not set" 1
command -v pg_dump >/dev/null 2>&1 || fail "pg_dump not found on PATH" 1
command -v gzip     >/dev/null 2>&1 || fail "gzip not found on PATH" 1

mkdir -p "$BACKUP_DIR"

# ---------------------------------------------------------------------------
# Dump + compress
# ---------------------------------------------------------------------------
log "Starting backup of database '${DB_NAME}' on ${DB_HOST}:${DB_PORT}"

# --format=custom (-Fc) enables parallel/selective restore via pg_restore and
# is already compressed, but we still gzip on top for consistency with the
# previous MySQL-based naming/retention scheme below.
if PGPASSWORD="$DB_PASSWORD" pg_dump \
    --host="$DB_HOST" \
    --port="$DB_PORT" \
    --username="$DB_USER" \
    --format=custom \
    --no-owner \
    --no-privileges \
    "$DB_NAME" | gzip -9 > "$BACKUP_FILE"; then
    SIZE=$(du -h "$BACKUP_FILE" | cut -f1)
    log "Backup succeeded: ${BACKUP_FILE} (${SIZE})"
else
    rm -f "$BACKUP_FILE"
    fail "pg_dump failed — no backup file was written" 2
fi

# ---------------------------------------------------------------------------
# Prune backups older than RETENTION_DAYS
# ---------------------------------------------------------------------------
log "Pruning backups older than ${RETENTION_DAYS} days in ${BACKUP_DIR}"
find "$BACKUP_DIR" -name "${DB_NAME}-*.sql.gz" -type f -mtime "+${RETENTION_DAYS}" -print -delete | while read -r removed; do
    log "Removed old backup: ${removed}"
done

# ---------------------------------------------------------------------------
# Optional off-site upload
# ---------------------------------------------------------------------------
if [ -n "${S3_BUCKET:-}" ]; then
    command -v aws >/dev/null 2>&1 || fail "S3_BUCKET is set but aws CLI is not installed" 3
    log "Uploading to s3://${S3_BUCKET}/$(basename "$BACKUP_FILE")"
    aws s3 cp "$BACKUP_FILE" "s3://${S3_BUCKET}/$(basename "$BACKUP_FILE")" \
        || fail "S3 upload failed" 3
    log "S3 upload complete"
fi

if [ -n "${AZURE_STORAGE_ACCOUNT:-}" ] && [ -n "${AZURE_CONTAINER:-}" ]; then
    command -v az >/dev/null 2>&1 || fail "Azure vars are set but az CLI is not installed" 3
    log "Uploading to Azure Blob container '${AZURE_CONTAINER}'"
    az storage blob upload \
        --account-name "$AZURE_STORAGE_ACCOUNT" \
        --container-name "$AZURE_CONTAINER" \
        --name "$(basename "$BACKUP_FILE")" \
        --file "$BACKUP_FILE" \
        --overwrite true \
        || fail "Azure Blob upload failed" 3
    log "Azure upload complete"
fi

log "Backup job finished successfully"
exit 0
