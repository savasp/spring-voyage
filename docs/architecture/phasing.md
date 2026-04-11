# Phasing

> **[Architecture Index](README.md)** | Related: [Infrastructure](infrastructure.md), [Units & Agents](units.md), [Open Questions](open-questions.md)

---

## Phased Implementation

**Phase 1: Platform Foundation + Software Engineering Domain**

- .NET host with Dapr actors (AgentActor, UnitActor, ConnectorActor, HumanActor) — see [Infrastructure](infrastructure.md)
- `IAddressable` / `IMessageReceiver` (with `Task<Message?>` return) + message routing (flat units) — see [Infrastructure](infrastructure.md), [Messaging](messaging.md)
- `IOrchestrationStrategy` with three implementations: AI-orchestrated, Workflow (container-based), AI+Workflow hybrid — see [Units & Agents](units.md)
- Workflow-as-container model: domain workflows deployed as containers with Dapr sidecars — see [Workflows](workflows.md)
- Platform-internal Dapr Workflows for agent lifecycle and cloning lifecycle
- Partitioned mailbox with conversation suspension — see [Messaging](messaging.md)
- Four-layer prompt assembly (platform, unit context, conversation context, agent instructions) — see [Units & Agents](units.md)
- `checkMessages` platform tool for delegated agent message retrieval
- One connector: GitHub (C#) — see [Connectors](connectors.md)
- Brain/Hands: hosted + delegated execution — see [Units & Agents](units.md)
- User authentication (OAuth via web portal, API token management, tenant admin token controls) — see [Security](security.md)
- Address resolution: cached directory with event-driven invalidation, permission checks at resolution time — see [Messaging](messaging.md)
- Basic API host (with single-tenant local dev mode), CLI (`spring` command) — see [CLI & Web](cli-and-web.md)
- PostgreSQL via Dapr state store + direct EF Core — see [Infrastructure](infrastructure.md)
- Skill format: prompt fragments + optional tool definitions, composable via declaration order — see [Packages](packages.md)
- `software-engineering` domain package (agent templates, unit templates, skills, workflow container) — see [Packages](packages.md)
- **Milestone:** v1 feature parity on new architecture
- **Delivers:** platform foundation with the software engineering domain fully operational

**Phase 2: Observability + Multi-Human**

- Structured activity events via `IObservable<ActivityEvent>` (Rx.NET) — see [Observability](observability.md)
- Streaming from execution environments (TokenDelta, ToolCall events) — see [Messaging](messaging.md)
- Cost tracking per agent/unit/tenant — see [Observability](observability.md)
- Multi-human RBAC (owner, operator, viewer) — see [Security](security.md)
- Agent cloning: `ephemeral-no-memory` and `ephemeral-with-memory` policies, detached and attached modes — see [Units & Agents](units.md)
- Web dashboard v2 — see [CLI & Web](cli-and-web.md)
- **Delivers:** real-time observation of agent work, multi-human participation, elastic agent scaling

**Phase 3: Initiative + Product Management Domain**

- Passive + Attentive initiative levels — see [Initiative](initiative.md)
- Tier 1 screening (small LLM), Tier 2 reflection — see [Initiative](initiative.md)
- Initiative policies, event-triggered cognition
- Cancellation flow (CancellationToken propagation to execution environments) — see [Messaging](messaging.md)
- `product-management` domain package with second connector (Linear, Notion, or Jira) — see [Packages](packages.md), [Connectors](connectors.md)
- **Delivers:** agents take initiative; second domain proves platform generality

**Phase 4: A2A + Additional Strategies**

- A2A protocol support (external agents as unit members, external orchestrators) — see [Workflows](workflows.md)
- Rule-based and Peer orchestration strategies — see [Units & Agents](units.md)
- External workflow engine integration via A2A (ADK, LangGraph as orchestrators) — see [Workflows](workflows.md)
- **Delivers:** full orchestration strategy spectrum, cross-framework interop

**Phase 5: Unit Nesting + Directory + Boundaries**

- Recursive composition (units containing units) — see [Units & Agents](units.md)
- Expertise directory and aggregation — see [Units & Agents](units.md)
- Unit boundary (opacity, projection, filtering, synthesis) — see [Units & Agents](units.md)
- Flat routing with hierarchy-aware permission checks — see [Messaging](messaging.md), [Security](security.md)
- Proactive + Autonomous initiative levels — see [Initiative](initiative.md)
- `persistent` cloning policy (independent clone evolution, recursive cloning) — see [Units & Agents](units.md)
- **Delivers:** organizational structure beyond flat teams, full cloning spectrum

**Phase 6: Platform Maturity**

- Package system (registry, install, versioning, NuGet distribution) — see [Packages](packages.md)
- `research` domain package and additional connectors
- **Delivers:** formal package distribution and additional domain packages

---

## Open Core Model

Spring Voyage follows an open core model. This repository contains the complete, fully functional platform: agents, messaging, routing, orchestration strategies, execution, connectors, CLI, basic RBAC, ephemeral cloning, observability, basic cost tracking, A2A protocol, unit nesting, package system, and dashboard.

Commercial extensions (multi-tenancy, OAuth/SSO/SAML, billing, and advanced features) are developed separately and extend this codebase via dependency injection without modifying it.

### License

Spring Voyage is licensed under the Business Source License 1.1 (BSL 1.1). On the Change Date (2030-04-10), the license converts to the Apache License, Version 2.0.

### Extension Model

The platform is designed for extensibility via DI. All core abstractions are defined as interfaces in `Cvoya.Spring.Core` and implemented in `Cvoya.Spring.Dapr`. External extensions can override default implementations by registering their own services after the default registrations.

The OSS codebase does not reference tenant concepts. Entities have no `TenantId`. Extensions add tenant-scoped wrappers around repositories and services.
