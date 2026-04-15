# End-to-end CLI test scenarios

Shell-based scenarios that exercise the running SV v2 stack. Unlike the
unit/integration suite, these hit real containers and can catch wiring
regressions the mocked harness misses (see #311 for rationale).

## Prerequisites

- A running stack (Podman or `dapr run`-launched) reachable at `http://localhost`.
- `bash`, `curl`, `jq` (the last is only required for `--sweep`).
- `dotnet` (.NET 10 SDK) on PATH for the CLI-driven scenarios. To skip the
  build wait, override `SPRING_CLI` with a path to a prebuilt binary.

## Usage

```
./run.sh                              # all scenarios
./run.sh '03-*'                       # one
./run.sh --sweep                      # orphan cleanup (see below)
E2E_BASE_URL=http://sv:80 ./run.sh    # custom host
SPRING_CLI=/usr/local/bin/spring ./run.sh   # prebuilt CLI
SPRING_API_URL=http://sv:80 ./run.sh        # forwarded to `spring apply`
```

Each scenario exits 0 on pass, non-zero on any failure. The runner aggregates
results and exits non-zero if any scenario failed.

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

### Scenario 04 caveat (serial only)

Scenario `04-create-unit-from-template.sh` creates a unit via
`POST /api/v1/units/from-template`, which derives the unit's `name` from the
template manifest verbatim (`engineering-team`). That name is **not**
parameterised by the run id, so two concurrent runs of scenario 04 collide
on the server's unique-name constraint. Run scenario 04 serially for now
(all other scenarios are concurrent-safe). #325 tracks adding a `name`
override to the from-template endpoint; drop the `@serial` caveat once it
lands.

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

| # | Scenario | Driver | Why |
|---|----------|--------|-----|
| 01 | api-health | curl | Raw contract check; the point is to bypass the CLI/Kiota layer. |
| 02 | create-unit-scratch | CLI (`spring unit create`) | Covered by the CLI today. |
| 03 | create-unit-with-model | curl (TODO #315) | CLI lacks `--model`/`--color` flags. |
| 04 | create-unit-from-template | curl (TODO #316) | CLI has no `--from-template` (and `spring apply` skips the resolver/validator/binding-preview path that this scenario covers). **@serial** — not concurrent-safe (template `name` is fixed). |
| 05 | cli-version-and-help | CLI (`spring --help`) | Sanity-check the CLI starts up before heavier scenarios spend API time. |

## Authentication

The CLI reads its endpoint and token from `~/.spring/config.json` (see
`src/Cvoya.Spring.Cli/CliConfig.cs`). `spring apply` additionally honours
`SPRING_API_URL` as an override. When the API is launched with
`LocalDev=true`, no token is required and the harness can run without
configuring one.

## Adding a scenario

Create `scenarios/NN-short-name.sh`, source `_lib.sh`, use `e2e::cli` (or
`e2e::http` for raw checks), `e2e::expect_status`, `e2e::expect_contains`.
Derive every unit/agent name from `e2e::unit_name <suffix>` or
`e2e::agent_name <suffix>` so `--sweep` can identify orphans, and two
concurrent invocations of `./run.sh` never collide. End with `e2e::summary`.
Keep scenarios idempotent and cleaning up after themselves where possible.

## Tracking

See issue #311 for the full roadmap and future scenario list. CLI gaps
discovered while porting scenarios live under #315 and #316.
