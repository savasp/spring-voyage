#!/usr/bin/env bash
# Migration-safety concurrent API+Worker startup race (#1388).
#
# Context:
#   PR #305 fixed a race where both the API host and the Worker host called
#   `MigrateAsync` concurrently against the same Postgres database, causing
#   the loser to crash with `42P07: relation "..." already exists`. The fix
#   made the Worker the sole owner of `DatabaseMigrator`; the API host no
#   longer registers it.
#
#   This scenario guards against a regression: if `DatabaseMigrator` is ever
#   accidentally added back to the API host's DI graph, concurrent startup
#   would reproduce the race. It also verifies that the API host starts
#   correctly (schema is visible, EF queries succeed) even though the Worker
#   is responsible for applying migrations.
#
# What this scenario exercises:
#
#   1. Fresh Postgres database with no schema.
#   2. API host and Worker host start concurrently via `dotnet run`.
#   3. Both reach ready state (HTTP 200 on /health) within 90 s.
#   4. Neither's stderr contains a migration-related stack trace
#      (`42P07`, `already exists`, `MigrateAsync`, `DatabaseMigrator`).
#   5. After both are ready, the API host can answer a basic CRUD request
#      (GET /api/v1/connectors → 200 or 404, never 500), proving the schema
#      is available even though the API did not run migrations.
#
# Prerequisites:
#   - `dotnet` (.NET 10 SDK) on PATH.
#   - Postgres reachable at SPRING_DB_HOST:SPRING_DB_PORT (defaults: localhost:5432).
#   - SPRING_DB_USER / SPRING_DB_PASSWORD / SPRING_DB_NAME for a database
#     that is EMPTY at scenario start (a fresh Docker container or a freshly
#     dropped + recreated database).
#
# Environment variables:
#   SPRING_API_PORT    TCP port for the API host (default 5100).
#   SPRING_WORKER_PORT TCP port for the Worker host health endpoint (default 5101).
#   SPRING_DB_HOST     Postgres hostname (default localhost).
#   SPRING_DB_PORT     Postgres port (default 5432).
#   SPRING_DB_USER     Postgres user (default spring).
#   SPRING_DB_PASSWORD Postgres password (default spring).
#   SPRING_DB_NAME     Database name (default spring).
#
# Pool:   infra   (requires a live Postgres; not in the fast pool)
# Opt-in: ./run.sh --infra   or   bash 27-migration-race.sh directly.
#
# References:
#   - #305  — migration race fix (DatabaseMigrator moved to Worker only)
#   - #311  — E2E harness parent issue
#   - #1388 — this scenario's tracking issue
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "${HERE}/../../_lib.sh"

# ─── Configuration ────────────────────────────────────────────────────────────

: "${SPRING_API_PORT:=5100}"
: "${SPRING_WORKER_PORT:=5101}"
: "${SPRING_DB_HOST:=localhost}"
: "${SPRING_DB_PORT:=5432}"
: "${SPRING_DB_USER:=spring}"
: "${SPRING_DB_PASSWORD:=spring}"
: "${SPRING_DB_NAME:=spring}"

CONN="Host=${SPRING_DB_HOST};Port=${SPRING_DB_PORT};Database=${SPRING_DB_NAME};Username=${SPRING_DB_USER};Password=${SPRING_DB_PASSWORD}"
API_URL="http://localhost:${SPRING_API_PORT}"
WORKER_URL="http://localhost:${SPRING_WORKER_PORT}"

# Temporary log files captured in /tmp so they survive the process kill.
API_LOG="$(mktemp /tmp/spring-e2e-api-XXXXXX.log)"
WORKER_LOG="$(mktemp /tmp/spring-e2e-worker-XXXXXX.log)"

API_PID=""
WORKER_PID=""

# ─── Cleanup ─────────────────────────────────────────────────────────────────

cleanup() {
    local rc=$?
    e2e::log "migration-race: cleanup (exit ${rc})"
    if [[ -n "${API_PID}" ]]; then
        kill "${API_PID}" 2>/dev/null || true
        wait "${API_PID}" 2>/dev/null || true
    fi
    if [[ -n "${WORKER_PID}" ]]; then
        kill "${WORKER_PID}" 2>/dev/null || true
        wait "${WORKER_PID}" 2>/dev/null || true
    fi
    rm -f "${API_LOG}" "${WORKER_LOG}"
    return "${rc}"
}
trap cleanup EXIT

# ─── 1: Start API host and Worker host concurrently ──────────────────────────

e2e::log "migration-race: starting API host on port ${SPRING_API_PORT}"

ASPNETCORE_URLS="http://localhost:${SPRING_API_PORT}" \
ConnectionStrings__SpringDb="${CONN}" \
LocalDev=true \
Secrets__AllowEphemeralDevKey=true \
Dapr__Enabled=false \
ASPNETCORE_ENVIRONMENT=Development \
    dotnet run \
        --project "${E2E_REPO_ROOT}/src/Cvoya.Spring.Host.Api/Cvoya.Spring.Host.Api.csproj" \
        --no-build --configuration Release \
        >"${API_LOG}" 2>&1 &
API_PID=$!

e2e::log "migration-race: starting Worker host on port ${SPRING_WORKER_PORT}"

ASPNETCORE_URLS="http://localhost:${SPRING_WORKER_PORT}" \
ConnectionStrings__SpringDb="${CONN}" \
LocalDev=true \
Secrets__AllowEphemeralDevKey=true \
Dapr__Enabled=false \
ASPNETCORE_ENVIRONMENT=Development \
    dotnet run \
        --project "${E2E_REPO_ROOT}/src/Cvoya.Spring.Host.Worker/Cvoya.Spring.Host.Worker.csproj" \
        --no-build --configuration Release \
        >"${WORKER_LOG}" 2>&1 &
WORKER_PID=$!

e2e::log "migration-race: both hosts started (API PID=${API_PID}, Worker PID=${WORKER_PID})"

# ─── 2: Wait for both hosts to reach ready state ─────────────────────────────

api_ready=0
worker_ready=0
deadline=90    # seconds total

e2e::log "migration-race: waiting up to ${deadline}s for both hosts to reach /health"

for i in $(seq 1 "${deadline}"); do
    # Check API.
    if (( api_ready == 0 )); then
        if curl --silent --max-time 2 --fail "${API_URL}/health" >/dev/null 2>&1; then
            api_ready=1
            e2e::log "migration-race: API host ready after ${i}s"
        fi
    fi

    # Check Worker.
    if (( worker_ready == 0 )); then
        if curl --silent --max-time 2 --fail "${WORKER_URL}/health" >/dev/null 2>&1; then
            worker_ready=1
            e2e::log "migration-race: Worker host ready after ${i}s"
        fi
    fi

    # Check that neither process has exited.
    if ! kill -0 "${API_PID}" 2>/dev/null; then
        e2e::fail "API host (PID=${API_PID}) exited before becoming ready"
        e2e::log "API log tail:"
        tail -40 "${API_LOG}" >&2 || true
        e2e::summary
        exit 1
    fi
    if ! kill -0 "${WORKER_PID}" 2>/dev/null; then
        e2e::fail "Worker host (PID=${WORKER_PID}) exited before becoming ready"
        e2e::log "Worker log tail:"
        tail -40 "${WORKER_LOG}" >&2 || true
        e2e::summary
        exit 1
    fi

    (( api_ready == 1 && worker_ready == 1 )) && break
    sleep 1
done

if (( api_ready == 0 )); then
    e2e::fail "API host did not become ready within ${deadline}s"
    e2e::log "API log tail:"
    tail -40 "${API_LOG}" >&2 || true
fi

if (( worker_ready == 0 )); then
    e2e::fail "Worker host did not become ready within ${deadline}s"
    e2e::log "Worker log tail:"
    tail -40 "${WORKER_LOG}" >&2 || true
fi

if (( api_ready == 0 || worker_ready == 0 )); then
    e2e::summary
    exit 1
fi

e2e::ok "both hosts reached ready state (API and Worker /health returned 200)"

# ─── 3: Assert no migration-related stack trace in either host's output ───────
#
# The patterns below are the diagnostic fingerprints of the race condition
# that #305 fixed. If any appear, it means DatabaseMigrator ran in both
# hosts concurrently and the loser crashed (or logged the exception before
# swallowing it). We check both combined stdout+stderr (captured in the logs).

migration_error_patterns=(
    "42P07"                      # Postgres "relation already exists"
    "already exists"             # EF Core migration failure message
    "MigrateAsync.*Exception"    # Stack frame from EF Core
    "DatabaseMigrator.*Exception" # Our own migrator class in a stack trace
)

for pattern in "${migration_error_patterns[@]}"; do
    if grep -qiE "${pattern}" "${API_LOG}" 2>/dev/null; then
        e2e::fail "API host log contains migration-race fingerprint: ${pattern}"
        grep -iE "${pattern}" "${API_LOG}" | head -5 >&2 || true
    else
        e2e::ok "API host log clean: no '${pattern}' pattern"
    fi
    if grep -qiE "${pattern}" "${WORKER_LOG}" 2>/dev/null; then
        e2e::fail "Worker host log contains migration-race fingerprint: ${pattern}"
        grep -iE "${pattern}" "${WORKER_LOG}" | head -5 >&2 || true
    else
        e2e::ok "Worker host log clean: no '${pattern}' pattern"
    fi
done

# ─── 4: API sanity-check — schema is accessible from the API host ─────────────
#
# The API host does not run migrations, but it must be able to query the
# schema that the Worker applied. A GET on /api/v1/connectors is cheap,
# reads from the tenant-scoped connector table, and must return 200 (empty
# list) — not a 500 or connection error.

e2e::log "migration-race: verifying API host can read connector list (schema sanity)"
# shellcheck disable=SC2086
response="$(curl --silent --show-error --max-time 10 \
    -w '\n%{http_code}' \
    "${API_URL}/api/v1/connectors" 2>&1)"
status="${response##*$'\n'}"
body="${response%$'\n'*}"

if [[ "${status}" == "200" ]]; then
    e2e::ok "GET /api/v1/connectors returned 200 — schema is accessible from API host"
else
    e2e::fail "GET /api/v1/connectors returned ${status} (expected 200); body: ${body:0:300}"
fi

e2e::summary
