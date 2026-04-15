#!/usr/bin/env bash
# Create a unit from the engineering-team template — exercises the skill-
# bundle resolver + validator + connector binding preview path.
#
# #325 added an optional UnitName override to CreateUnitFromTemplateRequest,
# and #316 exposed the endpoint through the CLI. We pass a run-scoped id as
# --name so two concurrent runs no longer collide on the template manifest's
# fixed `name` ("engineering-team"). That makes this scenario safe to run in
# parallel — restore a @serial note here only if UnitName is ever removed.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "${HERE}/../../_lib.sh"

# Run-scoped unit name so repeated / concurrent invocations don't clash on
# the template's fixed manifest name. The cascading purge trap uses this
# value, so the teardown path matches whatever we created here.
template_unit="$(e2e::unit_name from-template)"
trap 'e2e::cleanup_unit "${template_unit}"' EXIT

e2e::log "GET /api/v1/packages/templates (discover templates)"
response="$(e2e::http GET /api/v1/packages/templates)"
status="${response##*$'\n'}"
body="${response%$'\n'*}"
e2e::expect_status 200 "${status}" "templates endpoint returns 200"
e2e::expect_contains 'engineering-team' "${body}" "engineering-team template is listed"

# Exercise the CLI path (#316). The command maps to
# POST /api/v1/units/from-template with UnitName=${template_unit} (#325).
e2e::log "spring unit create --from-template software-engineering/engineering-team --name ${template_unit}"
response="$(e2e::cli --output json unit create --from-template software-engineering/engineering-team --name "${template_unit}")"
code="${response##*$'\n'}"
body="${response%$'\n'*}"
e2e::expect_status "0" "${code}" "from-template CLI create succeeds"
e2e::expect_contains "\"name\": \"${template_unit}\"" "${body}" "created unit carries the run-scoped UnitName override (#325)"

e2e::summary
