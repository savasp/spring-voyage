# LLM-dependent e2e scenarios

This directory is **intentionally empty**. It is the landing spot for scenarios
that require a running LLM backend — messaging round-trips, policy-at-turn-time,
skill-bundle prompt assembly, etc. Populating it is tracked by #330 (local LLM
backend), which will also wire up either ollama or a deterministic fake server
so `run.sh --llm` has something to point at.

## How scenarios here differ from `../fast/`

- **Fast scenarios** (`../fast/`) exercise the HTTP/CLI/actor surface without
  an LLM. They run in under ~30s and are safe for every CI invocation.
- **LLM scenarios** (this dir) send real messages that trigger an agent turn,
  meaning they need `LLM_BASE_URL` (or equivalent) pointing at a backend the
  host can reach. `run.sh --llm` errors out with a clear pointer to #330 when
  that env var is unset.

## Adding one (later)

Same shape as a fast scenario — source `../../_lib.sh`, derive names with
`e2e::unit_name` / `e2e::agent_name`, use `e2e::cleanup_unit` in an EXIT trap.
The runner's `--llm` mode globs `*.sh` under this directory, so the NN- prefix
just orders execution; there is no registry file to touch.
