#!/usr/bin/env bash
# Create a unit with model + color — exercises UnitActor.SetMetadataAsync via
# Dapr actor proxy. This is the flow that surfaces actor-wiring bugs (type-name
# mismatch, missing sidecar, data-contract serialization, placement routing).
#
# TODO(#315): Port to `spring unit create --model ... --color ...` once those
# flags exist on the CLI. The HTTP API already accepts both fields; the CLI
# command in src/Cvoya.Spring.Cli/Commands/UnitCommand.cs only forwards
# --display-name and --description today. Until #315 lands, this scenario
# stays on raw curl so the actor-proxy path keeps coverage.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "${HERE}/../../_lib.sh"

name="$(e2e::unit_name with-model)"

# Cascading teardown — purges the unit whether or not the POST below returned
# an id. `unit purge` resolves by name (address path), which is the same field
# we pass into the create call, so we don't need to parse the response id.
trap 'e2e::cleanup_unit "${name}"' EXIT

body="{\"name\":\"${name}\",\"model\":\"claude-sonnet-4-20250514\",\"color\":\"#6366f1\"}"
e2e::log "POST /api/v1/units ${body}"
response="$(e2e::http POST /api/v1/units "${body}")"
status="${response##*$'\n'}"
resp_body="${response%$'\n'*}"

if [[ "${status}" == "200" || "${status}" == "201" ]]; then
    e2e::ok "unit-with-model creation succeeds (status ${status})"
else
    e2e::fail "unit-with-model creation — expected 200/201, got ${status}: ${resp_body:0:500}"
fi
e2e::expect_contains "\"model\":\"claude-sonnet-4-20250514\"" "${resp_body}" "response carries the model"
e2e::expect_contains "\"color\":\"#6366f1\"" "${resp_body}" "response carries the color"

e2e::summary
