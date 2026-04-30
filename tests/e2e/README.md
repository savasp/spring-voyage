# End-to-end CLI test scenarios

Shell-based scenarios that exercise the running SV v2 stack. Unlike the
unit/integration suite, these hit real containers and can catch wiring
regressions the mocked harness misses (see #311 for rationale).

## Prerequisites

- A running stack (Podman or `dapr run`-launched) reachable at `http://localhost`.
- `bash`, `curl`, `jq` (the last is only required for `--sweep`).
- `dotnet` (.NET 10 SDK) on PATH for the CLI-driven scenarios. To skip the
  build wait, override `SPRING_CLI` with a path to a prebuilt binary.

## Layout

```
tests/e2e/scenarios/
├── fast/         no-LLM scenarios (CRUD, membership, templates, help)
├── llm/          LLM-backed scenarios (empty until #330 lands a local backend)
└── infra/        infra scenarios (concurrent API+Worker startup, migration safety)
```

Scenarios are classified by what they require:
- **fast** — no LLM, no container runtime beyond a running API+Postgres. Default
  for every invocation.
- **llm** — explicit opt-in with `--llm` (or `--all`); requires `LLM_BASE_URL`.
- **infra** — explicit opt-in with `--infra` (or `--all`); requires a live
  Postgres. These scenarios start the API and/or Worker host themselves via
  `dotnet run` and assert startup-level invariants (e.g. migration-safety).

## Usage

```
./run.sh                              # all fast scenarios (default)
./run.sh --llm                        # all llm scenarios (needs LLM_BASE_URL)
./run.sh --infra                      # all infra scenarios (needs live Postgres)
./run.sh --all                        # all pools, fast first
./run.sh '12-*'                       # glob across all pools
./run.sh '27-*'                       # run migration-race scenario specifically
./run.sh --sweep                      # orphan cleanup (see below)
E2E_BASE_URL=http://sv:80 ./run.sh    # custom host
SPRING_CLI=/usr/local/bin/spring ./run.sh   # prebuilt CLI
SPRING_API_URL=http://sv:80 ./run.sh        # forwarded to `spring apply`
```

Each scenario exits 0 on pass, non-zero on any failure. The runner aggregates
results and exits non-zero if any scenario failed. `--llm` without
`LLM_BASE_URL` exits 2 with a pointer to #330.

## Run identity and concurrent invocations

`run.sh` generates one `E2E_RUN_ID` (e.g. `1744560123-54321`, timestamp-pid) at
the top of the batch and exports it so every scenario derives its unit/agent
names from the same id. Combined with the static `E2E_PREFIX` (default `e2e`),
generated names look like `e2e-1744560123-54321-scratch`.

- `E2E_PREFIX` — override the leading segment (CI typically sets
  `E2E_PREFIX=e2e-ci` or `E2E_PREFIX=e2e-dev` so sweeps stay lane-local).
- `E2E_RUN_ID` — override the per-run id. Useful for reproducing a specific
  run's artefacts; leave unset for a fresh id each time.

Two concurrent `./run.sh` invocations (or two back-to-back on a shared
environment) do **not** collide, because each invocation generates its own id.
Scenarios source `_lib.sh` and call `e2e::unit_name <suffix>` or
`e2e::agent_name <suffix>` to derive names, rather than embedding the prefix
inline.

### Scenario 04 (now concurrent-safe)

Scenario `fast/04-create-unit-from-template.sh` used to be `@serial` because
the from-template endpoint derived the unit's `name` from the template's
manifest verbatim (`engineering-team`), and two concurrent runs collided on
the server's unique-name constraint. #325 added an optional `UnitName`
override to `CreateUnitFromTemplateRequest`, and the scenario now passes a
run-scoped id through the CLI's `--name` flag (#316). It is therefore
concurrent-safe alongside the rest of the suite.

## Cascading cleanup

Scenarios set a single `EXIT` trap that calls `e2e::cleanup_unit` (and
optionally `e2e::cleanup_agent`) with every artefact they created. The helper
invokes `spring unit purge --confirm <name>` per unit, which cascades through
every membership row before deleting the unit itself. Failures during cleanup
are logged but swallowed so they can never mask the scenario's real exit
code. Because every artefact carries the run-id prefix, `--sweep` remains
the backstop if a scenario aborts before the trap can fire (e.g. killed -9).

## Orphan cleanup (`--sweep`)

When a scenario aborts mid-way (ctrl-c, assertion failure, network hiccup) it
may leave units or agents behind. `./run.sh --sweep` enumerates every unit
whose name starts with `${E2E_PREFIX}-` via `spring unit list --output json`,
enumerates agents the same way via `spring agent list --output json`, and
deletes them. It prints a summary with the count cleaned up.

```
./run.sh --sweep                       # wipe orphans under the default prefix
E2E_PREFIX=e2e-ci ./run.sh --sweep     # wipe CI orphans only
```

Sweep never runs implicitly — it is an explicit `--sweep`-only invocation —
so a stray sweep cannot delete another concurrent run's in-flight artefacts.

## CLI vs HTTP

Scenarios prefer the `spring` CLI when a viable subcommand exists, because the
CLI exercises three layers raw `curl` skips: the Kiota-generated client (which
breaks if `openapi.json` drifts), CLI argument parsing + output formatting, and
the `ApiTokenAuthHandler` Bearer-token path. Scenarios that have no CLI
counterpart stay on `e2e::http` with a TODO referencing the gap.

| # | Scenario | Pool | Driver | Why |
|---|----------|------|--------|-----|
| 01 | api-health | fast | curl | Raw contract check; the point is to bypass the CLI/Kiota layer. |
| 02 | create-unit-scratch | fast | CLI (`spring unit create`) | Covered by the CLI today. |
| 03 | create-unit-with-model | fast | CLI (`spring unit create --model/--color`) | #315 exposed the flags. |
| 04 | create-unit-from-template | fast | CLI (`spring unit create --from-template`) | #316 exposed the CLI path; #325 added the `UnitName` override that makes this scenario concurrent-safe. |
| 05 | cli-version-and-help | fast | CLI (`spring --help`) | Sanity-check the CLI starts up before heavier scenarios spend API time. |
| 06 | unit-membership-roundtrip | fast | CLI (`spring unit members …`) | Full CLI coverage of #320. |
| 07 | create-start-unit | fast | CLI (`spring unit start / status`) | Lifecycle path through `Running` (or `Starting`). |
| 12 | nested-units | fast | CLI (`spring unit members add --unit`) | #331 added the `--unit` flag so the scenario drops its HTTP fallback. |
| 13 | agent-domain-message | fast | CLI + curl | #404: asserts `MessageReceived` persists after a Domain message to an agent, proving the router → actor → activity-bus path without an LLM. |
| 14 | conversation-lifecycle | fast | CLI + curl | #404: verifies `MessageReceived` → `ThreadStarted` → `StateChanged (Idle→Active)` all fire when a fresh thread kicks off on an idle agent. |
| 15 | unit-policy-roundtrip | fast | CLI + curl | #404: GET empty → PUT (skill + model) → GET round-trip → PUT clear → GET empty; also asserts 404 for an unknown unit. |
| 16 | cost-api-shape | fast | CLI + curl | #404: fresh agent/unit return the full CostSummaryResponse with zero counters and a valid time window; explicit from/to override is honoured. |
| 17 | activity-query-filters | fast | curl | #404: asserts the four server-side filters on `/api/v1/activity` (source, eventType, severity, pageSize) actually narrow results — complements the SSE path until a cross-host event bridge lands. |
| 18 | unit-policy-cli-roundtrip | fast | CLI | #453: per-dimension `spring unit policy <dim> get/set/clear` roundtrip; proves merge doesn't clobber adjacent slots. |
| 19 | unit-humans-cli | fast | CLI | #454: `spring unit humans add/remove/list` lifecycle. |
| 20 | persistent-agent-cli | fast | CLI | #396: exercises the error paths for `spring agent deploy/logs/undeploy/scale` without a container runtime. Happy-path tracked by #1390. |
| 21 | secret-cli | fast | CLI | #432: `spring secret create/list/rotate/versions/prune/get/delete` roundtrip across all three scopes. |
| 22 | validation-exit-codes | fast | CLI | #990 / #311: asserts exit-code table appears in `--help`; parse-level rejection exits non-zero; server-side validation failure maps to the 20..27 range. |
| 23 | bootstrap-and-auth | fast | CLI + curl | #311: `spring auth token create/list/revoke` lifecycle; token usability via Bearer header. |
| 24 | analytics-costs-breakdown | fast | CLI | #554 / E1-A: `spring analytics costs` (scalar total) and `--by-source` / `--breakdown` per-source rollup; `--window` flag accepted; bad window value exits non-zero. |
| 25 | github-app-rotate | fast | CLI | #636 / E1-A: `spring github-app rotate-key --dry-run` (preamble + dry-run exit 0); `--from-file` PEM validation; missing file exits non-zero; `rotate-webhook-secret --dry-run` generates secret without persisting. |
| 26 | exit-code-mapping | fast | CLI | #990 / E1-A: `ApiExceptionRenderer.DetermineExitCode` path — 404 on non-existent unit revalidate exits non-zero; revalidate on Draft unit exits non-zero (20..27 if ProblemDetails carries a code extension, 1 otherwise); E1-A canary asserts `--by-source` in analytics costs help. |
| 27 | migration-race | infra | curl + dotnet run | #1388: concurrent API+Worker startup against a fresh Postgres — asserts both reach ready state, no migration stack trace in either log, and the API can read the schema the Worker applied. |

## Authentication

The CLI reads its endpoint and token from `~/.spring/config.json` (see
`src/Cvoya.Spring.Cli/CliConfig.cs`). `spring apply` additionally honours
`SPRING_API_URL` as an override. When the API is launched with
`LocalDev=true`, no token is required and the harness can run without
configuring one.

## Verifying membership changes

Every scenario that creates, adds, or removes a member MUST cross-verify
the change across BOTH read paths:

1. The CLI `spring unit members list <unit> --output json` (exercises the
   Kiota-generated client and the CLI formatting layer).
2. The HTTP endpoint `GET /api/v1/units/{id}/memberships` (reads the DB
   membership table — the Agents tab's source of truth).

The scenario must assert these two paths AGREE (same count, same agent
addresses). During #340, the two stores drifted: `membersAdded` in the
response was non-zero, the actor's in-memory member list looked populated,
but the DB membership table stayed empty, so `/memberships` and `/agents`
returned `[]` while the CLI's read still reported success via the
actor-state path. Asserting only one side let the regression slip past the
suite. The two-path check catches that class of bug immediately.

Where it matters for membership counts (e.g. template creation, which adds
an exact known number of agents), also cross-check `GET /units/{id}/agents`
so all three read paths (CLI, /memberships, /agents) must agree.

## CI integration

The E2E harness runs via `.github/workflows/e2e-cli.yml` on a **weekly schedule** (Mondays 06:00 UTC) and on **manual trigger** (`workflow_dispatch`). It is intentionally opt-in and not part of the per-PR required-checks gate — standing up Postgres + the API host takes several minutes and would slow every PR merge.

**Gating rationale:** the unit + integration suite in `ci.yml` catches logic regressions quickly (< 2 minutes). The E2E harness catches wiring regressions (actor type-name mismatches, Dapr sidecar misses, serialisation failures) that the mocked suite can't see. Weekly cadence ensures regressions surface before a release window.

The workflow has two jobs:

- **`e2e-fast`** — runs `scenarios/fast/` (default pool; API + Postgres only; no Worker).
- **`e2e-infra`** — runs `scenarios/infra/` on the same weekly schedule; boots both API and
  Worker via `dotnet run` against a fresh Postgres. Covers startup-race and migration-safety
  scenarios that require two concurrent hosts.

To trigger a run manually: Actions → "E2E CLI (scheduled / manual)" → Run workflow.

To run a single scenario in CI: use the `scenario_glob` input (e.g. `22-*`). For infra scenarios run `27-*`; the runner picks the scenario from the infra pool automatically.

## Adding a scenario

Create `scenarios/{fast,llm,infra}/NN-short-name.sh`, source `../../_lib.sh`,
use `e2e::cli` (or `e2e::http` for raw checks), `e2e::expect_status`,
`e2e::expect_contains`. Derive every unit/agent name from `e2e::unit_name
<suffix>` or `e2e::agent_name <suffix>` so `--sweep` can identify orphans and
two concurrent invocations of `./run.sh` never collide. Wire cleanup through
an EXIT trap that calls `e2e::cleanup_unit` with every unit you created. End
with `e2e::summary`.

For **infra scenarios** that start their own hosts: use an EXIT trap to kill the
background processes and temp log files. Do not rely on the stack already being
up — infra scenarios are self-contained startup tests. Assign ports via
`SPRING_API_PORT` / `SPRING_WORKER_PORT` env vars (with sane defaults that
don't collide with the fast pool) so two parallel CI jobs don't fight for the
same port.

## Tracking

See issue #311 for the full roadmap and future scenario list. #404 tracks the
ongoing fast-pool expansion for messaging, conversation lifecycle, policy,
cost, and activity coverage (scenarios 13–17). CLI gaps discovered while
porting scenarios live under #315, #316, and #331. The LLM-backed scenario
pool is tracked by #330; the secrets, SSE-push, and clone-lifecycle items
from #404 are deferred until the supporting plumbing (pass-through secret
encryption defaults, cross-host activity bridging, and workflow-driven
clone liveness) stabilises on main.

Follow-ups filed alongside #311:
- #1388 — migration-safety concurrent API+Worker startup race test (shipped: `infra/27-migration-race.sh`)
- #1389 — multi-tenant isolation scenarios
- #1390 — full persistent-agent deploy/logs/undeploy happy-path (requires container runtime)
- #1391 — full Connector E2E scenarios (bind, webhook, unbind)
