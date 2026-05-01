# LLM-dependent e2e scenarios

This directory holds the scenarios that need a real LLM in the loop to assert
their full round-trip — agent turn dispatch, message routing through the
dapr-agent image, agent reply landing in the timeline. Without an LLM they
can only assert the upstream `messageId` (which is allocated synchronously
and tells you nothing about whether dispatch succeeded), so they live here
rather than in `../fast/`.

## How scenarios here differ from `../fast/`

- **Fast scenarios** (`../fast/`) exercise the HTTP/CLI/actor surface without
  an LLM. They run in under ~30 s and are safe for every CI invocation.
  Wire-shape regressions on the dispatcher → agent JSON-RPC transport are
  guarded by `tests/Cvoya.Spring.Integration.Tests/A2ADispatchTransportContractTests`,
  which runs on every PR via the integration suite — that test does **not**
  need an LLM.
- **LLM scenarios** (this directory) send real messages that trigger an
  agent turn. They need `LLM_BASE_URL` (or equivalent) pointing at a backend
  the host can reach. Each scenario calls `e2e::require_ollama` first and
  skips cleanly when Ollama isn't reachable, so the base scenario set stays
  green on hosts without a configured LLM.

## CI lane

The LLM pool runs in `.github/workflows/e2e-cli-llm.yml`:

- **On schedule** — 1st of every month at 06:00 UTC. Catches LLM-side
  regressions even when no contributor remembers to run the lane.
- **On `workflow_dispatch`** — anyone can trigger it from the Actions tab,
  passing a custom Ollama model, scenario glob, or API URL.

The workflow installs Ollama on the runner, pulls a model
(`llama3.2:3b` by default), brings up Postgres + the Spring Voyage API host,
and runs `tests/e2e/run.sh --llm` against the configured glob.

To run locally:

```bash
# Have Ollama up: see docs/developer/local-ai-ollama.md
cd tests/e2e
LLM_BASE_URL=http://localhost:11434 bash run.sh --llm
```

`run.sh --llm` errors out with a clear pointer when the LLM endpoint isn't
reachable.

## Adding a new scenario

Same shape as a fast scenario — source `../../_lib.sh`, derive names with
`e2e::unit_name` / `e2e::agent_name`, use `e2e::cleanup_unit` /
`e2e::cleanup_agent` in an EXIT trap. Call `e2e::require_ollama` at the top
and `exit 0` if it returns non-zero so the scenario skips cleanly when no
LLM is configured. The `--llm` mode globs `*.sh` under this directory; the
`NN-` prefix just orders execution.
