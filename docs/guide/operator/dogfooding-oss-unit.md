# Install and run the Spring Voyage OSS dogfooding unit

> **Audience.** Operators running Spring Voyage OSS who want to install the built-in dogfooding unit on their tenant — a five-unit hierarchy (one parent, four role sub-units) that uses the platform to develop the platform itself.

> **Scope.** How to install and verify the unit. For the conceptual overview — what each sub-unit is responsible for and how they coordinate — see [`docs/concepts/spring-voyage-oss.md`](../../concepts/spring-voyage-oss.md). For the design rationale, see [`docs/decisions/0034-oss-dogfooding-unit.md`](../../decisions/0034-oss-dogfooding-unit.md).

---

## Prerequisites

Before applying the template, confirm all of the following:

- [ ] **Platform is up.** `./deployment/deploy.sh up` has completed without errors. `spring system configuration` reports no mandatory-requirement failures.

- [ ] **OSS agent images are available.** Build them locally with:

  ```bash
  ./deployment/build-agent-images.sh           # builds all eight images at :dev
  ```

  Or pull pre-published images from GHCR (after an `oss-agents-v*` release tag has run):

  ```bash
  podman pull ghcr.io/cvoya-com/spring-voyage-agent-oss-software-engineering:latest
  podman pull ghcr.io/cvoya-com/spring-voyage-agent-oss-design:latest
  podman pull ghcr.io/cvoya-com/spring-voyage-agent-oss-product-management:latest
  podman pull ghcr.io/cvoya-com/spring-voyage-agent-oss-program-management:latest
  ```

  Confirm they are present: `podman images | grep spring-voyage-agent-oss`.

- [ ] **Spring Voyage GitHub App is registered and installed.** The App must be registered with the platform and installed on the target repository (`cvoya-com/spring-voyage` for the canonical dogfooding case).

  Register the App if you haven't already:

  ```bash
  spring github-app register --name "Spring Voyage" --org cvoya-com
  ```

  This opens a browser flow, writes the App credentials to `deployment/spring.env`, and prints an install URL. Visit the install URL and install the App on `cvoya-com/spring-voyage` (or whatever repository the unit will work on) via the GitHub UI.

  After installation, capture the installation ID:

  ```bash
  spring github-app list-installations
  ```

  Note the numeric ID for the repository you will bind the unit to.

- [ ] **Tenant-default LLM provider key is set.** The sub-unit agents need an LLM. Set the tenant default if you haven't already:

  ```bash
  spring secret create --scope tenant anthropic-api-key --value "<sk-ant-...>"
  # or the OpenAI / Google / Ollama equivalent
  ```

  Verify: `spring secret list --scope tenant`.

---

## Install via the New Unit wizard

1. Open the portal and navigate to `/units/create`.
2. Select **Spring Voyage OSS** from the template list.
3. Fill in a unit name (default: `spring-voyage-oss`) and an optional color.
4. Complete the GitHub connector step when it appears: enter `Owner` (`cvoya-com`), `Repo` (`spring-voyage`), and pick the installation from the drop-down (populated by `spring github-app list-installations`).
5. Click **Create**. The wizard creates the parent unit and four sub-units atomically, binding the GitHub connector on each sub-unit in the same transaction.

> If the wizard does not automatically show a connector step for the `spring-voyage-oss` template, you can bind the connector after creation (see the `spring unit github bind` command). A gap here is tracked in [#1543](https://github.com/cvoya-com/spring-voyage/issues/1543).

---

## Install via the CLI

```bash
spring unit create-from-template \
  --package spring-voyage-oss \
  --name spring-voyage-oss \
  --connector-owner cvoya-com \
  --connector-repo spring-voyage \
  --connector-installation-id <installation-id>
```

The `--connector-owner`, `--connector-repo`, and `--connector-installation-id` flags are wired through `ApiClient.BuildGitHubConnectorBinding` (`src/Cvoya.Spring.Cli/ApiClient.cs:417`) and posted as a `UnitConnectorBindingRequest` alongside the template request, so the unit and its connector binding are created in a single atomic transaction (`src/Cvoya.Spring.Cli/ApiClient.cs:356`).

Replace `<installation-id>` with the numeric ID from `spring github-app list-installations`.

---

## What to expect after installation

```bash
spring unit list
```

Should list five units:

| Name | Kind |
| ---- | ---- |
| `spring-voyage-oss` | top-level org unit |
| `sv-oss-software-engineering` | sub-unit |
| `sv-oss-design` | sub-unit |
| `sv-oss-product-management` | sub-unit |
| `sv-oss-program-management` | sub-unit |

The parent's sub-unit panel lists the four children. Each sub-unit has:

- A `github` connector binding pointing at the Spring Voyage App's installation on the target repository.
- `execution.hosting: permanent` so the agent containers stay warm across messages — appropriate for a team that runs continuously rather than per-request.

Verify each sub-unit's connector binding:

```bash
spring unit github show sv-oss-software-engineering
spring unit github show sv-oss-design
spring unit github show sv-oss-product-management
spring unit github show sv-oss-program-management
```

Each should report the bound owner, repo, and installation ID.

---

## Smoke verification

### Program management

Send a triage prompt to the program-management sub-unit:

```bash
spring message send sv-oss-program-management \
  "New issue opened: 'Agent container restarts on every turn even with hosting: permanent set.' Triage this."
```

Expected response: identifies the sub-system (agent runtime / hosting mode), proposes a milestone (`v0.1` or `v0.2`), suggests an issue type (`Bug`), proposes one or more `area:*` labels, and — if this looks like a dependency — suggests a sub-issue or `blocked-by` relationship with an existing issue.

### Software engineering

Send a planning prompt to the software-engineering sub-unit:

```bash
spring message send sv-oss-software-engineering \
  "The unit execution defaults merge doesn't honour the agent's own model field when the unit also sets one. Propose a fix."
```

Expected response: cites scope discipline, references `docs/plan/v0.1/README.md` for area placement, proposes an `area:*` label and issue type, and — because this touches the execution-config merge path — routes to the `dotnet-engineer` or `architect` persona and may suggest an ADR before code.

---

## Troubleshooting

| Symptom | Likely cause | Resolution |
| ------- | ------------ | ---------- |
| `podman images \| grep spring-voyage-agent-oss` shows nothing | OSS images not built or pulled | Run `./deployment/build-agent-images.sh --tag dev` and check the output of each step. |
| `"GitHub App not configured"` in the portal or CLI | App credentials not in `spring.env`, or the App is not installed on the target repository | Run `spring github-app register` and confirm the App is installed on the target repo (`spring github-app list-installations` should show a row for that repo). The API and Worker hosts validate GitHub credentials at startup; check `spring system configuration "GitHub Connector"` for the reported state. |
| HTTP 502 from an agent turn | Tenant-default LLM key is missing or invalid | Confirm the key is set: `spring secret list --scope tenant`. Create it if absent: `spring secret create --scope tenant anthropic-api-key --value "<sk-ant-...>"`. Restart the worker container after setting a new key if the host is already running. |
| Wizard does not show a connector step | Template-declared connector not surfaced automatically | Bind post-creation: `spring unit github bind <sub-unit> --owner cvoya-com --repo spring-voyage --installation-id <id>`. Track via [#1543](https://github.com/cvoya-com/spring-voyage/issues/1543). |
| Sub-unit stays in `Validating` indefinitely | Image pull failed or the OSS image tag is not available locally | Confirm the image is present (`podman images`). If using `:latest` and no release has published it yet, build locally (`./deployment/build-agent-images.sh`) and update the sub-unit's `execution.image` tag to `:dev`. |
| Agent turn silently produces no GitHub output | Connector binding missing on the sub-unit | Run `spring unit github show <sub-unit>`; if it shows no binding, rebind with the command above. |

---

## Where to go next

- [`docs/concepts/spring-voyage-oss.md`](../../concepts/spring-voyage-oss.md) — what the unit is: sub-unit responsibilities, orchestrator prompts, how it dogfoods the platform.
- [`docs/decisions/0034-oss-dogfooding-unit.md`](../../decisions/0034-oss-dogfooding-unit.md) — why this design: role decomposition, FROM-omnibus image strategy, `hosting: permanent`, connector binding at apply time.
- [`packages/spring-voyage-oss/README.md`](../../../packages/spring-voyage-oss/README.md) — template internals: unit and agent YAML layout, connector declaration, and post-apply steps.
- [`docs/guide/operator/byoi-agent-images.md`](byoi-agent-images.md) — conformance contract the four OSS images satisfy (BYOI path 1).
