# Execution Plan

> **[Roadmap Index](README.md)** | **Status: Active**

This document is the operating picture for shipping the remaining Spring Voyage V2 work. It is the source of truth for **what** ships, **in what order**, **bundled how**, and **who-blocks-whom**. Coordinator agents use it to hand concrete work units to implementer agents. Maintainers use it to see the state of the fleet at a glance.

The issue graph on GitHub is the authoritative status — this document describes the _plan_; the issues describe the _state_. Keep them in sync.

## Umbrellas and indexes

- [#418](https://github.com/cvoya-com/spring-voyage/issues/418) — V2 completion plan (top-level tracker).
- [#434](https://github.com/cvoya-com/spring-voyage/issues/434) — Portal redesign umbrella; 31 sub-issues wired with `blockedBy` edges.
- [#421](https://github.com/cvoya-com/spring-voyage/issues/421) — Phase 4 umbrella.
- [#422](https://github.com/cvoya-com/spring-voyage/issues/422) — Phase 5 umbrella (implicit — no dedicated issue yet; track via `roadmap:phase-5` label).
- [#423](https://github.com/cvoya-com/spring-voyage/issues/423) — Phase 6 umbrella.
- [`docs/design/portal-exploration.md`](../design/portal-exploration.md) — portal plan of record.
- [`docs/roadmap/phase-*.md`](.) — per-phase deliverable lists.

## Principles

Every decision below follows from these. When in doubt, re-derive.

1. **Unblocked-first.** A PR does not start until every open `blockedBy` edge on its issues is closed. This is enforced by GitHub's issue-dependency graph — don't bypass it.
2. **Topic-similarity bundling.** Bundle issues into a single PR when they touch the same files, follow the same pattern, or share review context. Avoid bundling unrelated work even if both are unblocked.
3. **Medium-or-largish PRs, not tiny or huge.** One reviewer is the bottleneck. Prefer ~200–800 line PRs that land one coherent deliverable. Split anything that crosses ~1,500 lines or touches fundamentally different subsystems.
4. **Hard UI/CLI parity rule.** A portal surface PR **does not merge** before its matching CLI PR has merged. Wire the dependency with `addBlockedBy` and verify on the PR page before approving.
5. **GitHub App identity for every op.** Push, PR create, issue edit, comment — all use `savasp-agent` via `~/.claude/skills/github-app/scripts/get_token.py`. Never personal credentials.
6. **Auto-merge on every PR.** After opening, enable auto-merge (`gh pr merge <N> --auto`). Let CI + review gate rather than manual merges.
7. **Worktrees for every PR.** Each PR lives in its own isolated worktree, based on the latest `origin/main`. No shared branches, no long-lived feature branches.
8. **Pre-push CI is mandatory.** Build, test, format, and any workflow-level lints pass locally before push. Per `~/.claude/CLAUDE.md`.
9. **Docs update with code.** Per #424 convention: every feature PR updates the relevant guide / architecture / concept docs and, for portal work, `DESIGN.md`.
10. **Reference issue numbers.** Every PR body has `closes #N` per issue it closes (one line per issue — GitHub auto-closes each on merge).

## How to use this document

### For a coordinator agent

You have been handed this plan. Your job is to dispatch implementer agents for a specific **wave** or **track**.

1. Find your wave/track section below. Read every PR entry in it.
2. For each PR whose prerequisites are already merged, spawn one implementer agent.
3. Launch them in parallel (multiple `Agent` calls in one message, with `isolation: "worktree"` and `run_in_background: true`). Do not serialize unless the PRs share files.
4. Monitor notifications; when an implementer returns a PR URL, record it under **State** below.
5. Do not start PRs whose prerequisites are still open. Wait for notifications; re-evaluate when blockers close.

### For an implementer agent

You have been handed one PR entry below (by ID, e.g. `PR-F1`). That entry lists the issues to close, the prerequisites, the scope, and the acceptance. Follow the dispatch pattern in [§ Dispatch brief](#dispatch-brief-template-for-implementer-agents). You are not responsible for the plan — only for your PR.

### Naming

- PR IDs follow `<track>-<wave>-<seq>`, e.g. `PR-F1`, `PR-C3`, `PR-S2`, `PR-PLAT-RUN-1`.
- Branch names: `<type>/<short-slug>`, e.g. `feat/tanstack-streaming`, `cli/conversation-inbox`, `docs/execution-plan`.
- PR titles: imperative, under 70 chars, close the referenced issues' intent.

## Wave overview

| Wave | Track | PRs | Prerequisites | Runs with |
|---|---|---|---|---|
| **0** | Portal foundation | F1–F5 (5 PRs) | none | Wave 1, Platform Wave |
| **1** | CLI parity | C1–C4 (4 PRs) | none | Wave 0, Platform Wave |
| **Platform** | Phase 4 + 5 + 6 core | PLAT-RUN-1/2/3, PLAT-ORCH-1, PLAT-OBS-1, PLAT-BOUND-1/2, PLAT-PKG-1 | varies; see entries | Wave 0, Wave 1 |
| **2** | Portal surfaces | S1–S4 (4 PRs) | Wave 0 foundation + matching CLI (Wave 1) + #391 for analytics | — |
| **3** | Portal refits | R1–R5 (5 PRs) | Wave 0 + Wave 1 + Phase 5 boundary work for some | — |
| **4** | Portal polish | Q1–Q2 (2 PRs) | Wave 2 + Wave 3 surfaces stable | — |
| **Phase 5** | Nesting + boundaries | PLAT-BOUND-3/4, PLAT-INIT-1, PLAT-CLONE-1 | Wave 0 extension slots (#440); some need Wave 3 portal | alongside Wave 3 |
| **Phase 6** | Platform maturity | PLAT-PKG-2, PLAT-DOMAIN-1 | Wave 2 Connectors + Packages surfaces | last |

Concretely: Wave 0 + Wave 1 + Platform Wave items run in parallel starting **now**. Wave 2 lights up as Wave 0 foundation PRs land. Wave 3 lights up as Wave 2 surfaces land and #420 closes (it closes when its last child, #406, is done — already merged). Wave 4 and Phase 5/6 come later.

---

## Track F — Portal foundation (Wave 0)

Five parallel PRs. Each unblocks one or more downstream surfaces under #434. All unblocked now.

### PR-F1: Supersede ADR 0001 + remove static-export scaffolding

- **Closes:** [#436](https://github.com/cvoya-com/spring-voyage/issues/436)
- **Prerequisites:** none
- **Size:** small-medium
- **Scope:**
  - Write ADR `docs/decisions/0005-portal-standalone-mode.md` recording "portal is on `output: 'standalone'`; static-export workarounds removed; streaming enabled for activity + conversation views".
  - Mark ADR 0001 superseded at the top of its frontmatter.
  - Delete `generateStaticParams` from `src/Cvoya.Spring.Web/src/app/units/[id]/page.tsx` and `src/Cvoya.Spring.Web/src/app/agents/[id]/page.tsx`.
  - Remove `__placeholder__` guards in the matching `*-client.tsx` files and any other sites.
  - Fix the stale source comment in `units/[id]/page.tsx` ("The dashboard is exported as a static site…").
- **Acceptance:**
  - ADR 0001 marked superseded by ADR 0005 in its header; ADR 0005 exists and cross-links.
  - `grep -rn "__placeholder__\|generateStaticParams" src/Cvoya.Spring.Web/src/` returns zero results.
  - `npm run build` in `src/Cvoya.Spring.Web/` succeeds.
  - `dotnet build` / test / format clean.
- **Unblocks:** [#444](https://github.com/cvoya-com/spring-voyage/issues/444) (nav restructure) — via the `addBlockedBy` edge.

### PR-F2: TanStack Query + streaming infrastructure

- **Closes:** [#438](https://github.com/cvoya-com/spring-voyage/issues/438), [#437](https://github.com/cvoya-com/spring-voyage/issues/437)
- **Prerequisites:** none (bundled because TanStack's cache consumes the stream — doing them in one PR avoids two round-trips to migrate every polling site)
- **Size:** medium-large
- **Scope:**
  - Add `@tanstack/react-query`; mount `QueryClientProvider` at the app root.
  - Add `/api/stream/activity` route handler (SSE) that proxies the platform's activity stream.
  - Add `useActivityStream` client hook that opens the stream and updates TanStack caches on event.
  - Migrate the three known polling sites (`page.tsx`, `/units/[id]/activity-tab`, `/agents/[id]/activity-tab`) off `useEffect` + `setInterval` onto typed `useQuery` hooks (for non-streaming) and `useActivityStream` (for the activity feed).
  - Document the pattern in `src/Cvoya.Spring.Web/src/lib/api/README.md`.
- **Acceptance:**
  - No `setInterval` in the three migrated sites.
  - SSE `/api/stream/activity` returns events when the underlying platform bus fires.
  - Vitest coverage for the new hook (mock stream, assert cache updates).
  - `dotnet build/test/format` + `npm run test` + `npm run lint` clean.
- **Unblocks:** [#447](https://github.com/cvoya-com/spring-voyage/issues/447), [#448](https://github.com/cvoya-com/spring-voyage/issues/448), [#449](https://github.com/cvoya-com/spring-voyage/issues/449), [#450](https://github.com/cvoya-com/spring-voyage/issues/450), [#451](https://github.com/cvoya-com/spring-voyage/issues/451).

### PR-F3: Extension slots + command palette

- **Closes:** [#440](https://github.com/cvoya-com/spring-voyage/issues/440), [#439](https://github.com/cvoya-com/spring-voyage/issues/439)
- **Prerequisites:** none (bundled because the palette is the first consumer of the route manifest)
- **Size:** medium
- **Scope:**
  - Define a lightweight plugin contract: route manifest (route → entry), auth adapter (`IAuthContext`), API-client decorator (request/response middleware). Export from `src/Cvoya.Spring.Web/src/lib/extensions/`.
  - Refactor sidebar / routing to consume the manifest rather than hard-coding routes.
  - Add `cmdk` dep; mount a global palette. Index units, agents, conversations, activity, settings; register CLI-equivalent actions (send message, create unit, rotate secret, etc.). Palette entries are extensible via the same manifest so hosted-only items (tenant switcher, billing) can be registered by an extension.
  - Bind `/` and `Cmd-K` / `Ctrl-K`.
- **Acceptance:**
  - Extension contract documented in `src/Cvoya.Spring.Web/src/lib/extensions/README.md`.
  - Palette opens on `Cmd-K`; typing filters all indexable routes and actions.
  - Vitest coverage for the palette keyboard shortcut and entry indexing.
  - OSS app renders with an empty extension set (no hosted entries present).
- **Unblocks:** [#447](https://github.com/cvoya-com/spring-voyage/issues/447), [#448](https://github.com/cvoya-com/spring-voyage/issues/448), [#449](https://github.com/cvoya-com/spring-voyage/issues/449), [#450](https://github.com/cvoya-com/spring-voyage/issues/450), [#451](https://github.com/cvoya-com/spring-voyage/issues/451).

### PR-F4: DESIGN.md + Stitch MCP + agent-DoD update

- **Closes:** [#441](https://github.com/cvoya-com/spring-voyage/issues/441), [#442](https://github.com/cvoya-com/spring-voyage/issues/442)
- **Prerequisites:** none (bundled — the DoD update references the committed `DESIGN.md` path)
- **Size:** medium
- **Scope:**
  - Author an initial `DESIGN.md` via Google Stitch that codifies the portal's current look — colors, typography, spacing, component patterns.
  - Commit as `src/Cvoya.Spring.Web/DESIGN.md`.
  - Update `AGENTS.md` § "Documentation Updates" (added by #424): for `src/Cvoya.Spring.Web/` changes, agents must check `DESIGN.md` and update it if the change adjusts the visual system.
  - Update DoD bullets in `.claude/agents/dotnet-engineer.md`, `connector-engineer.md`, `devops-engineer.md` to include `DESIGN.md` adherence for portal-touching work.
  - If the Stitch MCP server is available in the repo's MCP config, wire it.
- **Acceptance:**
  - `src/Cvoya.Spring.Web/DESIGN.md` exists and reflects current portal visuals.
  - `AGENTS.md` and three `.claude/agents/*.md` files reference `DESIGN.md`.
  - Agent-definitions lint passes.

### PR-F5: Breadcrumbs + object-card primitives + cross-link rules

- **Closes:** [#443](https://github.com/cvoya-com/spring-voyage/issues/443)
- **Prerequisites:** none
- **Size:** medium
- **Scope:**
  - Ship `<Breadcrumbs>` component with consistent back-navigation behaviour. Required on any page two levels or deeper.
  - Build `<UnitCard>`, `<AgentCard>`, `<ConversationCard>` primitives under `src/Cvoya.Spring.Web/src/components/cards/`. Shape: title, status badge, quick actions, cost mini-sparkline, "open" affordance. Reuse across Dashboard, Units list, Agents list (when #450 lands), Conversations list (when #447/#410 land).
  - Replace hard-coded back chevrons on existing detail pages with `<Breadcrumbs>`.
  - Wire cross-links per §3.3 of the portal exploration doc: unit card → conversations / costs / activity / policies; agent card → parent units + conversations.
- **Acceptance:**
  - Every existing two-levels-deep page uses `<Breadcrumbs>` (no ad-hoc back chevrons remain).
  - Three card primitives have Vitest coverage for the happy path + at least one empty state.
  - Snapshot or visual-review check that cards look identical across Dashboard, Units list, etc.

---

## Track C — CLI parity (Wave 1)

Four parallel PRs. Each closes one or more portal parity gaps. All unblocked now; all gate a portal surface via `addBlockedBy`.

Each CLI verb family must: (a) hit the existing HTTP endpoint (verify first — if the endpoint doesn't exist, file a backend issue and scope this PR around the HTTP work too); (b) support `--output json` per CLI convention; (c) ship tests under `tests/Cvoya.Spring.Cli.Tests/`; (d) add an e2e scenario under `tests/e2e/scenarios/fast/` that exercises the happy path.

### PR-C1: Conversation + Inbox CLI

- **Closes:** [#452](https://github.com/cvoya-com/spring-voyage/issues/452), [#456](https://github.com/cvoya-com/spring-voyage/issues/456)
- **Prerequisites:** none. Inbox CLI may need a backend query endpoint — scope increase allowed if so.
- **Size:** medium
- **Scope:** verbs `spring conversation list|show|send --conversation <id>`, `spring inbox list|show|respond`.
- **Unblocks:** [#410](https://github.com/cvoya-com/spring-voyage/issues/410), [#447](https://github.com/cvoya-com/spring-voyage/issues/447).

### PR-C2: Unit policy + humans + template CLI

- **Closes:** [#453](https://github.com/cvoya-com/spring-voyage/issues/453), [#454](https://github.com/cvoya-com/spring-voyage/issues/454), [#460](https://github.com/cvoya-com/spring-voyage/issues/460)
- **Prerequisites:** none. Verify the humans HTTP endpoint exists — docs reference it but the CLI gap suggests the verb was never wired.
- **Size:** medium
- **Scope:** `spring unit policy {skill|model|cost|execution-mode|initiative} {get|set|clear}`, `spring unit humans {add|remove|list}`, `spring unit create-from-template` as first-class verb (currently only a `--from-template` flag).
- **Unblocks:** [#393](https://github.com/cvoya-com/spring-voyage/issues/393), [#411](https://github.com/cvoya-com/spring-voyage/issues/411).

### PR-C3: Analytics + cost + agent-clone CLI

- **Closes:** [#457](https://github.com/cvoya-com/spring-voyage/issues/457), [#459](https://github.com/cvoya-com/spring-voyage/issues/459), [#458](https://github.com/cvoya-com/spring-voyage/issues/458)
- **Prerequisites:** none. Analytics verbs depend on `StateChanged` events carrying duration-computable data — verify before writing the waits command. If gaps exist, file a backend issue and scope the CLI to available fields.
- **Size:** medium
- **Scope:** `spring analytics costs --window 7d`, `spring analytics throughput --unit <name>`, `spring analytics waits --agent <name>`, `spring cost set-budget`, `spring agent clone create`.
- **Unblocks:** [#394](https://github.com/cvoya-com/spring-voyage/issues/394), [#448](https://github.com/cvoya-com/spring-voyage/issues/448).

### PR-C4: Connector CLI

- **Closes:** [#455](https://github.com/cvoya-com/spring-voyage/issues/455)
- **Prerequisites:** none
- **Size:** small-medium (own domain, small verb family)
- **Scope:** `spring connector catalog`, `spring connector bind --unit <name> --type <type>`, `spring connector show --unit <name>`.
- **Unblocks:** [#449](https://github.com/cvoya-com/spring-voyage/issues/449).

---

## Track PLAT — Platform (runs in parallel with Wave 0 + Wave 1)

These are roadmap items that don't touch portal or CLI domain logic and are therefore orthogonal to the portal redesign. Each is a single large deliverable per PR.

### PR-PLAT-RUN-1: Ollama-driven agent runtime

- **Closes:** [#334](https://github.com/cvoya-com/spring-voyage/issues/334)
- **Prerequisites:** none
- **Size:** large (multi-file, new runtime integration)
- **Scope:** First-class local/OSS agent runtime using Ollama. Adds an Ollama-backed `IAgentToolLauncher` and the supporting configuration. Unblocks the LLM-backed e2e scenario pool under `tests/e2e/scenarios/llm/`.
- **Key files:** new launcher under `src/Cvoya.Spring.Runtime.Ollama/` (or similar — match existing launcher layout); DI wiring; config schema; docs/architecture/workflows.md update.
- **Acceptance:** Ollama launcher registered; at least one existing LLM scenario under `tests/e2e/scenarios/llm/` passes against a local Ollama server.

### PR-PLAT-RUN-2: spring-agent container image + persistent-agents CLI

- **Closes:** [#390](https://github.com/cvoya-com/spring-voyage/issues/390), [#396](https://github.com/cvoya-com/spring-voyage/issues/396)
- **Prerequisites:** none (bundled — the CLI verbs operate on the containers this PR builds).
- **Size:** medium-large
- **Scope:** `Dockerfile.spring-agent` that bakes Claude Code into the runtime image; CI workflow that builds + publishes to `ghcr.io/cvoya/spring-agent:<version>` (ties in with [#433](https://github.com/cvoya-com/spring-voyage/issues/433) — coordinate tag scheme). `spring persistent deploy|status|scale|logs|undeploy` CLI verbs that drive the persistent-agent lifecycle.
- **Acceptance:** Image builds green in CI and publishes on tag. CLI verbs drive the full lifecycle against a local Podman; e2e scenario `fast/<NN>-persistent-lifecycle.sh` covers deploy → status → scale → logs → undeploy.

### PR-PLAT-RUN-3: Agents automatically exposed as skills

- **Closes:** [#359](https://github.com/cvoya-com/spring-voyage/issues/359)
- **Prerequisites:** none (dependency-light; may touch the same DI surface as PR-PLAT-RUN-1 — coordinate ordering if both are in flight).
- **Size:** medium
- **Scope:** Every agent exposes itself as a skill to sibling agents in the same unit. Auto-registration path; permission model for cross-agent calls.
- **Acceptance:** Integration test proves agent A can invoke agent B via the skill resolver; skill listing includes auto-exposed agents.

### PR-PLAT-ORCH-1: Label-based orchestration strategy

- **Closes:** [#389](https://github.com/cvoya-com/spring-voyage/issues/389)
- **Prerequisites:** none
- **Size:** medium-large
- **Scope:** Label-based dispatch — route messages to agents based on their registered labels. v1 parity with the pre-V2 behaviour. Surfaces a new policy dimension (label) that needs representation in the Policies surface (§5.6 of portal exploration); coordinate with PR-C2 author so the `spring unit policy` CLI includes label routing when this lands.
- **Acceptance:** `DefaultOrchestrationStrategy` resolves label-based routing; unit template can specify labels; e2e scenario covers label routing.

### PR-PLAT-OBS-1: Rx.NET activity pipeline end-to-end

- **Closes:** [#391](https://github.com/cvoya-com/spring-voyage/issues/391)
- **Prerequisites:** none
- **Size:** medium-large
- **Scope:** Complete the observable graph wiring from `ActivityEventPersister` through to SSE subscribers. Close any gaps in `StateChanged` event emission (previous/current state, consistent timestamps) so the Analytics surface (#448) can compute wait times reliably.
- **Acceptance:** Every `AgentActor` state transition emits a complete `StateChanged` event; SSE stream delivers events within 1s of emission; an e2e scenario asserts a full message → state → tool call → cost event chain.

### PR-PLAT-BOUND-1: Expertise directory aggregation

- **Closes:** [#412](https://github.com/cvoya-com/spring-voyage/issues/412)
- **Prerequisites:** none directly; coordinate with PR-PLAT-BOUND-2 (both reshape directory behaviour).
- **Size:** medium-large
- **Scope:** Recursive composition of expertise from child units to root. Directory queries aggregate subtrees.
- **Acceptance:** `IDirectoryService.QueryExpertise(unit)` returns the recursive rollup; existing flat-directory consumers are unaffected.

### PR-PLAT-BOUND-2: Unit boundary — opacity, projection, filtering, synthesis

- **Closes:** [#413](https://github.com/cvoya-com/spring-voyage/issues/413)
- **Prerequisites:** none directly; coordinate with PR-PLAT-BOUND-1.
- **Size:** large
- **Scope:** Implement the four boundary behaviours on unit actors — opacity (what's visible), projection (what sub-units expose upward), filtering (what passes through boundaries), synthesis (aggregated views).
- **Acceptance:** Each behaviour has integration coverage; existing flat-unit consumers continue to work (boundaries are opt-in per unit).

### PR-PLAT-PKG-1: Package browser CLI + portal (Wave 2-adjacent)

- **Closes:** [#395](https://github.com/cvoya-com/spring-voyage/issues/395)
- **Prerequisites:** Wave 0 foundation (#440 extension slots, #438 TanStack) for the portal half. The CLI half has no dependency.
- **Size:** medium (CLI) + medium (portal) — consider splitting into two PRs if the bundle runs large.
- **Scope:** Package catalogue browsing surface. Discovery via the local registry (future #422 work is orthogonal — this PR ships browsing for whatever the registry already knows).
- **Acceptance:** `spring package list` + `spring package show <name>`; portal `/packages` top-level route with list + detail.

---

## Track S — Portal surfaces (Wave 2)

Four PRs. Each takes a foundation + CLI bundle and ships a new top-level route. Unblocked as the relevant Wave 0 and Wave 1 PRs land.

### PR-S1: Nav restructure + Agents lens + Settings drawer + Inbox

- **Closes:** [#444](https://github.com/cvoya-com/spring-voyage/issues/444), [#450](https://github.com/cvoya-com/spring-voyage/issues/450), [#451](https://github.com/cvoya-com/spring-voyage/issues/451), [#447](https://github.com/cvoya-com/spring-voyage/issues/447)
- **Prerequisites:** PR-F1, PR-F2, PR-F3, PR-F5; PR-C1 (for Inbox).
- **Size:** large (acceptable — these four must land coherently to avoid a broken sidebar midway).
- **Scope:** Replace the sidebar per § 3.2 (Dashboard / Inbox / Units / Agents / Conversations / Activity / Analytics / Policies / Connectors / Packages / Settings). Implement four of the new top-level routes (Agents lens, Settings drawer, Inbox, placeholder redirects for not-yet-shipped surfaces). Move `/budgets` under `/analytics/costs` as a 301 redirect.
- **Acceptance:** Sidebar matches the design doc. Each route exists (or redirects). No broken links.

### PR-S2: Analytics surface

- **Closes:** [#448](https://github.com/cvoya-com/spring-voyage/issues/448)
- **Prerequisites:** PR-F2, PR-F3, PR-F5; PR-C3; PR-PLAT-OBS-1 (wait times need reliable `StateChanged`).
- **Size:** medium-large
- **Scope:** `/analytics` with Costs / Throughput / Wait times tabs per § 5.7. Range picker. Progressive loading (stream each widget in as its query resolves).
- **Acceptance:** All three tabs render real data from the activity bus; range picker applies across tabs; keyboard + a11y pass.

### PR-S3: Connectors browser

- **Closes:** [#449](https://github.com/cvoya-com/spring-voyage/issues/449)
- **Prerequisites:** PR-F2, PR-F3, PR-F5; PR-C4.
- **Size:** medium
- **Scope:** `/connectors` top-level. Catalogue of available connector types. Per-connector detail with bind targets and configuration. Deep-links into the unit's Connector tab.
- **Acceptance:** List renders all connector packages discovered by the catalogue. Detail pages render configuration shape for the GitHub connector (the only one shipped today).

### PR-S4: Packages browser (portal half)

- **Closes:** part of [#395](https://github.com/cvoya-com/spring-voyage/issues/395) if PR-PLAT-PKG-1 was split
- **Prerequisites:** PR-F2, PR-F3, PR-F5; PR-PLAT-PKG-1 CLI half.
- **Size:** medium
- **Scope:** `/packages` top-level. List installed packages; detail for each.

---

## Track R — Portal refits (Wave 3)

Five PRs. Update existing portal features to match the redesign. Unblocked as matching CLI + Wave 2 surfaces + Phase 5 boundaries land (per each entry's `blockedBy`).

### PR-R1: Conversation UI (#410)

- **Prerequisites:** PR-F2 (streaming), PR-F5 (breadcrumbs); PR-C1 (conversation CLI).
- **Size:** large
- **Scope:** Full conversation list + detail pages per § 5.3. Role attribution, tool-call summaries, streaming via the new infrastructure. Reply compose box. Activity-vs-Conversations cross-linking.

### PR-R2: Drill-down views (#392)

- **Prerequisites:** PR-F5 (breadcrumbs).
- **Size:** large
- **Scope:** Unit, agent, and conversation detail pages refactored with breadcrumb navigation, object-card primitives, and cross-links per § 3.3.

### PR-R3: RBAC management (hosted-only, #393)

- **Prerequisites:** PR-F3 (extension slots); PR-C2 (spring unit humans CLI); PR-R2 (drill-down).
- **Size:** medium
- **Scope:** Hosted-only extension exposing invite / change-role / remove / audit flows at `/units/[id]/members`. OSS route stays empty (extension slot).

### PR-R4: Cost rollup → Analytics Costs tab (#394)

- **Prerequisites:** PR-C3 (cost CLI); PR-S2 (Analytics surface must exist).
- **Size:** medium
- **Scope:** Refit the existing `/budgets` page into the Analytics → Costs tab (already promoted under PR-S2). This PR closes out the rollup + per-agent attribution requirements from #394 that aren't covered by the shell in PR-S2.

### PR-R5: Autonomy + all-5-dimensions Policies (#411)

- **Prerequisites:** PR-C2 (spring unit policy CLI).
- **Size:** large
- **Scope:** Policies portal surface covering all five `UnitPolicy` dimensions (§ 5.6). Initiative becomes a sub-view. Budget visualization. Effective-policy rollup.

---

## Track Q — Polish (Wave 4)

### PR-Q1: Responsive / mobile pass

- **Closes:** [#445](https://github.com/cvoya-com/spring-voyage/issues/445)
- **Prerequisites:** Wave 2 surfaces stable (S1–S3).
- **Size:** medium
- **Scope:** Apply the rules in § 6 across the portal. Physical-device check.

### PR-Q2: Accessibility audit and regression smoke tests

- **Closes:** [#446](https://github.com/cvoya-com/spring-voyage/issues/446)
- **Prerequisites:** Wave 2 surfaces stable.
- **Size:** medium
- **Scope:** Execute the § 7 checklist. Add Vitest browser mode or Playwright for keyboard / screen-reader regression.

---

## Phase 5 — Nesting + boundaries

Runs alongside Wave 3. Two of the four Phase-5 items depend on portal changes; the other two are platform-only.

### PR-PLAT-BOUND-3: Hierarchy-aware permission checks

- **Closes:** [#414](https://github.com/cvoya-com/spring-voyage/issues/414)
- **Prerequisites:** PR-PLAT-BOUND-1 (directory), PR-PLAT-BOUND-2 (boundaries).
- **Size:** large
- **Scope:** Permission checks walk the unit hierarchy, honouring boundaries.

### PR-PLAT-INIT-1: Proactive + Autonomous initiative levels

- **Closes:** [#415](https://github.com/cvoya-com/spring-voyage/issues/415)
- **Prerequisites:** PR-R5 (autonomy UI; #411 closes #415's dependency).
- **Size:** large
- **Scope:** Ship the two top initiative levels with their approval-gate + cost-cap behaviour.

### PR-PLAT-CLONE-1: Persistent cloning policy

- **Closes:** [#416](https://github.com/cvoya-com/spring-voyage/issues/416)
- **Prerequisites:** PR-PLAT-BOUND-2 (boundaries affect clone projection).
- **Size:** large
- **Scope:** Independent clone evolution; recursive cloning.

---

## Phase 6 — Platform maturity

### PR-PLAT-PKG-2: Package system (local registry, install, versioning)

- **New issue needed** — not filed yet. File during Wave 2 when the portal Packages surface crystallises the UX contract.
- **Prerequisites:** PR-PLAT-PKG-1, PR-S4.
- **Size:** large

### PR-PLAT-DOMAIN-1: Research domain package + additional connectors

- **Closes:** [#417](https://github.com/cvoya-com/spring-voyage/issues/417)
- **Prerequisites:** none platform-side; PR-S3 (connectors browser) for the portal integration story.
- **Size:** large

---

## Dispatch brief template (for implementer agents)

Use this shape when handing a PR to an implementer agent. It matches the prompts we've used for the foundation batch.

```
You are implementing <PR-ID>: <short title> in the spring-voyage repo
(https://github.com/cvoya-com/spring-voyage). Work in the isolated
worktree you've been given. Read `docs/roadmap/execution-plan.md` for
the entry, then `gh issue view <N>` for each issue it closes.

Mandatory reading: AGENTS.md, CONVENTIONS.md, docs/design/portal-exploration.md
(for portal work), docs/architecture/*.md (for the subsystem you're touching).

Branch: base on latest origin/main.
  git fetch origin && git checkout -b <branch> origin/main

Pre-push CI (mandatory per ~/.claude/CLAUDE.md):
  dotnet build
  dotnet test --solution SpringVoyage.slnx --no-restore --no-build --configuration Release
  dotnet format --verify-no-changes
  Any workflow-level lints under .github/workflows/.
  For portal PRs: npm --prefix src/Cvoya.Spring.Web run build/test/lint.

Push + PR via the GitHub App (savasp-agent), not personal credentials.
Title under 70 chars. Body: one `closes #<N>` line per issue. Reviewer
and assignee: `savasp`. Enable auto-merge after creation.

Doc updates (per #424 convention): update architecture / guide / DESIGN.md
as the change warrants.

Return: PR URL, files changed (short list), CI confirmation, anything
deliberately deferred.
```

Worktree isolation: spawn the agent with `isolation: "worktree"`. Parallel dispatch: call the `Agent` tool with `run_in_background: true` in a single message with multiple entries when the PRs are independent.

## Coordinator agent brief

A coordinator agent owns a **wave** or a **track**. Its job:

1. Read its section of this document.
2. For each PR in its scope with all prerequisites merged (check via `gh pr view` + the GitHub dependency graph), spawn an implementer agent using the template above.
3. When an implementer returns, record the PR number under **State** below via PR-editing this document (one-line amend).
4. If a dependency blocker closes, re-evaluate the wave — new PRs may now be unblocked.
5. Do not start work outside its wave/track without explicit authorization.

Coordinators themselves are spawned in parallel by the top-level human or top-level agent. One coordinator per wave/track is the default; split further only if a track is too large for one agent's context window.

## State (maintained as work proceeds)

> Update this table each time a PR is filed or merged. Truth-source: GitHub issue-dependency graph. This table is a convenience dashboard.

| PR ID | Status | PR # | Merged |
|---|---|---|---|
| PR-F1 | not started | — | — |
| PR-F2 | not started | — | — |
| PR-F3 | not started | — | — |
| PR-F4 | not started | — | — |
| PR-F5 | not started | — | — |
| PR-C1 | not started | — | — |
| PR-C2 | not started | — | — |
| PR-C3 | not started | — | — |
| PR-C4 | not started | — | — |
| PR-PLAT-RUN-1 | not started | — | — |
| PR-PLAT-RUN-2 | not started | — | — |
| PR-PLAT-RUN-3 | not started | — | — |
| PR-PLAT-ORCH-1 | not started | — | — |
| PR-PLAT-OBS-1 | not started | — | — |
| PR-PLAT-BOUND-1 | not started | — | — |
| PR-PLAT-BOUND-2 | not started | — | — |
| PR-PLAT-PKG-1 | not started | — | — |
| PR-S1 | blocked | — | — |
| PR-S2 | blocked | — | — |
| PR-S3 | blocked | — | — |
| PR-S4 | blocked | — | — |
| PR-R1 | blocked | — | — |
| PR-R2 | blocked | — | — |
| PR-R3 | blocked | — | — |
| PR-R4 | blocked | — | — |
| PR-R5 | blocked | — | — |
| PR-Q1 | blocked | — | — |
| PR-Q2 | blocked | — | — |
| PR-PLAT-BOUND-3 | blocked | — | — |
| PR-PLAT-INIT-1 | blocked | — | — |
| PR-PLAT-CLONE-1 | blocked | — | — |
| PR-PLAT-PKG-2 | not filed | — | — |
| PR-PLAT-DOMAIN-1 | blocked | — | — |

## Amendments

Material changes to this plan happen via PR against this file. Small status updates can be inline edits to the State table on `main`. Large changes (new wave, new track, re-ordered dependencies) go through review.

When an assumption in this plan conflicts with the GitHub issue-dependency graph, **the graph wins**. Update this doc to match.
