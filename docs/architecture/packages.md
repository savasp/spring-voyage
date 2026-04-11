# Packages

> **[Architecture Index](README.md)** | Related: [Connectors](connectors.md), [Units & Agents](units.md), [Roadmap](../roadmap/README.md)

---

## Domain Packages (Phase 1 Concept)

A **domain package** is a logical grouping of domain-specific content — agent templates, unit templates, skills, workflows, and connector implementations — organized by directory convention. Domain packages are how v2 remains domain-agnostic at the platform level while providing ready-to-use configurations for specific domains.

Each domain package follows a standard directory convention:

- **`agents/`** — Agent definition YAML files (templates for agent personas)
- **`units/`** — Unit definition YAML files (team compositions)
- **`skills/`** — Prompt fragments (`.md`) + optional tool definitions (`.tools.json`)
- **`workflows/`** — Workflow container sources (Dockerfile + project code)
- **`execution/`** — Agent execution environment sources (Dockerfile)
- **`connectors/`** — Connector implementations (compiled into host)

The `software-engineering` package ships with Phase 1. Additional packages (`product-management`, `research`) are planned for later phases.

**Dockerfiles are source; images are runtime.** Packages include Dockerfiles for workflows and agent execution environments — they are the source of truth for how images are built. Agent and unit definitions reference pre-built images at runtime (e.g., `image: spring-workflows/software-dev-cycle:latest`). The `spring build` command bridges the gap:

```bash
# Build all images from a package's Dockerfiles
spring build packages/software-engineering

# Build a specific workflow or execution environment
spring build packages/software-engineering/workflows/software-dev-cycle

# List built images
spring images list
```

For local development, `spring apply` auto-builds if a referenced image doesn't exist locally — it locates the Dockerfile in the package, builds the image, then runs it. Production deployments always use pre-built images from a registry.

In Phase 1, domain packages are simply directories applied with `spring apply -f packages/software-engineering/units/engineering-team.yaml`. Connectors within a domain package are compiled into the host. Workflows and execution environments are deployed as containers (see [Workflows](workflows.md)).

## Skill Format & Composition

A **skill** is a bundle of a prompt fragment and optional tool definitions. Skills are how domain knowledge and domain-specific actions are packaged for reuse.

**Skill files:**

- `{skill-name}.md` — A markdown prompt fragment. Contains domain knowledge, decision criteria, procedures, and behavioral guidance. Injected into Layer 2 (unit context) of the prompt.
- `{skill-name}.tools.json` (optional) — Tool definitions in JSON schema format. Each tool specifies a name, description, and parameter schema. The platform translates tool calls into the appropriate messages and service calls.

**Composition:** When a unit or agent references skills, the skill prompt fragments are concatenated into the prompt in declaration order. The unit's `ai.prompt` is the base; skills append to it. Tool definitions from all referenced skills are merged into the agent's tool manifest.

```yaml
# Unit AI references skills — prompts and tools compose automatically
ai:
  prompt: |                          # base prompt (always included)
    You coordinate a software engineering team.
  skills:                            # appended in declaration order
    - package: spring-voyage/software-engineering
      skill: triage-and-assign       # adds triage prompt + assignToAgent tool
    - package: spring-voyage/software-engineering
      skill: pr-review-cycle         # adds review prompt + requestReview tool
```

**Example skill prompt fragment** (`triage-and-assign.md`):

```markdown
## Triage & Assignment

When you receive a new work item:
1. Classify by type: feature, bug, refactor, documentation
2. Estimate complexity: small (< 1 hour), medium (1-4 hours), large (> 4 hours)
3. Match to the best-fit agent by expertise using `discoverPeers`
4. Assign using `assignToAgent` with a clear description of the work
5. For large items, consider breaking into sub-tasks first
```

**Example tool definition** (`triage-and-assign.tools.json`):

```json
[
  {
    "name": "assignToAgent",
    "description": "Assign a work item to a specific agent in the unit",
    "parameters": {
      "type": "object",
      "required": ["agentId", "description"],
      "properties": {
        "agentId": { "type": "string", "description": "Agent ID or role to assign to" },
        "description": { "type": "string", "description": "Clear description of the work" },
        "conversationId": { "type": "string", "description": "Optional — attach to existing conversation" }
      }
    }
  }
]
```

## Package System (Phase 6)

The formal package system adds distribution and lifecycle management on top of domain packages:

```yaml
package:
  name: spring-voyage/software-engineering
  version: 1.0.0
  
  contents:
    skills:
      - triage-and-assign.md
      - pr-review-cycle.md
    workflows:
      - software-dev-cycle:latest       # container image reference
    agent_templates:
      - backend-engineer.yaml
    unit_templates:
      - engineering-team.yaml
    connectors:
      - github-connector.dll
    topics:
      - github-events.schema.json
```

**Installation:** `spring package install spring-voyage/software-engineering`

**Distribution:** NuGet for .NET code, companion manifest for declarative content. Includes versioning, dependency resolution, and a package registry.
