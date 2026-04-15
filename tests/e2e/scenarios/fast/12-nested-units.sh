#!/usr/bin/env bash
# Nested units: create parent + child via CLI, add the child as a member of the
# parent, verify the parent's member list exposes the child. Cascading purge
# on EXIT cleans both up, even if the scenario aborts mid-way.
#
# TODO(#331): `spring unit members add <parent> --unit <child>` does not exist
# yet. The CLI's `members add` only accepts `--agent`, and the PUT /memberships
# endpoint it drives resolves exclusively through Address("agent", ...). The
# scheme-agnostic path is POST /api/v1/units/{id}/members, so we fall back to
# `e2e::http` for the one step the CLI cannot express. Flip to the CLI once
# #331 lands and drop this TODO.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "${HERE}/../../_lib.sh"

parent="$(e2e::unit_name parent)"
child="$(e2e::unit_name child)"

# Cascading teardown covers both units even if any assertion aborts the script.
# Purge is idempotent on the server side, so re-running after a partial failure
# is safe.
trap 'e2e::cleanup_unit "${parent}" "${child}"' EXIT

# --- Setup: create parent and child via CLI -----------------------------------
e2e::log "spring unit create ${parent}"
response="$(e2e::cli --output json unit create "${parent}")"
code="${response##*$'\n'}"
body="${response%$'\n'*}"
e2e::expect_status "0" "${code}" "parent unit create succeeds"
e2e::expect_contains "\"name\": \"${parent}\"" "${body}" "parent create response carries the unit name"

e2e::log "spring unit create ${child}"
response="$(e2e::cli --output json unit create "${child}")"
code="${response##*$'\n'}"
body="${response%$'\n'*}"
e2e::expect_status "0" "${code}" "child unit create succeeds"
e2e::expect_contains "\"name\": \"${child}\"" "${body}" "child create response carries the unit name"

# --- Add child as member of parent (HTTP fallback, TODO #331) -----------------
# POST /api/v1/units/{id}/members takes { memberAddress: { scheme, path } }.
# The server resolves the parent by path (Address("unit", id)) and the member
# by the full { scheme, path } pair, so we can pass scheme="unit" here.
add_body="{\"memberAddress\":{\"scheme\":\"unit\",\"path\":\"${child}\"}}"
e2e::log "POST /api/v1/units/${parent}/members ${add_body}"
response="$(e2e::http POST "/api/v1/units/${parent}/members" "${add_body}")"
status="${response##*$'\n'}"
resp_body="${response%$'\n'*}"
# The endpoint returns 204 No Content on success (see UnitEndpoints.AddMemberAsync).
e2e::expect_status "204" "${status}" "add child as member of parent returns 204"

# --- Verify via GET /api/v1/units/{id} ----------------------------------------
# GetUnitAsync returns UnitDetailResponse { unit, details } where `details` is
# the actor's StatusQuery payload. That payload includes the current member
# list; we assert the child's address appears in the raw JSON rather than
# parsing the full shape (which would couple us to the actor's reply schema).
e2e::log "GET /api/v1/units/${parent}"
response="$(e2e::http GET "/api/v1/units/${parent}")"
status="${response##*$'\n'}"
resp_body="${response%$'\n'*}"
e2e::expect_status "200" "${status}" "get parent unit returns 200"
e2e::expect_contains "${child}" "${resp_body}" "parent detail response mentions the child address"
e2e::expect_contains "\"unit\"" "${resp_body}" "parent detail response carries the unit scheme marker"

e2e::summary
