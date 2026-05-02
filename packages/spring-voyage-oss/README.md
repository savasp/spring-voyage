# Spring Voyage OSS Dogfooding Package

The built-in template package for spinning up a multi-role unit that develops the Spring Voyage platform on itself. Apply it once and an operator gets a working organisation that can triage issues, ship PRs, review designs, and keep the program plan honest — all backed by the **Spring Voyage** GitHub App.

## What this package ships

- **Top-level unit** (`units/spring-voyage-oss.yaml`) — the org unit. Its members are the four sub-units below; it carries the cross-team orchestration prompt and the policies inherited downstream.
- **Sub-units** (`units/sv-oss-*.yaml`):
  - `sv-oss-software-engineering` — 10 personas (architect, dotnet-engineer, web-engineer, cli-engineer, api-designer, connector-engineer, qa-engineer, devops-engineer, security-engineer, docs-writer). Carries the load-bearing SE-team orchestrator prompt that encodes how the project plans, triages, and reviews.
  - `sv-oss-design` — 1 persona (design-engineer). Visual review, accessibility, mockups.
  - `sv-oss-product-management` — 1 persona (pm). Triage, roadmap, sprint planning against the v0.1 plan-of-record.
  - `sv-oss-program-management` — 1 persona (program-manager). Milestone hygiene, sub-issue / blocked-by wiring, dependency tracking.
- **Agents** (`agents/`) — 13 persona YAMLs ported from `.claude/agents/<role>.md` plus the new `program-manager.yaml`.

Each sub-unit declares a `github` connector binding scaffold; `owner`, `repo`, and `installation_id` are collected at apply time, not checked in.

## Image references

Each sub-unit pins an OSS-flavored agent image:

| Sub-unit | Image |
| --- | --- |
| `sv-oss-software-engineering` | `ghcr.io/cvoya-com/spring-voyage-agent-oss-software-engineering:latest` |
| `sv-oss-design` | `ghcr.io/cvoya-com/spring-voyage-agent-oss-design:latest` |
| `sv-oss-product-management` | `ghcr.io/cvoya-com/spring-voyage-agent-oss-product-management:latest` |
| `sv-oss-program-management` | `ghcr.io/cvoya-com/spring-voyage-agent-oss-program-management:latest` |

The four images `FROM` the omnibus agent base and add per-role tooling. Build them locally with:

```bash
./deployment/build-agent-images.sh --tag dev
```

The release workflow `.github/workflows/release-oss-agent-images.yml` publishes multi-arch images to GHCR on `oss-agents-v*` tag pushes.

## Applying the package

### Wizard

`/units/create` → pick **Spring Voyage OSS** from the template list → fill the GitHub connector step (Owner, Repo, Installation ID) → done. The wizard creates all 5 units atomically and binds the GitHub connector on each sub-unit in the same transaction.

### CLI

```bash
spring unit create-from-template \
  --package spring-voyage-oss \
  --name spring-voyage-oss \
  --connector-owner cvoya-com \
  --connector-repo spring-voyage \
  --connector-installation-id <installation-id>
```

To list available installation IDs against the **Spring Voyage** GitHub App:

```bash
spring github-app list-installations
```

## Post-create

- Confirm each sub-unit has a `github` binding pointing at the **Spring Voyage** App's installation: `spring unit github show <sub-unit>`.
- Send a triage prompt to `sv-oss-program-management` and confirm it returns a milestone + label + sub-issue/blocked-by recommendation.
- Send a triage prompt to `sv-oss-software-engineering` and confirm it routes against scope discipline + the `area:*` label scheme.

## Identity

All GitHub writes from agents in this organisation go through each sub-unit's binding to the **Spring Voyage** GitHub App. No other GitHub identity is referenced anywhere in this package's templates, prompts, or instructions — that is a non-negotiable property of the package.

## Further reading

- `docs/concepts/spring-voyage-oss.md` — the multi-role unit at conceptual level.
- `docs/guide/operator/dogfooding-oss-unit.md` — operator-facing bring-up guide.
- `docs/plan/v0.1/README.md` — the active plan-of-record.
