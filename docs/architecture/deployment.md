# Deployment

> **[Architecture Index](README.md)** | Related: [Infrastructure](infrastructure.md), [CLI & Web](cli-and-web.md), [Units & Agents](units.md)

---

## Execution Modes

The agent actor (brain) and execution environment (hands) are separate — see [Units & Agents](units.md) for execution patterns (hosted vs. delegated) and [Messaging](messaging.md) for async dispatch, cancellation, and streaming details. This section covers the isolation modes available for execution environments.


| Mode                  | Isolation   | Startup | Best For                               |
| --------------------- | ----------- | ------- | -------------------------------------- |
| `in-process`          | None        | Instant | LLM-only agents, research, advisory    |
| `container-per-agent` | Full        | Seconds | Software engineering, tool use         |
| `ephemeral`           | Maximum     | Seconds | Untrusted code, compliance             |
| `pool`                | Full (warm) | Instant | Large-scale, mixed workloads           |
| `a2a`                 | External    | Varies  | External agents (ADK, LangGraph, etc.) |


---

## Solution Structure

```
SpringVoyage.sln
├── src/
│   ├── Spring.Core/                    # Domain: interfaces, types, no Dapr dependency
│   │   ├── Messaging/                  # IAddressable, IMessageReceiver, Message, Address
│   │   ├── Orchestration/              # IOrchestrationStrategy, IUnitContext
│   │   └── Observability/              # IActivityObservable, ActivityEvent
│   ├── Spring.Dapr/                    # Dapr implementations of Core interfaces
│   │   ├── Actors/                     # AgentActor, UnitActor, ConnectorActor, HumanActor
│   │   └── Orchestration/              # RuleBasedStrategy, WorkflowStrategy, AiStrategy, etc.
│   ├── Spring.A2A/                     # A2A protocol client + server
│   ├── Spring.Connector.GitHub/        # GitHub connector (C#)
│   ├── Spring.Connector.Slack/         # Slack connector
│   ├── Spring.Host.Api/                # Web API host (authz, multi-tenant, local dev mode)
│   ├── Spring.Host.Worker/             # Headless worker host
│   ├── Spring.Cli/                     # CLI ("spring" command)
│   └── Spring.Web/                     # Web UI
├── python/                             # Optional — for Python-based agents
│   ├── spring_agent/                   # Python agent process (or Dapr Agents-based)
│   └── spring_connectors/              # Python-side connector logic
├── dapr/
│   ├── components/                     # pubsub, state, bindings, secrets, configuration
│   └── configuration/                  # access control, resiliency
├── packages/                           # Domain packages (Dockerfiles, definitions, skills)
│   └── software-engineering/           # Phase 1
│       ├── agents/                     # Agent definition YAML
│       ├── units/                      # Unit definition YAML
│       ├── skills/                     # Prompt fragments + tool definitions
│       ├── workflows/                  # Workflow container sources
│       │   └── software-dev-cycle/
│       │       ├── Dockerfile
│       │       └── SoftwareDevCycle/
│       ├── execution/                  # Agent execution environment sources
│       │   └── spring-agent/
│       │       └── Dockerfile
│       └── connectors/                 # Compiled into host
│           └── github/
└── tests/
```
