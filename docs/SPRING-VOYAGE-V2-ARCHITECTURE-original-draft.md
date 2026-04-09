# Spring Voyage V2 — Architecture Design

**Status:** Draft — living document
**Last updated:** 2026-03-27

---

## Decisions Made

- **Language:** .NET (C#). Python only if AI SDK gaps are discovered.
- **Infrastructure:** Dapr — actors, workflows, pub/sub, state, bindings, secrets, service invocation.
- **Grouping term:** Ensemble (a group of agents performing together).
- **MVP target:** Multi-domain — at least 2 connectors (GitHub + one non-code).
- **Agents are not necessarily workers.** They can be observers, monitors, advisors, learners, etc.
- **Agent initiative is first-class.** Agents can work independently, continuously, take initiative, and make autonomous decisions.
- **Execution model belongs to the ensemble**, not the agent definition.
- **Multiple humans** can interact with the same agent or ensemble, with different permission levels.
- **Workflows are optional** per ensemble, providing structured execution when needed.
- **An ensemble IS an agent.** Both implement the same addressable interface. Ensembles compose recursively — members can be single agents or other ensembles.
- **Alwyse** provides the cognitive backbone for expertise development, learning, adaptation, and initiative.

---

## Terminology

| v1 Term | v2 Term | Description |
|---------|---------|-------------|
| Team | **Ensemble** | A group of agents performing together |
| Team Leader | *(removed)* | Ensembles orchestrate themselves — they ARE agents (see IAddressable) |
| Team Agent | **Agent** | An autonomous AI-powered entity (not necessarily a "worker") |
| GitHub integration | **Connector** | A pluggable adapter to an external system (may leverage Dapr bindings) |
| Work Item | **Task** | A unit of work (one message type among many) |
| — | **Message** | A typed communication between addressable entities |
| — | **Address** | A globally-unique routable identity (namespaced by tenant + ensemble) |
| — | **Topic** | A named pub/sub channel for event distribution |
| — | **Package** | An installable bundle of skills, connectors, workflows, templates, or config |
| — | **Activation** | What causes an agent to wake up and act |
| — | **Workflow** | A durable, structured execution plan for an ensemble or task |
| — | **Directory** | A registry of agent expertise, queryable within and across ensembles |
| — | **Mailbox** | An agent's inbound message queue (Dapr actor turn-based processing) |
| — | **Initiative** | An agent's capacity to autonomously decide to act without external triggers |
| — | **Boundary** | The interface an ensemble exposes when acting as a member of a parent ensemble |
| — | **Tenant** | An isolated organizational unit; contains a root ensemble. Maps to a Dapr namespace. |
| — | **Observer** | An agent (e.g., alwyse) that subscribes to another agent's activity stream with permission |

---

## Why Dapr

Dapr is a distributed application runtime that provides building blocks as a sidecar process. Our .NET application talks to the Dapr sidecar via gRPC/HTTP, and Dapr handles the infrastructure — with pluggable components swappable via YAML configuration (no code changes).

### Building blocks we use

| Dapr Building Block | What it provides for us |
|---------------------|------------------------|
| **Actors** | Virtual actors for agents, ensembles, connectors. Turn-based concurrency (natural mailbox). Reminders and timers for scheduled activation. Automatic activation/deactivation. |
| **Workflows** | Durable orchestration: task chaining, fan-out/fan-in, parallel execution, monitoring patterns. Built on actors. Automatic recovery from failures — resumes from last completed step. |
| **Pub/Sub** | Pluggable pub/sub with 30+ broker backends (Redis, Kafka, RabbitMQ, Azure Event Hubs, etc.). At-least-once delivery. Topic-based routing. Dead letter support. |
| **State Management** | Pluggable state stores (PostgreSQL, Redis, Azure Cosmos, etc.) for agent state, memory, ensemble state. |
| **Bindings** | Input/output connectors to external systems — cron schedules, HTTP webhooks, SMTP, databases, cloud services. These *are* connectors for many common integrations. |
| **Secrets** | Pluggable secret stores (local files, Azure Key Vault, Kubernetes secrets, HashiCorp Vault). |
| **Service Invocation** | Secure service-to-service calls with mTLS, retries, observability. |
| **Configuration** | Dynamic configuration with subscription to changes. |
| **A2A** | Agent-to-agent communication protocol (via `Dapr.AI.A2a` .NET package). |

### Why Dapr over Orleans

Both use the virtual actor model (Microsoft developed both), but Dapr provides significantly more:

- **Pub/Sub** — Orleans Streams are powerful but Orleans-specific. Dapr pub/sub works with any broker and any language.
- **Bindings** — Dapr has 40+ pre-built input/output bindings (cron, HTTP, SMTP, cloud services). These map directly to our "connector" concept. Orleans has nothing equivalent.
- **Workflows** — Dapr Workflows are durable, built on actors, and support the orchestration patterns we need. Orleans requires building this from scratch.
- **Secrets** — Dapr has a dedicated secrets building block with pluggable backends. Orleans doesn't handle secrets.
- **Multi-tenancy** — Dapr namespaces provide tenant isolation for pub/sub consumer groups, state stores, and actor identity out of the box.
- **Language-agnostic sidecar** — if an agent needs to run as a Python process (for AI SDK reasons), it can still use Dapr's building blocks via the sidecar. Orleans is .NET-only.
- **Kubernetes-native** — Dapr is a CNCF project designed for cloud-native deployment.

### Dapr Actor Mapping

Each actor has a **mailbox** — Dapr actors process one message at a time (turn-based concurrency), which naturally models an agent's message queue.

All actors that participate in the agent system implement `IAddressable` — the unifying interface that enables the composite pattern (ensembles as agents).

```
IAddressable (common interface: receive messages, report capabilities)
├── AgentActor       — a single AI-powered entity. Holds state, processes
│                      messages, runs cognition loop (initiative), manages
│                      subscriptions and reminders. Publishes activity
│                      stream for observers.
└── EnsembleActor    — a composite IAddressable: contains other
                       IAddressables (agents or sub-ensembles). The
                       ensemble IS the orchestrator — it has its own AI
                       config (prompt, skills, model) for routing decisions.
                       Manages membership, policies, directory, workflow.
                       When nested in a parent, appears as a single agent.

ConnectorActor      — one per connector instance. Bridges external events
                      to pub/sub topics and translates outbound messages to
                      actions. For simple integrations, a Dapr binding suffices.
HumanActor          — one per human participant. Routes messages to
                      notification channels. Enforces permission level.
```

**No separate ConductorActor.** Since an ensemble IS an agent (IAddressable), it orchestrates itself. The EnsembleActor has optional AI configuration (prompt, skills, model) that it uses to make routing and assignment decisions. If no AI config is provided, it falls back to rule-based routing (round-robin, role-matching, etc.).

**Directory is a property of the ensemble**, not a separate actor type. Each EnsembleActor maintains its own directory internally. The tenant's root ensemble directory aggregates all nested directories.

---

## The Fundamental Abstraction: IAddressable

The most important architectural decision in v2 is that **an ensemble IS an agent**. Both single agents and ensembles implement the same core interface:

```csharp
interface IAddressable
{
    Address Address { get; }
    Task<Message> ReceiveAsync(Message message);
    ExpertiseProfile GetExpertise();
    IReadOnlyList<string> Capabilities { get; }
    IAsyncEnumerable<ActivityEvent> ActivityStream { get; }  // observable output
}
```

From the outside, you cannot tell whether you're talking to a single agent or an ensemble of 100 agents. This is the **Composite pattern** applied to agent systems:

```
IAddressable
├── Agent (leaf) — a single AI-powered entity
└── Ensemble (composite) — contains other IAddressables (agents or ensembles)
```

Since an ensemble IS an agent, it orchestrates itself — no separate "conductor" entity needed. The ensemble has its own AI configuration (prompt, skills, model) for making routing decisions. This eliminates the conductor concept entirely: the ensemble is the conductor.

### Tenant Root Ensemble

Every tenant has an implicit **root ensemble** — the top-level container for all ensembles in the tenant:

```
Tenant: acme (Dapr namespace)
└── Root Ensemble (implicit)
    ├── Engineering Team (ensemble)
    │   ├── Backend Team (ensemble)
    │   │   ├── Ada (agent)
    │   │   ├── Dijkstra (agent)
    │   │   └── Database Specialists (ensemble)
    │   │       ├── Codd (agent)
    │   │       └── Date (agent)
    │   ├── Frontend Team (ensemble)
    │   │   ├── Kay (agent)
    │   │   └── Turing (agent)
    │   └── DevOps Team (ensemble)
    │       └── Hopper (agent)
    ├── Research Team (ensemble)
    │   └── ...
    └── Standalone Agent (agent — not in any sub-ensemble)
```

The root ensemble provides:

- **Tenant-wide directory** — aggregates expertise from all nested ensembles
- **Tenant-wide addressing** — all addresses are relative to the root
- **Cross-ensemble routing** — messages between sibling ensembles flow through common ancestry
- **Tenant policies** — default initiative caps, cost budgets, permission defaults

The root ensemble can be explicitly configured (with AI, policies, connectors) or left implicit (minimal, just provides structure).

### Ensemble Boundary

When an ensemble participates as a member of a parent ensemble, it exposes a **boundary**:

- Inbound messages → the ensemble receives, routes internally using its AI/rules
- Outbound messages → the ensemble aggregates, responds to parent
- Expertise profile → aggregated from all members (directory rolls up)
- Capabilities → union of all members' capabilities

The parent sees the child as a single, capable agent. The child's internal structure is encapsulated.

### Composition Patterns

| Pattern | Structure | Example |
|---------|-----------|---------|
| **Hierarchy** | Tree of ensembles | Engineering org with team leads |
| **Federation** | Peer ensembles sharing directories | Independent teams that can borrow specialists |
| **Swarm** | Flat collection of homogeneous ensembles | N parallel instances processing a workload |
| **Nested specialization** | Ensembles within ensembles, increasing specificity | "Backend" → "Database" → "PostgreSQL optimization" |

---

## Agent Model

An agent definition describes *what* the agent is — not *where* or *how* it runs.

```yaml
agent:
  name: ada
  display_name: Ada
  
  role: backend-engineer                     # intrinsic role — used for multicast addressing
  capabilities: [csharp, python, fastapi, postgresql, testing]
  
  ai:
    backend: claude                          # claude | openai | gemini | local
    model: claude-sonnet-4-20250514
    execution: delegated                     # hosted | delegated
    tool: claude-code                        # tool to launch in execution env (delegated only)
    
  instructions: |
    You are a backend engineer...
    
  expertise:                                 # seeded expertise (evolves via alwyse)
    - domain: python/fastapi
      level: advanced
    - domain: postgresql
      level: intermediate
    
  memory:
    backend: default                         # default | alwyse
    
  activations:                               # what wakes this agent up
    - type: message                          # direct messages to this agent's mailbox
    - type: subscription
      topic: pr-reviews                      # pub/sub topic subscription
      filter: "labels contains 'backend'"    # optional filter
    - type: reminder
      schedule: "0 9 * * MON-FRI"            # cron — Dapr actor reminder
      payload: { action: "daily-standup" }
    - type: binding                          # Dapr input binding
      component: github-webhook
      route: /issues
```

**Key design points:**

- `ai.execution: hosted` — the agent actor calls the AI backend API directly (Pattern A). Good for reasoning-only agents.
- `ai.execution: delegated` — the agent actor dispatches work to the execution environment, which launches a tool (e.g., `claude-code` CLI). The **tool drives the agentic loop** — reading files, writing code, running tests. The actor monitors and collects results (Pattern B). Essential for software engineering.
- No `execution` block on the agent — that's the ensemble's concern (which container mode, which image, etc.)
- `role` is intrinsic to the agent and used for multicast addressing and capability signaling
- `capabilities` are queryable tags for routing and discovery
- `expertise` is seeded statically and evolves through alwyse
- `activations` define triggers — messages, pub/sub, reminders, and Dapr bindings
- An agent definition is portable: the same agent can participate in different ensembles
- The agent has a **mailbox** (Dapr actor turn-based processing) — messages are queued and processed in order

### What does `role` mean?

Role serves two purposes:

1. **Multicast addressing** — `role://engineering-team/backend-engineer` routes a message to all agents with that role in that ensemble
2. **Capability signal** — other agents can reason about who to delegate to based on role

Role is *not* a structural position in the ensemble (that's defined by the ensemble's structure). It's an intrinsic property of the agent, like a job title.

---

## Agent Initiative

Initiative is the agent's capacity to **autonomously decide to act** — not just respond to triggers, but originate actions. This is what makes agents genuinely autonomous rather than sophisticated event handlers.

### Initiative Levels

Each agent has a configurable initiative level that controls how much autonomy it exercises:

| Level | Behavior | Example |
|-------|----------|---------|
| **Passive** | Only acts when explicitly activated (message, workflow step) | A code formatter that runs when invoked |
| **Attentive** | Monitors topics/events and decides whether to act on each | A security scanner that watches commits and flags issues |
| **Proactive** | Periodically reflects on state and may initiate new actions | An agent that notices untested code and creates test tasks |
| **Autonomous** | Continuously runs, sets its own goals, manages its own workload | A research agent that independently tracks a field and produces reports |

### The Cognition Loop

Initiative is powered by a **background cognition loop** — a periodic process where the agent reflects on its state, knowledge, and context, and decides whether to act:

```
┌──────────────────────────────────────────────┐
│              Agent Cognition Loop             │
│                                              │
│  1. Perceive — What has changed since I last │
│     thought? (new messages, events, time)    │
│                                              │
│  2. Reflect — Given my expertise, goals, and │
│     current context, is there something I    │
│     should do? (powered by alwyse)           │
│                                              │
│  3. Decide — What action, if any?            │
│     • Send a message to another agent        │
│     • Start a new task                       │
│     • Query the expertise directory          │
│     • Raise an alert to a human              │
│     • Update my own knowledge                │
│     • Do nothing (most common outcome)       │
│                                              │
│  4. Act — Execute the decided action         │
│                                              │
│  5. Learn — Record the outcome (via alwyse)  │
│                                              │
│  [loop frequency based on initiative level]  │
└──────────────────────────────────────────────┘
```

### Implementation via Dapr

- **Passive agents** — no background loop; only activated by direct calls
- **Attentive agents** — pub/sub subscriptions with filter logic in the handler (the agent's LLM decides whether to act)
- **Proactive agents** — Dapr actor **reminder** runs the cognition loop periodically (e.g., every 5 minutes)
- **Autonomous agents** — Dapr actor **reminder** at higher frequency + persistent goals state + alwyse-driven planning

### Initiative Policies (Ensemble-level)

The ensemble can constrain agent initiative:

```yaml
ensemble:
  policies:
    initiative:
      max_level: proactive              # cap initiative for all agents
      require_ensemble_approval: false  # proactive actions need ensemble OK?
      budget:
        max_actions_per_hour: 10        # rate-limit autonomous actions
        max_cost_per_day: $5.00         # cap LLM spend from initiative
      allowed_actions:                  # whitelist of initiative action types
        - create-task
        - send-message
        - query-directory
      blocked_actions:                  # blacklist
        - modify-connector-config
        - spawn-agent
```

This prevents runaway autonomy while enabling valuable proactive behavior. A new ensemble might start with `max_level: attentive` and increase as trust builds — mirroring how human teams earn autonomy.

### Initiative and Alwyse

Initiative is where alwyse integration matters most. The cognition loop's "Reflect" and "Decide" steps are powered by alwyse's cognitive model:

- **Pattern recognition** — alwyse notices recurring issues ("this type of PR always fails review")
- **Opportunity detection** — alwyse identifies gaps ("no one has updated the API docs in 3 weeks")
- **Risk assessment** — alwyse evaluates whether acting is worth the risk ("this change looks risky, I should flag it")
- **Learning from initiative outcomes** — alwyse tracks which proactive actions were valuable and which were noise, refining the agent's judgment over time

Without alwyse, initiative is simple rule-based triggering. With alwyse, initiative becomes genuine judgment.

---

## Ensemble Model

The ensemble defines *how* agents run together — including execution, structure, and optionally a workflow.

```yaml
ensemble:
  name: engineering-team
  description: Software engineering team for the spring-voyage repo
  
  # --- Structure ---
  structure: hierarchical       # hierarchical | peer | custom
  
  # --- Ensemble AI (the ensemble orchestrates itself) ---
  ai:
    backend: claude
    model: claude-sonnet-4-20250514
    prompt: |
      You coordinate a software engineering team. You triage
      incoming work, assign it to the best-fit agent, monitor
      progress, and intervene when tasks are stuck.
    skills:
      - package: spring-voyage/software-engineering
        skill: triage-and-assign
      - package: spring-voyage/software-engineering
        skill: pr-review-cycle
  
  # --- Lead (optional — a member with elevated permissions) ---
  lead: tech-lead               # optional designated lead agent
  
  members:
    - agent: ada                # a single agent
    - agent: kay
    - agent: hopper
    - agent: tech-lead          # the lead is just another member
    - ensemble: database-team   # a sub-ensemble (recursive composition!)
  
  # --- Execution (ensemble-level, not agent-level) ---
  execution:
    mode: container-per-agent   # in-process | shared-container | container-per-agent
                                # | ephemeral | pool | serverless
    runtime: podman             # podman | docker | kubernetes
    image: spring-agent:latest  # container image with tools (Claude CLI, git, etc.)
    pool:                       # pool-mode specific config
      size: 10                  # number of containers in the pool
      max_idle: 5m              # release container after idle period
    per_agent:
      ada: { resources: { memory: 4Gi } }
  
  # --- Optional Workflow ---
  workflow: software-dev-cycle   # reference to a named workflow (see Workflow section)
  
  # --- Connectors ---
  connectors:
    - type: github
      config:
        repo: savasp/spring
        webhook_secret: ${GITHUB_WEBHOOK_SECRET}
    - type: slack
      config:
        channel: "#engineering-team"
  
  # --- Packages ---
  packages:
    - spring-voyage/software-engineering
    - spring-voyage/github-conventions
  
  # --- Policies ---
  policies:
    communication: hybrid       # through-ensemble | peer-to-peer | hybrid
    work_assignment: ensemble-assigns  # ensemble-assigns | self-select | round-robin | capability-match
    expertise_sharing: advertise  # advertise | private
    initiative:
      max_level: proactive
      max_actions_per_hour: 20
      require_ensemble_approval: false
    
  # --- Goals (injected into ensemble AI prompt context) ---
  goals:
    - Implement features from GitHub issues with high code quality
    - Maintain >80% test coverage
    - Respond to PR feedback within 1 hour
    
  # --- Humans (with permission levels) ---
  humans:
    - identity: savasp
      permission: owner
      notifications: [slack, email]
    - identity: reviewer2
      permission: operator
      notifications: [github]
    - identity: stakeholder1
      permission: viewer
      notifications: [email]
```

### How are "goals" used?

Goals are **injected into the ensemble's AI prompt** as strategic context. The ensemble (when AI-powered) interprets goals when making decisions about task prioritization, agent assignment, and direction. If a workflow is attached, goals inform the workflow's decision points.

---

## Ensemble Workflows

An ensemble may optionally be associated with a **workflow** — a durable, structured execution plan built on **Dapr Workflows**.

### Why workflows?

Not every ensemble needs a workflow. A peer-based brainstorming ensemble might just have agents responding to messages freely. But a software engineering ensemble benefits from structured execution: triage → plan → implement → review → merge. A workflow makes this deterministic and recoverable.

### Workflow patterns (Dapr Workflows + Google ADK-inspired)

| Pattern | Description | Example |
|---------|-------------|---------|
| **Sequential** | Steps execute one after another | triage → assign → implement → review |
| **Parallel** | Multiple steps execute concurrently | run tests + run linter + run security scan |
| **Fan-out/Fan-in** | Distribute work to N agents, aggregate results | assign subtasks to 3 agents, collect PRs |
| **Conditional** | Branch based on state or agent output | if complexity > threshold → request human review |
| **Loop** | Repeat until condition met | retry review cycle until approved |
| **Human-in-the-loop** | Pause workflow, wait for human input | wait for plan approval before implementing |
| **Sub-workflow** | Delegate to a nested workflow | "implement feature" is itself a multi-step workflow |

### Workflow definition

```yaml
workflow:
  name: software-dev-cycle
  description: Standard software development lifecycle
  
  steps:
    - name: triage
      type: activity
      actor: ensemble                    # the ensemble itself handles this
      action: classify-and-prioritize
      
    - name: assign
      type: activity
      actor: ensemble
      action: select-agent-by-expertise
      output: assigned_agent
      
    - name: plan
      type: activity
      actor: ${assigned_agent}
      action: create-implementation-plan
      output: plan
      
    - name: approve-plan
      type: human-approval
      prompt_to: team-lead
      input: ${plan}
      timeout: 24h
      on_timeout: auto-approve
      
    - name: implement
      type: activity
      actor: ${assigned_agent}
      action: implement-plan
      output: pr_url
      
    - name: review
      type: fan-out
      actors: [role://backend-engineer]
      action: review-pr
      input: ${pr_url}
      aggregate: all-approved
      
    - name: merge
      type: activity
      actor: ${assigned_agent}
      action: merge-pr
      condition: ${review.all-approved}
```

### Relationship between ensemble AI and workflow

- **AI-powered ensemble without workflow:** The ensemble uses its prompt + skills to orchestrate freely. It decides what to do based on messages, goals, and context. Flexible, LLM-driven.
- **Workflow without ensemble AI:** The workflow drives execution mechanically. Steps are deterministic. Agents are invoked at specific points. No LLM reasoning.
- **AI + workflow (recommended):** The workflow provides the skeleton; the ensemble's AI fills in the decisions. The workflow defines the *phases* (triage → plan → implement → review); the ensemble decides *who* does each phase and *how* using its LLM reasoning. Structured enough to be reliable, flexible enough to handle novel situations.

---

## Activation Model

Agents can be activated by multiple trigger types, all mapped to Dapr primitives:

| Trigger | Dapr Primitive | Description |
|---------|---------------|-------------|
| Direct message | Actor method call | Another entity sends a message to this agent's actor |
| Pub/Sub subscription | Pub/Sub subscriber | Agent subscribes to topics, activated on publish |
| Scheduled reminder | Actor reminder | Durable, cron-like trigger that survives restarts |
| Volatile timer | Actor timer | In-memory periodic callback while actor is active |
| External event | Input binding | Dapr binding translates external event to actor invocation |
| Workflow step | Workflow activity | A Dapr Workflow invokes the agent as an activity step |
| **Initiative** | Actor reminder | Agent's cognition loop fires periodically; agent decides whether to act |

### Pub/Sub System

Dapr pub/sub is broker-agnostic. The same application code works with Redis, Kafka, RabbitMQ, Azure Event Hubs, etc. — swapped via YAML component configuration.

```yaml
# dapr/components/pubsub.yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: ensemble-pubsub
spec:
  type: pubsub.redis          # swap to pubsub.kafka for production
  version: v1
  metadata:
    - name: redisHost
      value: localhost:6379
```

**Topic namespacing:** Topics are namespaced by ensemble to prevent cross-ensemble leakage:

- `engineering-team/github/issues/opened`
- `engineering-team/pr-reviews`
- `research-team/papers/new-arxiv`

Dapr's namespace support for pub/sub consumer groups provides additional multi-tenancy isolation.

---

## Addressing

Addresses are globally unique, namespaced by **tenant** and **ensemble path**.

**Scheme:** `{entity-type}://{tenant}/{ensemble-path}/{name}`

Within a tenant, the tenant prefix can be omitted (implicit context). Across tenants, it's required.

**Within-tenant addresses** (tenant prefix implicit):

- `agent://engineering-team/ada` — Ada in the engineering-team ensemble
- `agent://engineering-team/backend-team/ada` — Ada in a nested sub-ensemble
- `ensemble://engineering-team` — the ensemble itself
- `human://engineering-team/savasp` — a human participant
- `connector://engineering-team/github` — the GitHub connector
- `role://engineering-team/backend-engineer` — multicast to all agents with that role
- `topic://engineering-team/pr-reviews` — a pub/sub topic

**Cross-tenant addresses** (fully qualified):

- `agent://acme/engineering-team/ada` — Ada in acme tenant's engineering team
- `ensemble://other-corp/research-team` — another tenant's ensemble

**System-level addresses:**

- `system://root` — the tenant's root ensemble
- `system://directory` — the tenant's root directory (aggregated from all ensembles)
- `system://package-registry` — the package registry

### Cross-Tenant Communication

Cross-tenant communication is **not enabled by default**. When allowed, it requires:

1. **Explicit federation policy** — both tenants must opt in
2. **OAuth 2.0 / API key authentication** — the requesting tenant authenticates with the target tenant's API
3. **Scoped permissions** — the federation agreement specifies what's accessible (specific ensembles, roles, expertise queries)
4. **Audit trail** — all cross-tenant messages are logged for both parties

```yaml
# Tenant-level federation config
federation:
  allow_inbound: true
  trusted_tenants:
    - tenant: partner-corp
      scope: [expertise-query, message]  # what they can do
      ensembles: [engineering-team]       # which ensembles they can reach
      auth: oauth2                        # authentication method
  allow_outbound: true
```

This model enables the future expertise marketplace — cross-tenant expertise queries are just federated directory lookups, and cross-tenant agent lending is just scoped messaging with authentication.

---

## Ensemble Orchestration

Since an ensemble IS an agent (IAddressable), it orchestrates itself. No separate conductor entity exists. The EnsembleActor has optional AI configuration:

```yaml
ensemble:
  ai:                                # optional — makes the ensemble AI-powered
    backend: claude
    model: claude-sonnet-4-20250514
    prompt: |
      You coordinate a software engineering team...
    skills:
      - package: spring-voyage/software-engineering
        skill: triage-and-assign
```

**Three orchestration modes:**

1. **AI-powered** — the ensemble has `ai` config. Inbound messages are processed by the LLM, which decides routing, assignment, and coordination. This replaces the v1 team leader.
2. **Rule-based** — no `ai` config. The ensemble routes by policy (round-robin, role-matching, capability-based). Mechanical, deterministic.
3. **Peer** — no routing at all. Messages are broadcast to all members. Members decide for themselves whether to act (using their own initiative).

A member agent can be designated as a **lead** — a member with elevated permissions who also handles escalation. But the lead is still just a member, not a special entity.

---

## Agent Output & Observation

Every `IAddressable` publishes an **activity stream** — a sequence of events describing what the entity is doing:

```csharp
interface IAddressable
{
    IAsyncEnumerable<ActivityEvent> ActivityStream { get; }
}
```

Activity events include: messages sent/received, tasks started/completed, decisions made, errors, state changes. This stream is the foundation for:

### 1. Agent-to-Agent Observation

An agent can subscribe to another agent's activity stream — with permission. This enables monitoring, mentoring, and learning:

```yaml
agent:
  name: senior-ada
  observers:
    allow: [role://engineering-team/qa-engineer]  # who can observe me
  observing:
    - agent://engineering-team/junior-dev         # who I observe
      filter: "event.type == 'error'"             # only errors
```

### 2. Alwyse as Observer

Alwyse is modeled as an **observer agent** — it subscribes to its Spring Voyage agent's activity stream, watching everything the agent does:

```
┌──────────────────────┐     activity stream     ┌──────────────────┐
│   Ada (SV Agent)     │ ──────────────────────► │  Ada's Alwyse    │
│                      │                         │  (Observer Agent) │
│  - receives tasks    │                         │                   │
│  - writes code       │   cognitive feedback    │  - accumulates    │
│  - creates PRs       │ ◄────────────────────── │    experience     │
│  - responds to       │                         │  - builds expertise│
│    reviews           │                         │  - provides        │
│                      │                         │    sub-agents      │
└──────────────────────┘                         └──────────────────┘
```

Alwyse observes *everything* the agent does (given permission), just as it observes a human user. It then:

- Accumulates experience → builds the expertise profile
- Provides cognitive feedback → powers the initiative cognition loop
- Offers sub-agents → alwyse can spawn helper agents (research, tool use, etc.) on behalf of the Spring Voyage agent

### 3. Ensemble Observation

An ensemble's activity stream is the aggregate of its members' streams (filtered by the ensemble's visibility policies). A parent ensemble can observe its child ensembles.

### 4. Human Observation

Humans with the right permissions can subscribe to agent/ensemble activity streams via the UI, CLI, or notifications. This powers the dashboard, activity feed, and alerting.

### Observation Permissions

| Observer | Subject | Permission Required |
|----------|---------|-------------------|
| Agent → Agent (same ensemble) | Member activity | `observer` permission on subject, or ensemble policy allows |
| Agent → Agent (cross-ensemble) | Member activity | Federation policy + subject's `observers.allow` list |
| Alwyse → its agent | All activity | Implicit — alwyse always has observer access to its own agent |
| Ensemble → member | Member activity | Implicit — ensembles can observe their members |
| Human → agent/ensemble | Activity stream | Ensemble permission `operator` or above |
| Cross-tenant | Any activity | Federation agreement + tenant-level audit |

---

## Multi-Human Participation & Permissions

Humans are first-class addressable entities with role-based access control:

- **HumanActor** — represents a human participant, routes messages to notification channels
- Multiple humans can interact with the same ensemble or individual agents simultaneously
- Messages to humans are delivered via configured channels (Slack, email, web UI, GitHub)
- Humans can send messages to agents via CLI, web UI, or external systems
- Human approval steps can be part of ensemble workflows (human-in-the-loop pattern)

### Permission Model

Multiple users can access an ensemble with different permission levels. Permissions are scoped hierarchically:

**System-level roles:**

| Role | Scope | Permissions |
|------|-------|-------------|
| **Platform Admin** | Global | Create/delete ensembles, manage users, system config |
| **User** | Global | Create own ensembles, join ensembles they're invited to |

**Ensemble-level roles:**

| Role | Permissions |
|------|-------------|
| **Owner** | Full control — configure ensemble, manage members, delete, set policies, manage permissions |
| **Admin** | Manage agents, connectors, packages, workflows. Cannot delete ensemble or change owners. |
| **Operator** | Start/stop ensemble, view all status, interact with agents, approve workflow steps |
| **Contributor** | Send messages to agents, view feed, trigger tasks. Cannot change config. |
| **Viewer** | Read-only access to ensemble state, feed, metrics, agent status |

**Permission inheritance in recursive ensembles:**

- A user's role in a parent ensemble does **not** automatically grant access to child ensembles
- Each ensemble manages its own ACL independently
- Exception: Platform Admins have implicit access everywhere (configurable)
- An ensemble can optionally inherit its parent's ACL: `permissions.inherit: parent`

### Agent & Ensemble Permissions

Permissions aren't just for humans. Agents and ensembles also have scoped access:

**Agent permissions (what an agent can do):**

| Permission | Description |
|-----------|-------------|
| `message.send` | Send messages to specified addresses/roles |
| `message.receive` | Receive messages (always granted within own ensemble) |
| `directory.query` | Query the ensemble directory |
| `directory.query.parent` | Query the parent ensemble's directory |
| `directory.query.root` | Query the tenant root directory |
| `topic.publish` | Publish to specified topics |
| `topic.subscribe` | Subscribe to specified topics |
| `observe` | Subscribe to another agent's activity stream |
| `workflow.participate` | Be invoked as a workflow step |
| `agent.spawn` | Create new agents dynamically (future) |

**Ensemble permissions (what an ensemble can do within the tenant):**

| Permission | Description |
|-----------|-------------|
| `federation.query` | Query other ensembles' directories |
| `federation.message` | Send messages to other ensembles |
| `federation.lend` | Lend agents to other ensembles |
| `federation.borrow` | Borrow agents from other ensembles |

### Cross-Tenant Permission Model

Cross-tenant communication requires **OAuth 2.0 authentication** at the platform level:

1. Tenants register as OAuth clients with each other (or via a shared identity provider)
2. Federation agreements are established (which ensembles, which capabilities)
3. Cross-tenant messages carry OAuth tokens; the receiving tenant validates and enforces scoping
4. All cross-tenant activity is audited on both sides

### Universal Permission Principle

Both humans and agents are `IAddressable`. The permission model applies uniformly — it governs what any entity (human, agent, ensemble, connector) can *do*. The messaging model treats them identically. The system enforces permissions at the boundary, transparently.

---

## Expertise Discovery & Alwyse Integration

### Alwyse: Personal Intelligence for Every Agent

Alwyse is not just a memory backend — it is an **observer agent** that acts as each Spring Voyage agent's personal intelligence. Just as alwyse observes and supports a human user, it observes and supports a Spring Voyage agent.

**What alwyse does for each agent:**

- **Observes** — subscribes to the agent's activity stream, watching all inputs, outputs, decisions, and outcomes
- **Accumulates experience** — builds a cognitive model from observed activity (not just memory — pattern recognition, judgment development)
- **Develops expertise profiles** — tracks what the agent has done, how well, and evolves the expertise profile accordingly
- **Powers the cognition loop** — provides the "Reflect" and "Decide" steps in the initiative loop with real cognitive reasoning
- **Provides sub-agents** — alwyse can spawn helper agents on behalf of the Spring Voyage agent (e.g., to perform research, use external tools, search the web, analyze data)
- **Adapts instructions** — suggests refinements to the agent's prompt based on observed patterns

**Integration points:**

1. **`IMemoryStore`** — `AlwyseMemoryStore` replaces simple state persistence with cognitive memory (experiences, patterns, knowledge graphs)
2. **`IAIBackend` wrapper** — `AlwyseAIBackend` wraps the underlying LLM call, augmenting prompts with relevant experience and capturing new experiences
3. **`ActivityStream` observer** — alwyse subscribes to the agent's activity stream (implicit observer permission)
4. **Sub-agent provider** — alwyse exposes helper agents via its own API, accessible to the Spring Voyage agent

```
┌──────────────────────────────┐
│      Spring Voyage Agent     │
│                              │
│  ┌────────────────────────┐  │     activity stream
│  │    Agent Logic         │──│──────────────────────►┌──────────────────┐
│  │    (IAddressable)      │  │                       │  Alwyse Instance │
│  └──────────┬─────────────┘  │  cognitive feedback   │                  │
│             │                │◄──────────────────────│  - observes      │
│  ┌──────────▼─────────────┐  │                       │  - learns        │
│  │  AlwyseAIBackend       │  │  sub-agent services   │  - evolves       │
│  │  (wraps Claude/GPT)    │  │◄──────────────────────│    expertise     │
│  └──────────┬─────────────┘  │                       │  - provides      │
│             │                │                       │    sub-agents    │
│  ┌──────────▼─────────────┐  │                       │  - adapts        │
│  │  AlwyseMemoryStore     │  │                       │    prompts       │
│  │  (cognitive memory)    │  │                       └──────────────────┘
│  └────────────────────────┘  │
└──────────────────────────────┘
```

### Expertise Profiles

Each agent has an **expertise profile** — a structured representation of what the agent knows and how well it knows it. The profile has two sources:

**1. Seeded from configuration** — the agent definition declares initial expertise:

```yaml
agent:
  name: ada
  expertise:                          # seeded expertise (static)
    - domain: python/fastapi
      level: advanced
    - domain: postgresql
      level: intermediate
    - domain: csharp/aspnet
      level: novice
```

**2. Evolved through alwyse** — as the agent works, alwyse tracks outcomes and evolves the profile:

```
ExpertiseProfile:
  agent: agent://acme/engineering-team/ada
  domains:
    - name: python/fastapi
      level: expert                   # evolved from seeded "advanced"
      source: alwyse                  # alwyse evolved this
      evidence: 47 completed tasks, 3 failed, avg review score 4.2/5
      last_active: 2026-03-25
    - name: postgresql/optimization
      level: advanced                 # evolved from seeded "intermediate"
      source: alwyse
      evidence: 12 completed tasks, 1 failed
      last_active: 2026-03-20
    - name: react/nextjs
      level: novice                   # emerged — not seeded, learned on the job
      source: alwyse
      evidence: 2 completed tasks, 1 failed
      last_active: 2026-02-15
```

Without alwyse, the profile stays at seeded values. With alwyse, it evolves — domains can level up, new domains can emerge from experience, and stale expertise can decay.

### Discovery Mechanisms

The directory is a **property of the ensemble**, not a separate entity. Each ensemble maintains its own directory of member expertise. Directories compose recursively through the ensemble hierarchy.

#### 1. Ensemble Directory (Local)

Each ensemble maintains a directory of its members' expertise profiles as part of its state. The ensemble itself handles queries.

#### 2. Root Ensemble Directory (Tenant-wide)

The tenant's root ensemble aggregates directories from all child ensembles (when `expertise_sharing: advertise` policy is set). Querying the root directory searches across all ensembles in the tenant.

#### 3. Specialized Directory Ensembles (Future)

A directory could itself be an ensemble — a group of "librarian" agents that maintain expertise catalogs, match requests to experts, and facilitate introductions.

#### 4. Cross-Tenant Directory (Future)

Tenants with federation agreements can query each other's root directories.

### Future: Expertise Marketplace

> **Not for MVP — noted here for architectural consideration.**
>
> As the platform scales to many ensembles (potentially across organizations), cross-ensemble expertise access could have a cost structure:
>
> - **Token/credit-based billing** for cross-ensemble agent usage
> - **Expertise licensing** — an ensemble publishes expertise as a service
> - **Usage metering** — track how much one ensemble uses another's agents
> - **SLA contracts** — response time, availability guarantees for shared expertise

---

## Package System

Packages are installable bundles that extend an ensemble's capabilities. A package may contain any combination of:

```yaml
package:
  name: spring-voyage/software-engineering
  version: 1.0.0
  description: Software engineering workflows for agent ensembles
  
  contents:
    skills:                          # markdown skill documents for agents
      - triage-and-assign.md
      - pr-review-cycle.md
      - code-review-standards.md
    workflows:                       # Dapr Workflow definitions
      - software-dev-cycle.yaml
      - hotfix-cycle.yaml
    agent_templates:                 # reusable agent definitions
      - backend-engineer.yaml
      - frontend-engineer.yaml
    ensemble_templates:              # reusable ensemble definitions
      - engineering-team.yaml
    connectors:                      # connector implementations
      - github-connector.dll         # compiled .NET connector
    dapr_components:                 # Dapr component YAML files
      - github-binding.yaml
    topics:                          # topic schemas this package defines
      - github-events.schema.json
```

**Distribution:**

- NuGet packages for .NET code (connectors, actor implementations, workflow activities)
- A companion manifest format for declarative content (skills, templates, YAML)
- Consider compatibility with Anthropic's `plugin.json` manifest for agent-facing skills

**Installation:** `spring package install spring-voyage/software-engineering` adds the package to the ensemble and makes its contents available.

---

## Connectors

Connectors bridge external systems to the ensemble. Two approaches, depending on complexity:

### 1. Dapr Bindings (Simple Connectors)

For standard integrations, Dapr's 40+ built-in bindings handle the plumbing — configured as YAML, no code needed.

### 2. Custom Connectors (Rich Integrations)

For complex integrations like GitHub or Slack, a custom `ConnectorActor` provides bidirectional event/action mapping, stateful connection management, and domain-specific logic.

---

## Client API Surface (CLI, Web, Native Apps)

The platform exposes a unified API that all clients consume:

| API Domain | Operations | Clients |
|------------|-----------|---------|
| **Identity & Auth** | OAuth login, API keys, tenant management | All |
| **Ensemble Management** | CRUD ensembles, configure AI/policies/connectors, manage members | CLI, Web |
| **Agent Management** | CRUD agents, view status, configure expertise seeds | CLI, Web |
| **Messaging** | Send messages to agents/ensembles, read conversations | CLI, Web, Native |
| **Activity Streams** | Subscribe to agent/ensemble activity (SSE/WebSocket) | Web, Native |
| **Workflow Management** | Start/stop/inspect workflows, approve human-in-the-loop steps | CLI, Web |
| **Directory & Discovery** | Query expertise, browse agent capabilities | CLI, Web |
| **Package Management** | Install/remove packages, browse registry | CLI |
| **Observability** | Metrics, telemetry, cost tracking, audit logs | Web |
| **Admin** | User management, tenant config, federation policies | CLI, Web |

### Hosting Modes

```
┌─────────────────────────────────────────────────────────┐
│                    Spring.Core + Spring.Dapr             │
│              (same interfaces, same behavior)           │
├─────────────┬───────────────┬───────────────────────────┤
│ Daemon Host │   Web Host    │      Worker Host          │
│ (local API) │ (REST + WS   │   (headless, config-      │
│             │  + authz)     │    driven)                │
├─────────────┼───────────────┼───────────────────────────┤
│    CLI      │  Web Portal   │      (no client)          │
│             │  Native Apps  │                           │
│             │  3rd-party    │                           │
└─────────────┴───────────────┴───────────────────────────┘
```

---

## Execution Model: Brain vs. Hands

A critical architectural distinction: the **agent actor** (brain) and the **execution environment** (hands) are separate concerns.

```
┌─────────────────────────────────────────────────────────────────┐
│                        Agent Actor (Brain)                      │
│                                                                 │
│  Dapr virtual actor — always "exists" (activated on demand)     │
│  Lightweight: state, mailbox, subscriptions, cognition loop     │
│  Lives in the Spring Voyage host process (Dapr sidecar)         │
│                                                                 │
│  Two AI execution patterns:                                     │
│                                                                 │
│  Pattern A: Actor-hosted AI                                     │
│  ┌───────────────┐                                              │
│  │ Agent Actor    │──► calls AI backend directly (API call)     │
│  │               │◄── receives response                         │
│  │               │    Good for: simple reasoning, classification│
│  └───────────────┘    routing, initiative decisions             │
│                                                                 │
│  Pattern B: Delegated execution (v1 model)                      │
│  ┌───────────────┐    ┌──────────────────────────────────────┐  │
│  │ Agent Actor    │──► │    Execution Environment (Hands)     │  │
│  │               │    │                                      │  │
│  │  dispatches   │    │  Runs `claude` CLI or other tools    │  │
│  │  work to      │    │  The TOOL drives the agentic loop    │  │
│  │  environment  │    │  Agent actor monitors + collects     │  │
│  │               │◄── │  results                             │  │
│  └───────────────┘    └──────────────────────────────────────┘  │
│                       Good for: software engineering, complex   │
│                       multi-step tool use, when the agentic     │
│                       loop needs filesystem/network access      │
└─────────────────────────────────────────────────────────────────┘
```

### Execution Modes

The ensemble's `execution.mode` determines how execution environments are provisioned:

| Mode | Isolation | Cost | Scale | Startup | Best For |
|------|-----------|------|-------|---------|----------|
| `in-process` | None | Minimal | 10K+ agents | Instant | LLM-only agents, research, simulations |
| `shared-container` | Process | Low | ~100/container | Instant | Small teams, dev/test |
| `container-per-agent` | Full | Medium | ~1K/cluster | Seconds | Production software eng |
| `ephemeral` | Maximum | Medium-High | By concurrency | Seconds | Untrusted, compliance |
| `pool` | Full | Variable | 10K+ agents | Seconds (warm) | Large-scale, mixed workloads |
| `serverless` | Full | Pay-per-use | Unlimited | Seconds-minutes | Cloud-native, burst |

See the detailed execution mode descriptions in the full plan document.

---

## Security & Multi-Tenancy

### Dapr-native Security

- **Agent identity** — Dapr provides secure identity for actors
- **mTLS** — all service-to-service communication encrypted automatically
- **Secrets management** — pluggable secret stores
- **Access control policies** — Dapr configuration restricts which actors can invoke which building blocks

### Multi-Tenancy via Dapr Namespaces

- Each tenant gets a Dapr namespace
- Pub/sub, state stores, and actor identities are namespace-scoped
- The web host maps authenticated users to namespaces

---

## .NET Solution Structure (Proposed)

```
SpringVoyage.sln
├── src/
│   ├── Spring.Core/                    # Domain model: interfaces, types, no Dapr dependency
│   ├── Spring.Dapr/                    # Dapr building block implementations
│   ├── Spring.AI.Claude/              # Claude backend
│   ├── Spring.AI.OpenAI/              # OpenAI backend
│   ├── Spring.AI.Gemini/              # Gemini backend
│   ├── Spring.Connector.GitHub/       # GitHub connector
│   ├── Spring.Connector.Slack/        # Slack connector
│   ├── Spring.Host.Daemon/            # Local daemon host
│   ├── Spring.Host.Web/               # Web API host (authz, multi-tenant)
│   ├── Spring.Host.Worker/            # Headless worker host
│   ├── Spring.Cli/                    # CLI tool (dotnet tool)
│   └── Spring.Web/                    # Web UI
├── dapr/                              # Dapr component YAML files
├── packages/                          # Built-in packages
└── tests/
```

---

## Open Design Questions

1. **Dapr Agents (.NET gap)** — Dapr Agents v1.0 is Python-only. We build our own agent framework on Dapr's core building blocks. Monitor for .NET SDK.
2. **Workflow Engine** — Dapr Workflows for MVP; evaluate Temporal if we hit limitations.
3. **Recursive Ensemble Execution** — same Dapr sidecar vs. separate app per ensemble.
4. **State & Event Sourcing** — per actor type decision.
5. **Second MVP Connector** — Slack, Email, or Filesystem?
6. **Web UI Technology** — Blazor vs. React/Next.js.
7. **Initiative Cost Management** — token budgets, tiered cognition, adaptive frequency.

---

## Future Directions

- **Expertise Marketplace** — cross-ensemble billing, licensing, SLA contracts
- **Dynamic Agent & Ensemble Creation** — runtime spawning, ad-hoc ensembles, emergent structure
- **Cross-Organization Federation** — multi-deployment trust and billing
- **Advanced Self-Organization** — agents negotiating allocation, restructuring ensembles
- **Alwyse Depth** — genuine specialization, knowledge transfer, mentoring
- **Ensemble Evolution** — self-adjusting team compositions based on outcomes
