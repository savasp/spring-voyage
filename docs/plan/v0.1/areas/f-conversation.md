# Area F: Conversation concept

**Status:** 🟢 **Planning done.** Sub-issues, supersessions, and live work breakdown live on umbrella [#1220](https://github.com/cvoya-com/spring-voyage/issues/1220) — see its sub-issue panel.

## Reframing anchor

The conceptual anchor for this area is the participant-set reframing of "conversation":

- "Conversation" → **participant-set relationship** (the participant set IS the identity).
- Users see a **dialog surface** (one per relationship with an agent, like iMessage DMs) + an **ambient task surface**. No "new conversation" button, no thread picker, no session list.
- Per-conversation mailbox; memory has two layers (per-conversation + agent-level spanning); cross-conversation flow is policy-governed.

The system-design sub-issue (tracked on the umbrella) resolves the open questions — naming, container/execution model, dispatch semantics, memory flow, participant-set change UX, initiative messages, misinference correction, cold start, multi-party, migration — before implementation begins. The execution-plan issue is **deliberately deferred** until system design converges.

## Dependencies

- Depends on: J (ADR audit) ✅ done.
- Blocks: C2, E1, E2 (architecturally); Area D Stage 4 (memory contract).
- Intersects with: D (execution model, boundary implications).
