# Area F: Conversation concept (#1123)

**Status:** 🟢 **Planning done.** [#1085](https://github.com/cvoya-com/spring-voyage/issues/1085) and [#1086](https://github.com/cvoya-com/spring-voyage/issues/1086) closed as superseded by [#1123](https://github.com/cvoya-com/spring-voyage/issues/1123). Sub-issues populated under umbrella [#1220](https://github.com/cvoya-com/spring-voyage/issues/1220) — see the sub-issue panel for the live work breakdown. The system-design sub-issue is the unblocker for the rest of the area; the execution-plan issue is **deliberately deferred** until system design converges.

## Reframing anchor

[#1123](https://github.com/cvoya-com/spring-voyage/issues/1123) is the conceptual anchor. Key decisions already made:

- "Conversation" → **participant-set relationship** (the participant set IS the identity).
- Users see: a **dialog surface** (one per relationship with an agent, like iMessage DMs) + an **ambient task surface**.
- No "new conversation" button, no thread picker, no session list.
- Per-conversation mailbox; memory has two layers (per-conversation + agent-level spanning); cross-conversation flow is policy-governed.

The system-design sub-issue resolves the 10 open questions from #1123 (naming, container/execution model, dispatch semantics, memory flow, participant-set change UX, initiative messages, misinference correction, cold start, multi-party, migration) before implementation can begin. Documentation impact (glossary, messaging architecture, ADR-0018 revision, new ADR) is tracked as separate sub-issues blocked on the design.

## Dependencies

- Depends on: J (ADR audit) ✅ done.
- Blocks: C2, E1, E2 (architecturally); Area D Stage 4 (memory contract).
- Intersects with: D (execution model, boundary implications).
