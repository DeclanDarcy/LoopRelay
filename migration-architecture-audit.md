# Migration Architecture Audit — Realizing `app.dc.html` in the Command Center Frontend

**Status:** Migration & component-architecture audit. Not a plan. No roadmap, milestones, APIs, schemas, or code.
**Companion to:** `audit.md` (the adaptation/discovery audit at repo root).
**Purpose:** Provide the missing migration, information-architecture, component-architecture, and sequencing analysis required to author an excellent frontend `plan.md` that realizes `app.dc.html` inside the existing Command Center frontend.

**Authority assumption (per brief):** `app.dc.html` is the **UX / UI / Layout / Interaction authority** and the **target experience**. Where a target element is mock data, placeholder, fake statistic, or a nonexistent backend capability, **real backend authority wins**. The question is not *whether* the target is right — it is **how to realize it safely**.

**What this audit does not repeat.** The prior `audit.md` already established the current frontend state, backend projection catalog, the Exists/Partial/Missing/Conflicts **mapping matrix**, architectural boundaries, UX debt, generic risks, and adaptation opportunities. Those are inputs here, cited but not re-derived. This audit covers only **migration strategy, information architecture, component architecture, and sequencing constraints**.

**New evidence this audit adds (first-hand):**
- *Structural map of `App.tsx`* (single ~3,523-line component): 32 state hooks, 11 effects, 6 transport sites, and **3 CRITICAL coupling cascades**.
- *The target's interaction model* read from the `DCLogic` script (`app.dc.html:391–605`): the entire UI is driven by **four client-state atoms** over **synchronously-derived in-memory data** — no async, no loading, no effects, no streaming.

The decisive insight of this audit follows from that pair: **the target and the implementation render the same conceptual surface but live on opposite ends of a reactivity gap.** Most migration risk is the temptation to copy the target's synchronous, four-atom model onto a system whose data is asynchronous, shared-across-views, and backend-authoritative.

---

## Executive Summary

**Recommended migration strategy: Hybrid (Strategy C) — foundations-first, then a new shell, then panel-by-panel migration, retiring `App.tsx` last.** Pure in-place refactor (A) stacks three transforms — decouple, restyle, recompose — onto one fragile 3,500-line file simultaneously; it has the highest regression probability and the weakest architectural alignment. A clean new-shell rebuild (B) aligns with the target's structure but risks re-implementing the monolith's hard-won async/effect logic from scratch in the new shell. The Hybrid extracts the dangerous shared state into a stable data layer **once, behavior-preserving, before any pixel changes**, then builds the target shell over it and ports panels incrementally behind a living reference. It captures B's architectural alignment while neutralizing A's regression surface.

**Largest migration risks (all rooted in the reactivity gap):**
1. **Synchronous-derivation drift (HIGH).** The target computes the whole screen in one `renderVals()` pass from always-present data. Copying that shape would collapse async projection loads into render-time client derivation — re-introducing the projection-only violation the architecture forbids, and erasing loading/error UX.
2. **Shared-projection-across-tabs drift (HIGH).** Execution and operational-context data appear on multiple tabs at different densities. If each tab loads its own copy (the monolith's instinct), the tabs silently drift. One shared per-repository projection source must feed all consumers.
3. **Fragile-cascade regression (HIGH).** Three CRITICAL `useEffect`/`useMemo` cascades in `App.tsx` (repo→workspace→diagnostics; SSE event-merge→milestone; proposal→reviewState gating) are exactly what a naïve decomposition fractures.

**Largest architectural opportunities:**
1. **Separate navigation state from projection state.** The target proves navigation is only four cheap, synchronous atoms; the implementation entangles navigation with data loading. Isolating the two is the highest-leverage structural move — it simultaneously yields the target's instant-feeling tab/repo switches *and* a clean decomposition seam.
2. **A shared, repository-keyed projection layer (`api.ts` + hooks + `types.ts`).** Converts the monolith's scattered inline transport and per-effect loads into one source the multi-tab IA requires anyway.
3. **The shell as the home of the new information architecture.** A purpose-built `AppShell` (sidebar + two-axis nav + inspector rail + palette) makes the target's density and simultaneity native rather than retrofitted.

---

## Migration Strategy Analysis

Three strategies, scored on Complexity, Risk, Regression Probability, Architectural Alignment, Delivery Efficiency. Evidence: `App.tsx` is one ~3,523-line component with 32 state hooks, 11 effects, 6 transport sites, no extracted components, and three CRITICAL coupling cascades; the target's structure is a fixed-sidebar shell with two independent nav axes and an inspector rail.

### Strategy A — Refactor `App.tsx` in place → restyle → recompose layout
- **Complexity: High.** Three transforms (decouple state, apply design system, restructure DOM from list/detail to shell+inspector) all land on the same live file.
- **Risk: High.** Every change touches the file that owns the fragile cascades; there is no isolated reference to diff against.
- **Regression probability: High.** The repo→workspace→diagnostics cascade and the SSE-merge→milestone chain break precisely when their surrounding code is moved; restyle churn obscures behavioral diffs.
- **Architectural alignment: Low.** The current 2/3-column list/detail DOM resists morphing into the target's sidebar + `1fr 364px` inspector grid + two-axis tabs; in-place edits fight the existing tree.
- **Delivery efficiency: Low.** Entangled work cannot ship in safe increments; the file is unmergeable mid-flight.

### Strategy B — New workspace shell → migrate panels in → retire old
- **Complexity: Medium-High.** Build the shell chrome fresh against tokens, then port each panel.
- **Risk: Medium.** Main hazard is a long dual-stack window and re-implementing the monolith's async/effect logic from scratch (a second chance to get SSE/polling/loading wrong).
- **Regression probability: Medium.** Each ported panel is verifiable against the still-running old app, but logic re-authored in the shell can subtly diverge.
- **Architectural alignment: High.** Shell is built to the target's structure (sidebar, `tabA`/`tabC` axes, inspector, palette) from day one; the port forces `api.ts`/`types.ts` extraction as the seam.
- **Delivery efficiency: Medium-High.** Clean seams, incremental panel porting, graceful degradation if paused.

### Strategy C — Hybrid (recommended)
**Shape:** (1) extract cross-cutting **foundations first** — design tokens, `types.ts`, `api.ts`, and the three coupling hotspots lifted into behavior-preserving hooks — *while `App.tsx` still runs on them*; (2) stand up the **new shell** (B-style) over those foundations; (3) **migrate panels** into the shell one at a time, each diffed against the living monolith; (4) **retire `App.tsx`** last.
- **Complexity: Medium.** Same total work as B, but the riskiest part (state decoupling) is isolated as its own behavior-preserving step, not co-mingled with visual change.
- **Risk: Medium-Low.** The fragile cascades are tamed under existing behavior **before** any restyle; both old and new shells consume the *same* extracted hooks, so logic is authored once, not twice.
- **Regression probability: Medium-Low.** Decoupling is verified against current behavior before the DOM changes; panel ports are verified against the living reference after.
- **Architectural alignment: High.** Identical to B once foundations exist.
- **Delivery efficiency: High.** Foundations are reusable immediately; panels port independently and in parallel.

### Recommendation
**Adopt Strategy C.** It is B's destination reached through a de-risking on-ramp: extract-then-rebuild rather than rebuild-and-hope or refactor-in-the-fire. This directly honors the prior audit's guidance that `api.ts` and `types.ts` are *prerequisites* for clean decomposition and that effect/memoization behavior must be preserved through the restructure. Pure A is rejected (compounds three transforms on the fragile monolith); pure B is acceptable but accepts avoidable logic-duplication risk that C removes for the same total cost.

---

## Information Architecture Analysis

### Current IA (implementation reality)
- **Navigation model.** Predominantly **single-screen with workflow-driven progressive disclosure**: panels appear/expand as the workflow advances and as review/loading states gate visibility (`operationalContextProposal.review.reviewState` gates 10+ renders; commit appears only in the prepare→review→commit phase). Whatever Workspace/Execution/Operational/Continuity section state exists is secondary to this workflow sequencing. Navigation is *largely one-dimensional* — driven by where you are in the loop, not by free user choice.
- **Workspace model.** The "workspace" is the selected repository's detail expanded inline across a center/right region on one scroll surface; everything for a repo lives on one continuous screen.
- **Visibility model.** **Sequential.** Loading flags (8 of them) and workflow phase hide/show regions; the user sees mostly one stage at a time.
- **Context-switching model.** Switching repositories triggers an **effect cascade** (select → `loadWorkspace` → `loadDiagnostics`); switching "views" is workflow-driven. Switches are *load-bearing and asynchronous*.

### Target IA (`app.dc.html` authority)
- **Navigation model.** **Three independent axes**, all cheap client state: `selectedRepoId` (global repository context), `tabA` (primary view: Workspace / Execution / Operational context / Continuity), and `tabC` (artifact-tree selection on the Operational tab) — plus **⌘K command palette** as a jump axis. The global sidebar nav (Overview / Repositories / Executions / Insights) is present but **inert in the mock** (no `onClick` wiring in `DCLogic`), confirming the prior audit's "conflicts/speculative" classification.
- **Workspace model.** A **tabbed dense console**: a persistent identity header (name, state pill, path, branch, stat tiles) + a 5-step **workflow rail** + a tab body. The Workspace tab itself is a **two-column composite** (`grid-template-columns: 1fr 364px`): a left main column and a permanent right **inspector rail**.
- **Visibility model.** **Simultaneous.** On the Workspace tab, execution context + live activity + milestones + commit/push + operational-context card + execution history are **all visible at once**; the inspector keeps commit/push and op-context permanently on screen.
- **Context-switching model.** **Instantaneous and client-only** in the mock — every switch is a `setState` over already-derived data; there is no load, no spinner, no async.

### Delta — where information moves, merges, splits, becomes simultaneous
- **MOVES.** The repository list moves from *content* (a center/left panel) into *chrome* (the global sidebar). Commit/push and the operational-context summary move from inline-sequential center panels into a **persistent right inspector rail**, always visible regardless of workflow phase.
- **MERGES.** Execution context + live activity + milestones + commit/push + op-context card + history **merge onto one Workspace surface** (today sequential/separate). Each sidebar repo row merges state pill + dirty count + proposal-ready dot + language color into one dense row.
- **SPLITS.** The monolithic single screen **splits along two independent axes** — primary view (`tabA`) and artifact selection (`tabC`). Execution gets a **dedicated full-height tab** (deep view) *in addition to* its summary on the Workspace tab — so execution information is **presented at two densities in two places**.
- **BECOMES SIMULTANEOUS.** The workflow rail and every panel it indexes are co-visible; the inspector holds commit/push + op-context permanently alongside execution. This is the core IA shift and the literal expression of the prior audit's "density mismatch" debt: *users stop tabbing between things the target shows together.*

### The two IA findings that constrain everything downstream
1. **Navigation collapses to ~4 synchronous atoms; data does not.** The target proves the *navigation* problem is trivial (four `setState` values). The hard part is that those atoms must sit **over an asynchronous, shared projection cache**, not over synchronously-derived data. Realizing the target's instant-switch feel without violating projection-only means **pre-loading / caching projections and keeping switches client-only** — never deriving data at render time.
2. **The same projection feeds multiple simultaneous surfaces.** Execution session and operational context each appear on ≥2 tabs at once. This forbids per-tab independent loads (they would drift) and **mandates a single shared per-repository projection source** consumed by all views — which is also the natural decomposition seam.

---

## Canonical Frontend Architecture

*Architecture, not code.* Boundaries are derived from the target's layout/interaction (`app.dc.html`), the existing projection catalog (`audit.md`), and the `App.tsx` coupling map.

### Likely component hierarchy
```
AppShell                         ← owns GLOBAL CLIENT STATE only (the target's 4 atoms + theme)
├─ Sidebar                       ← chrome (was content)
│  ├─ ProductBadge
│  ├─ CommandLauncher            ← opens CommandPalette
│  ├─ GlobalNav                  ← Overview/Repositories/Executions/Insights (gated; see Risks)
│  ├─ RepositoryList
│  │  └─ RepositoryRow           ← state pill · dirty · proposal-ready dot · lang color
│  └─ UserProfile                ← decorative unless identity exists
├─ MainColumn
│  ├─ Topbar                     ← Breadcrumb · Refresh · NotificationsBell · RunPlan (gated)
│  ├─ WorkspaceIdentityHeader    ← name · state pill · path · branch · StatTiles
│  ├─ WorkspaceTabs              ← tabA axis
│  └─ TabBody (one of)
│     ├─ WorkspaceTab            ← WorkflowRail + (LeftColumn | InspectorRail)
│     │   ├─ LeftColumn          ← ExecutionContextPanel · LiveActivityLog · MilestonesList
│     │   └─ InspectorRail       ← CommitPushPanel · OperationalContextCard · ExecutionHistory
│     ├─ ExecutionTab            ← ExecutionStream(full-height) · SessionPanel · ContextDiagnostics
│     ├─ OperationalContextTab   ← CurrentUnderstanding | ProposedRevision (+ SemanticChanges, Counts,
│     │                            Accept/Edit/Reject) + ArtifactTree (tabC axis)
│     └─ ContinuityTab           ← EvolutionTable · ContinuityWarnings
└─ CommandPalette (overlay)      ← PaletteGroup → PaletteItem (navigation-only initially)
```

### Likely layout hierarchy
- **Root:** flex row — fixed sidebar (264px) + fluid `MainColumn` (flex column: fixed 48px topbar → fluid scroll body).
- **Scroll body:** identity header → tabs → tab content.
- **Workspace tab:** workflow rail (5-column grid) above a `1fr 364px` main/inspector grid.
- **Operational tab:** two-column current-vs-proposed, with the artifact tree as a secondary axis.
- **Palette:** fixed full-viewport overlay, escape/`⌘K` toggled.

### Natural state boundaries (the load-bearing decision)
Three strata, kept strictly separate — this separation is the architecture's spine:

1. **Navigation / shell state — client-only, synchronous.** `selectedRepositoryId`, `activePrimaryTab` (`tabA`), `activeArtifact` (`tabC`), `paletteOpen`, `theme`. In the target these are *literally the only state that exists.* They belong at `AppShell`, must be instantaneous, and must **never** be entangled with data loading.
2. **Projection state — server-authoritative, async, shared.** Dashboard list, workspace projection, execution session + event stream, operational-context projection + proposal, continuity diagnostics, git status, commit preparation. **Keyed by repository, shared across all tabs**, owned by a data layer (`api.ts` + hooks), bearing all loading/error/staleness concerns.
3. **Draft / form state — client-only, per-surface.** Artifact editor draft, commit message, proposal edit draft, review note. Local to one surface; must not leak upward into projection state.

The current `App.tsx` violates stratum (1)/(2) separation: selecting a repository (navigation) triggers an effect cascade that loads workspace then diagnostics (projection). **The canonical architecture isolates navigation as instant client state over an independently-loading, shared projection cache** — which is simultaneously how the target *feels* (instant) and how a clean decomposition *works* (one shared source, many views).

### Likely projection hierarchy (bind to existing projections; never client-derive)
| Surface | Backing projection (existing) |
|---|---|
| Sidebar repo rows, Execution history | `RepositoryDashboardProjection` (+ proposal-ready / continuity summary) |
| Identity header, stat tiles, milestones, workflow rail | `RepositoryWorkspaceProjection` over `RepositoryExecutionState` |
| Execution context panel + size gauge, Context diagnostics | `ExecutionContextPreview` + `ExecutionContextDiagnostics` + size policy |
| Live activity / execution stream, Session panel | Execution session + `ExecutionEvent` SSE stream |
| Op-context current understanding (grouped), Op-context card | `OperationalContextProjection` |
| Proposed revision, semantic changes, counts, review actions | `OperationalContextProposal` (+ semantic-change / compression types) |
| Continuity evolution table, warnings | `ContinuityDiagnostics` + trends + warnings |
| Commit/push inspector | `CommitPreparation` + `RepositoryGitStatus` |

**Projection gaps (resolve in backend or drop — never compute in UI):** ahead/behind counts (`gitLabel` in the mock), per-milestone criteria progress (`"4 / 6 criteria"`), abort capability, any cross-repo Overview/Executions/Insights read model, notifications feed.

---

## Dependency Analysis

### Must happen first (foundations — blocking, no UI parity risk)
1. **Design-token / theme layer** — extract `app.dc.html`'s CSS custom properties (`--bg-canvas`, `--surface-*`, `--fg-*`, `--accent-*`, status families, type scale, fonts) into real CSS. Blocks every restyle. The `DCLogic`/`sc-if`/`sc-for`/`support.js` runtime is **not portable** — only tokens + layout intent migrate.
2. **`types.ts` consolidation** — centralize the ~31 DTO types mirroring backend projections. Blocks `api.ts` and every component signature.
3. **`api.ts` transport centralization** — gather the scattered `invoke`/`fetch`/`EventSource` sites into one client. Blocks decoupling views from transport.
4. **State decoupling into shared hooks** — lift the three CRITICAL coupling cascades (`useRepositoryWorkspace`, `useExecutionSession`, `useOperationalContext`/`useContinuity`) behind stable, behavior-preserving APIs **before any visual change**. This is the single most important de-risking step and the gate for safe decomposition. It also establishes stratum (2) (shared projection state) that the multi-tab IA requires.

### Can happen in parallel (after foundations)
- **Shell chrome** (sidebar, topbar, identity header, tabs, workflow rail) — pure presentation over dashboard/workspace projections.
- **Individual panel ports** — execution context, live activity, milestones, commit/push, operational context, continuity. Each is independent once its hook + tokens exist; each is diffed against the living monolith.
- **Command palette (navigation-only)** — depends only on shell nav state + repo list; faithful to the mock, where run-actions are no-ops.

### Should happen last
- **Capability-gated affordances requiring backend work** — Abort execution (no endpoint today), ahead/behind counts (no projection), per-milestone criteria progress (no projection). Sequenced after the restyle so backend decisions never block the visual migration; rendered disabled/placeholder until backed.
- **Resolution of conflicting global nav** (Overview / Executions / Insights) — requires product + projection decisions; the shell ships repository-centric with these disabled/placeholder until resolved.
- **Retiring `App.tsx`** — only after every panel is ported and behavior-verified against it.

### Cross-cutting sequencing constraints
- `types.ts` → `api.ts` → hooks → shell/panels (strict order for the data spine).
- Tokens may proceed fully in parallel with the data spine (orthogonal concern).
- Palette run-actions are gated behind the **same** authority decisions as the topbar Run/Abort buttons — do not wire mutations into the palette ahead of those decisions.

---

## Migration Risk Analysis

Scoped to **migration**, and especially to the brief's target case: *where `app.dc.html` assumes a behavior the current implementation achieves differently.* Generic risks already in `audit.md` are not repeated.

### High risk
- **R1 — Synchronous-derivation vs async-projection drift.** *Target assumes:* the whole screen is one synchronous `renderVals()` over always-present data — no loading, no async, no errors. *Implementation achieves:* asynchronous projection loads via 11 effects, polling, SSE, and 8 loading/error flags. *Migration hazard:* copying the mock's shape collapses async loads into render-time client derivation — re-introducing the projection-only violation and erasing loading/error UX. *Constraint:* keep the data layer async behind hooks; achieve the target's instant feel via cached/preloaded projections, not synchronous derivation.
- **R2 — Shared-projection-across-tabs drift / duplication.** *Target assumes:* execution and operational-context data are simply present on multiple tabs at once. *Implementation achieves:* per-effect loads that, if duplicated per tab, silently diverge (one tab stale vs another). Maps to `App.tsx` coupling hotspot #3 (execution session + SSE event-merge + milestone). *Constraint:* one shared per-repository projection source feeds all consumers.
- **R3 — Fragile-cascade regression.** *Implementation reality:* three CRITICAL `useEffect`/`useMemo` cascades — repo→workspace→diagnostics; SSE-merge→milestone; proposal→`reviewState` gating — that a naïve split fractures. *Constraint:* extract behind hooks with characterization tests **before** restyle (the basis for recommending Strategy C).
- **R4 — Navigation-state vs workflow-state drift.** *Target assumes:* tab switches are free client state (`tabA`), independent of any backend transition. *Implementation achieves:* view visibility partly driven by workflow/review state. *Migration hazard:* free tab navigation may show a tab whose projection is invalid for the current workflow state, or worse, wire a tab switch to a backend transition. *Constraint:* tabs are presentation; the workflow rail **reads** `RepositoryExecutionState`, it never **drives** it; transitions stay explicit backend calls.

### Medium risk
- **R5 — Palette mutation / authority drift.** In the mock, palette "Run actions" (Start / Build context / Abort) are **no-ops** (`cmd(null)`); only navigation commands act. Wiring real mutations into the palette risks dispatching transitions outside single-workflow-authority (and Abort has no endpoint). *Constraint:* ship palette navigation-only; gate run-actions behind the same decisions as the buttons.
- **R6 — Projection-gap → client-derivation drift.** The mock hardcodes ahead/behind (`gitLabel`), per-milestone criteria (`"4 / 6"`), and per-repo `openQ`/`risks` as if trivially available. *Hazard:* realizing them tempts client-side git math or operational-context Markdown re-parsing. *Constraint:* each is a backend-projection decision or an omission — never a UI computation.
- **R7 — Affordance-without-capability drift.** Abort execution, Run plan (ambiguous vs Start execution), notifications bell, refresh are drawn as working. Abort has internal plumbing but **no endpoint/command/UI path**. *Constraint:* disabled/gated, not faked.

### Low risk
- **R8 — Two-axis navigation persistence drift.** Independent `tabA`/`tabC`/`selectedRepoId` axes replace a single conflated selection model; switching one axis must not unintentionally reset another (e.g., changing repo wiping artifact selection). Needs explicit per-axis retention rules.
- **R9 — Simultaneous-visibility performance cost.** The Workspace tab renders execution context + live SSE activity + milestones + commit/push + op-context + history at once, where today they are sequential; the monolith's 40+ inline `.map` loops re-rendering on every SSE event could regress. *Constraint:* memoize panels; isolate the SSE-driven subtree.
- **R10 — Runtime / theme non-portability.** `DCLogic`, `sc-if`/`sc-for`, `support.js`, and the external `_ds` bundle do not exist in-repo; only tokens + layout intent migrate. (Flagged in `audit.md`; restated as a migration constraint.)

---

## Open Questions

*Migration/IA/component-architecture questions specific to this audit. The eleven planning questions in `audit.md` (global nav scope, abort, Run-plan semantics, theme scope, palette mutation policy, criteria progress, ahead/behind, notifications, streaming ownership, behavior-parity bar, decomposition appetite) are inherited and still blocking — not re-derived here.*

1. **Tab-state persistence.** Should `tabA`/`tabC`/`selectedRepoId` persist across sessions, and should tab choice be remembered per-repository or reset on repo switch?
2. **Projection-cache lifetime.** When a non-selected repository's projections are loaded, are they kept warm (for the mock's instant switch-back feel) or evicted? What is the staleness/refresh policy for a shared cache?
3. **Dual-stack parity window.** During Strategy C's old-shell + new-shell coexistence, must each ported panel be byte-for-behavior identical to the monolith, and how long do the two coexist before `App.tsx` retires?
4. **State-management substrate.** Is extracting shared projection state into **custom hooks** acceptable as the seam, or is a store (e.g., Zustand/Query) wanted? Today there is no Redux/Zustand/React-Query — hooks are the lowest-disruption path, but the multi-tab shared cache raises the question explicitly.
5. **Inspector-rail validity.** Commit/push and the op-context card are *permanently* visible in the Workspace inspector — but they are meaningful only in certain `RepositoryExecutionState`s. What are their empty/disabled presentations when not applicable?
6. **Execution duplication contract.** Is the Execution tab a strict superset (deep view) of the Workspace execution summary sharing one source, or a distinct projection? (Determines whether R2's "single shared source" fully covers it.)
7. **Loading / empty / error vocabulary.** The mock has none. What is the intended skeleton/empty/error presentation per tab, given projections load asynchronously?
8. **Frontend verification safety net.** The backend has ~192 tests; the UI has no test surface noted. What is the per-panel acceptance/visual-parity gate that makes incremental migration safe, given the backend suite is UI-agnostic?

---

## Planning Guidance

### Key findings `plan.md` must incorporate
1. **The reactivity gap is the migration's central problem.** Target = 4 client-state atoms over synchronous derived data; implementation = 32 state hooks + 11 effects + SSE/polling. The plan must realize the target's *feel* (instant, simultaneous, dense) **without** adopting its *mechanism* (synchronous client derivation).
2. **Navigation state and projection state must be separated into distinct strata**, with a third for drafts. This is both the source of the target's instant switches and the safe decomposition seam.
3. **One shared, repository-keyed projection source feeds all tabs** — execution and operational context are surfaced simultaneously at multiple densities and will drift if loaded per-tab.
4. **Three CRITICAL coupling cascades in `App.tsx`** (repo→workspace→diagnostics; SSE-merge→milestone; proposal→reviewState) are the regression surface; they must be extracted behind characterized hooks before any restyle.
5. **The sidebar repo list migrates from content to chrome; commit/push and op-context migrate into a persistent inspector rail** — these are the load-bearing IA moves, not cosmetic ones.
6. **The palette and several affordances are decorative or capability-gapped in the mock** (palette run-actions are no-ops; Abort has no endpoint; ahead/behind, criteria, notifications, cross-repo nav have no projection). The plan must classify each as adopt-with-backing-projection, translate, gate-disabled, or omit.

### Architectural decisions `plan.md` must respect
- **Projection-only UI** — every surface fed by a backend projection; no client-side aggregation, derivation, or operational-context Markdown re-parsing.
- **Backend authority** over all state and workflow transitions; the **two distinct execution state models** (`ExecutionSessionState` provider lifecycle vs `RepositoryExecutionState` workflow) stay separate and backend-owned.
- **Single workflow authority** — no new sessions/routers/parallel state machines; **no client-held workflow state.** Tabs and the workflow rail are *read-only projections* of state, never drivers of it.
- **Review-before-mutation** for operational context — Accept/Edit/Reject/Promote stay explicit; no auto-generate/auto-promote as a navigation or render side effect.
- **Canonical `OperationalContextDocument`** is the only basis for continuity rendering.
- **Repository filesystem authority** — `.agents/` artifacts remain source of truth.
- **Thin Tauri shell** — SSE stays UI→backend direct unless routing through the shell is taken as a deliberate, explicit boundary decision.

### Recommended sequencing constraints
- **Foundations strictly precede shell and panels:** tokens (parallel) and the data spine `types.ts → api.ts → shared hooks` (ordered). The state-decoupling step (hotspot extraction) is **behavior-preserving and lands before any visual change** — this is the C-strategy on-ramp and the chief regression control.
- **Shell chrome and panel ports run in parallel** once foundations exist, each verified against the still-living monolith; **`App.tsx` retires last.**
- **Capability-gapped and conflicting surfaces are sequenced last** (Abort, ahead/behind, criteria progress, global nav, notifications) and ship disabled/placeholder until their backend/product questions resolve — they must never block the visual migration.
- **Palette ships navigation-only first** (faithful to the mock); mutations are gated behind the same authority decisions as the equivalent buttons.

---

*Audit complete. This document analyzes migration strategy, information architecture, component architecture, and sequencing constraints only. It does not propose a roadmap, milestones, `plan.md`, APIs, schemas, or implementation steps — those belong to a subsequent `plan.md` it is intended to inform.*
