# Roadmap

Spring Voyage V2 is developed in six phases. Each phase delivers a complete, usable increment. Later phases build on earlier ones but don't invalidate them.

For full architectural details, see [`docs/architecture/`](architecture/README.md). The [Phasing](architecture/phasing.md) document maps each phase item to its architecture document.

---

## Phase 1: Platform Foundation + Software Engineering Domain

The foundation. Everything else builds on this.

**Status: Complete**

**What ships:**
- [x] .NET host with Dapr actors (AgentActor, UnitActor, ConnectorActor, HumanActor) — [Infrastructure](architecture/infrastructure.md)
- [x] IAddressable / IMessageReceiver + message routing (flat units) — [Infrastructure](architecture/infrastructure.md), [Messaging](architecture/messaging.md)
- [x] AI-orchestrated + Workflow orchestration strategies — [Units & Agents](architecture/units.md)
- [x] Platform-internal Dapr Workflows for agent lifecycle and cloning lifecycle — [Workflows](architecture/workflows.md)
- [x] Partitioned mailbox with conversation suspension — [Messaging](architecture/messaging.md)
- [x] Four-layer prompt assembly (platform, unit context, conversation context, agent instructions) — [Units & Agents](architecture/units.md)
- [x] `checkMessages` platform tool for delegated agent message retrieval — [Units & Agents](architecture/units.md)
- [x] One connector: GitHub (C#) — [Connectors](architecture/connectors.md)
- [x] Brain/Hands: hosted + delegated execution — [Units & Agents](architecture/units.md)
- [x] Address resolution: cached directory with event-driven invalidation, permission checks at resolution time — [Messaging](architecture/messaging.md)
- [x] Basic API host (with single-user local dev mode), CLI (`spring` command) — [CLI & Web](architecture/cli-and-web.md)
- [x] Skill format: prompt fragments + optional tool definitions, composable via declaration order — [Packages](architecture/packages.md)
- [x] `software-engineering` domain package (agent templates, unit templates, skills, workflow container) — [Packages](architecture/packages.md)
- [x] Workflow-as-container deployment with Dapr sidecars — [Workflows](architecture/workflows.md)
- [x] Dapr state store wrapper integration — [Infrastructure](architecture/infrastructure.md)

**Milestone:** v1 feature parity on the new architecture.

---

## Phase 2: Observability + Multi-Human

Real-time visibility into what agents are doing, and support for multiple human participants.

**Status: In progress** (5 remaining items tracked below)

**What ships:**
- [x] Enrich ActivityEvent model + Rx.NET pipeline (models + schema; #1) — [Observability](architecture/observability.md)
- [x] Streaming event types + Dapr pub/sub transport (#2) — [Messaging](architecture/messaging.md)
- [x] Basic cost tracking service + aggregation (schema; #3) — [Observability](architecture/observability.md)
- [x] Multi-human RBAC with unit-scoped permissions (#4) — [Security](architecture/security.md)
- [x] Clone state model + ephemeral lifecycle (model; #5) — [Units & Agents](architecture/units.md)
- [x] Clone API endpoints + cost attribution (model; #6) — [Units & Agents](architecture/units.md)
- [x] Real-time SSE endpoint + activity query API (model; #7) — [Observability](architecture/observability.md)
- [ ] Wire Rx.NET reactive pipeline end-to-end (#44) — [Observability](architecture/observability.md)
- [ ] Implement cost tracking aggregation service + API endpoints (#41) — [Observability](architecture/observability.md)
- [ ] Implement agent cloning lifecycle workflow + clone API (#43) — [Units & Agents](architecture/units.md)
- [ ] Implement SSE activity stream endpoint + activity query API (#42) — [Observability](architecture/observability.md)
- [ ] React/Next.js web dashboard (#8, in progress) — [CLI & Web](architecture/cli-and-web.md)

**Delivers:** Real-time observation of agent work, multi-human participation, elastic agent scaling.

---

## Phase 3: Initiative + Product Management Domain

Agents start taking initiative. A second domain proves the platform is genuinely domain-agnostic.

**Status: Not started**

**What ships:**
- [ ] Passive + Attentive initiative levels — [Initiative](architecture/initiative.md)
- [ ] Tier 1 screening (small LLM), Tier 2 reflection — [Initiative](architecture/initiative.md)
- [ ] Initiative policies, event-triggered cognition — [Initiative](architecture/initiative.md)
- [ ] Cancellation flow (CancellationToken propagation to execution environments) — [Messaging](architecture/messaging.md)
- [ ] `product-management` domain package with second connector (Linear, Notion, or Jira) — [Packages](architecture/packages.md), [Connectors](architecture/connectors.md)

**Delivers:** Agents that take initiative; second domain proves platform generality.

---

## Phase 4: A2A + Additional Strategies

Cross-framework interoperability and the full orchestration strategy spectrum.

**Status: Not started**

**What ships:**
- [ ] A2A protocol support (external agents as unit members, external orchestrators) — [Workflows](architecture/workflows.md)
- [ ] Rule-based and Peer orchestration strategies — [Units & Agents](architecture/units.md)
- [ ] External workflow engine integration via A2A (ADK, LangGraph as orchestrators) — [Workflows](architecture/workflows.md)

**Delivers:** Full orchestration strategy spectrum, cross-framework agent collaboration.

---

## Phase 5: Unit Nesting + Directory + Boundaries

Organizational structure beyond flat teams.

**Status: Not started**

**What ships:**
- [ ] Recursive composition (units containing units) — [Units & Agents](architecture/units.md)
- [ ] Expertise directory and aggregation — [Units & Agents](architecture/units.md)
- [ ] Unit boundary (opacity, projection, filtering, synthesis) — [Units & Agents](architecture/units.md)
- [ ] Flat routing with hierarchy-aware permission checks — [Messaging](architecture/messaging.md), [Security](architecture/security.md)
- [ ] Proactive + Autonomous initiative levels — [Initiative](architecture/initiative.md)
- [ ] `persistent` cloning policy (independent clone evolution, recursive cloning) — [Units & Agents](architecture/units.md)

**Delivers:** Complex organizational structures with hierarchy-aware routing.

---

## Phase 6: Platform Maturity

Production-grade multi-organization platform.

**Status: Not started**

**What ships:**
- [ ] Package system (local registry, install, versioning) — [Packages](architecture/packages.md)
- [ ] `research` domain package and additional connectors — [Connectors](architecture/connectors.md)
**Delivers:** Formal package distribution and additional domain packages.

---

## Future Work (Beyond Phase 6)

The architecture is designed to accommodate these capabilities. Interfaces and extension points are in place. See [Open Questions](architecture/open-questions.md) for details.

**Dynamic Agent and Unit Creation** — Agents and units created programmatically at runtime: workload scaling, specialist spawning, ad-hoc units, emergent structure.

**Advanced Self-Organization** — Agents negotiating task allocation, forming ad-hoc sub-units, and reorganizing unit structure based on workload patterns.

**Alwyse: Cognitive Backbone** — Optional observer agent providing cognitive memory, pattern recognition, expertise evolution, and sub-agent spawning.
