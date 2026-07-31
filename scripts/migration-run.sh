#!/usr/bin/env bash
#
# migration-run.sh — Applies (or rolls back) EF Core migrations for the
# DAFTECH CRM database using the `dotnet ef` tool.
#
# Usage:
#   ./migration-run.sh                       # apply all pending migrations
#   ./migration-run.sh --to <MigrationName>  # migrate up/down to a specific migration
#   ./migration-run.sh --rollback-last       # roll back the most recently applied migration
#   ./migration-run.sh --list                # list migrations and their applied status
#
# Required environment variables (same as backup-database.sh):
#   DB_HOST, DB_PORT, DB_NAME, DB_USER, DB_PASSWORD
#
# The connection string is passed to `dotnet ef` via ConnectionStrings:Postgres
# so this script never needs to touch appsettings.json.
#
# Exit codes: 0 success, 1 missing config/tooling, 2 migration failed.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_PROJECT="${SCRIPT_DIR}/../src/DaftechCrm.Api"
INFRA_PROJECT="${SCRIPT_DIR}/../src/DaftechCrm.Infrastructure"

DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-5432}"
DB_NAME="${DB_NAME:-daftech_crm}"
DB_USER="${DB_USER:-}"
DB_PASSWORD="${DB_PASSWORD:-}"

LOG_PREFIX="[migration-run]"
log()  { echo "${LOG_PREFIX} $(date '+%Y-%m-%d %H:%M:%S') - $*"; }
fail() { log "ERROR: $*"; exit "${2:-1}"; }

[ -n "$DB_USER" ]     || fail "DB_USER is not set" 1
[ -n "$DB_PASSWORD" ] || fail "DB_PASSWORD is not set" 1
command -v dotnet >/dev/null 2>&1 || fail "dotnet SDK not found on PATH" 1

if ! dotnet ef --version >/dev/null 2>&1; then
    fail "dotnet-ef tool not found. Install with: dotnet tool install --global dotnet-ef" 1
fi

export ConnectionStrings__Postgres="Host=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD};SSL Mode=Require;Trust Server Certificate=true"

EF_ARGS=(--project "$INFRA_PROJECT" --startup-project "$API_PROJECT")

MODE="apply-all"
TARGET=""

while [ $# -gt 0 ]; do
    case "$1" in
        --to)
            MODE="apply-to"
            TARGET="${2:-}"
            [ -n "$TARGET" ] || fail "--to requires a migration name" 1
            shift 2
            ;;
        --rollback-last)
            MODE="rollback-last"
            shift
            ;;
        --list)
            MODE="list"
            shift
            ;;
        *)
            fail "Unknown argument: $1" 1
            ;;
    esac
done

case "$MODE" in
    list)
        log "Listing migrations for ${DB_NAME}"
        dotnet ef migrations list "${EF_ARGS[@]}"
        ;;

    apply-all)
        log "Applying all pending migrations to ${DB_NAME} on ${DB_HOST}:${DB_PORT}"
        if dotnet ef database update "${EF_ARGS[@]}"; then
            log "Migrations applied successfully"
        else
            fail "Migration failed — database may need manual inspection" 2
        fi
        ;;

    apply-to)
        log "Migrating ${DB_NAME} to target migration: ${TARGET}"
        if dotnet ef database update "$TARGET" "${EF_ARGS[@]}"; then
            log "Database is now at migration: ${TARGET}"
        else
            fail "Migration to '${TARGET}' failed" 2
        fi
        ;;

    rollback-last)
        log "Determining current and previous migration for rollback"
        APPLIED=$(dotnet ef migrations list "${EF_ARGS[@]}" --no-build | grep -v "(Pending)" || true)
        PREV=$(echo "$APPLIED" | tail -n 2 | head -n 1 | awk '{print $1}')

        if [ -z "$PREV" ]; then
            log "No earlier migration found — rolling back to an empty database (migration 0)"
            if dotnet ef database update 0 "${EF_ARGS[@]}"; then
                log "Rolled back all migrations"
            else
                fail "Rollback failed" 2
            fi
        else
            log "Rolling back to previous migration: ${PREV}"
            if dotnet ef database update "$PREV" "${EF_ARGS[@]}"; then
                log "Rolled back to: ${PREV}"
            else
                fail "Rollback to '${PREV}' failed" 2
            fi
        fi
        ;;
esac

log "Done"
exit 0
