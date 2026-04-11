# Workflows

> **[Architecture Index](README.md)** | Related: [Units & Agents](units.md), [Infrastructure](infrastructure.md), [Connectors](connectors.md)

---

## Workflows & External Orchestration

The orchestration strategies defined in the [Units & Agents](units.md) document determine *how* a unit routes messages to members. This document covers the two workflow models (container-based and platform-internal), external workflow engine integration (A2A), and workflow patterns.

### Workflow-as-Container (Primary Model)

Domain workflows are deployed as **containers** — the same deployment model used for delegated agent execution environments. A workflow container runs its own Dapr sidecar and orchestrates by sending messages to agents in the unit. This decouples workflow evolution from platform releases: updating a workflow means deploying a new container image, not recompiling the host.

**How it works:**

1. The unit's `WorkflowStrategy` receives an incoming message.
2. The strategy dispatches to the workflow container via Dapr service invocation.
3. The workflow container orchestrates the work — calling agents as activities, waiting for events, managing state.
4. The workflow communicates with agents via the Dapr sidecar (messages, pub/sub, state).
5. On completion, the workflow reports results back to the unit actor.

**Workflow containers can use any workflow engine:**

- **Dapr Workflows** (C# or Python) — durable orchestration with the Dapr Workflow SDK
- **Temporal** — if the team prefers Temporal's model
- **Custom** — any process that can speak to the Dapr sidecar

**Example Dapr Workflow in a container** (C#):

```csharp
public class SoftwareDevCycleWorkflow : Workflow<DevCycleInput, DevCycleOutput>
{
    public override async Task<DevCycleOutput> RunAsync(
        WorkflowContext ctx, DevCycleInput input)
    {
        // Triage and classify the issue
        var triage = await ctx.CallActivityAsync<TriageResult>(
            nameof(TriageActivity), input.Issue);
        
        // Select best-fit agent by expertise
        var agent = await ctx.CallActivityAsync<AgentRef>(
            nameof(AssignByExpertiseActivity), triage);
        
        // Agent creates implementation plan
        var plan = await ctx.CallActivityAsync<Plan>(
            nameof(CreatePlanActivity), new PlanInput(agent, triage));
        
        // Human-in-the-loop: wait for plan approval
        var approval = await ctx.WaitForExternalEventAsync<Approval>(
            "plan-approval", timeout: TimeSpan.FromHours(24));
        
        // Agent implements the plan
        var pr = await ctx.CallActivityAsync<PrResult>(
            nameof(ImplementActivity), new ImplInput(agent, plan));
        
        // Fan-out: multiple reviewers
        var reviews = await Task.WhenAll(
            ctx.CallActivityAsync<ReviewResult>(nameof(ReviewActivity), pr),
            ctx.CallActivityAsync<ReviewResult>(nameof(ReviewActivity), pr));
        
        // Merge if all approved
        if (reviews.All(r => r.Approved))
            await ctx.CallActivityAsync(nameof(MergeActivity), pr);
        
        return new DevCycleOutput(pr, reviews);
    }
}
```

The unit definition references the workflow container through its `ai` block — see [Units & Agents](units.md) for the full unit definition example.

### Platform-Internal Workflows (Dapr Workflows in Host)

A small set of workflows are compiled into the .NET host for platform-internal orchestration. These handle agent lifecycle, cloning lifecycle, and other platform concerns — not domain workflows.

Platform-internal workflows are **not** used for domain orchestration. Domain workflows always run in containers.

### External Workflow Engines via A2A

The platform supports external workflow engines as unit orchestrators via the A2A protocol:


| Engine             | Integration Pattern                                                                                                                                  |
| ------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Google ADK**     | An ADK agent graph runs as a Python process. Participates as a unit member or orchestrator via A2A. ADK nodes can invoke Spring agents as A2A peers. |
| **LangGraph**      | A LangGraph graph runs as a Python process. Same A2A integration. Graph nodes can be Spring agents.                                                  |
| **Custom**         | Any process that speaks A2A can orchestrate a unit or participate as a member.                                                                       |


### A2A Protocol Integration

A2A (Agent-to-Agent) is an open protocol for cross-framework agent communication. It enables:

- **External agents as unit members** — an ADK agent, LangGraph node, or AutoGen agent participates in a Spring unit via A2A, wrapped as an `A2AAgentActor : IMessageReceiver`.
- **External orchestrators** — an external workflow engine drives a Spring unit's agents via A2A.
- **Cross-platform collaboration** — Spring agents collaborate with agents built on other frameworks.

Each unit can expose an A2A endpoint. Each external agent is wrapped as an `A2AAgentActor` implementing `IMessageReceiver`, making it indistinguishable from a native agent at the messaging level.

### Workflow Patterns

All workflow patterns below are supported regardless of which workflow engine runs inside the container:


| Pattern           | Description                        | Example                                  |
| ----------------- | ---------------------------------- | ---------------------------------------- |
| Sequential        | Steps execute one after another    | triage → assign → implement → review     |
| Parallel          | Multiple steps concurrently        | tests + linting + security scan          |
| Fan-out/Fan-in    | Distribute work, aggregate results | assign to 3 agents, collect PRs          |
| Conditional       | Branch based on state              | if complexity > threshold → human review |
| Loop              | Repeat until condition met         | review cycle until approved              |
| Human-in-the-loop | Pause, wait for human input        | approval before implementing             |
| Sub-workflow      | Delegate to nested workflow        | "implement feature" is multi-step        |
