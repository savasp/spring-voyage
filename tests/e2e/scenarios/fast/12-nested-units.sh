#!/usr/bin/env bash
# Nested units: create parent + child via CLI, add the child as a member of
# the parent, verify the parent's member list exposes the child. Cascading
# purge on EXIT cleans both up, even if the scenario aborts mid-way.
#
# #331 landed: `spring unit members add <parent> --unit <child>` targets the
# scheme-agnostic POST /api/v1/units/{id}/members endpoint, so the step that
# previously fell back to `e2e::http` is now fully CLI-driven.
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

# --- Add child as member of parent (CLI, #331) --------------------------------
# `spring unit members add <parent> --unit <child>` POSTs to the scheme-
# agnostic /api/v1/units/{id}/members endpoint with memberAddress={
# scheme: "unit", path: <child> }. Exit code 0 on success; the CLI prints a
# confirmation line we don't inspect here.
e2e::log "spring unit members add ${parent} --unit ${child}"
response="$(e2e::cli unit members add "${parent}" --unit "${child}")"
code="${response##*$'\n'}"
e2e::expect_status "0" "${code}" "add child as member of parent via CLI succeeds"

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
