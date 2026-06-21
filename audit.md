# Audit — Adapting `app.dc.html` UX into the Command Center Frontend

**Status:** Discovery & adaptation audit. Not a plan. No roadmap, milestones, APIs, schemas, or code are proposed here.
**Purpose:** Capture the evidence needed to later author a high-quality `plan.md` that integrates the UX expressed in `app.dc.html` into the *existing* Command Center frontend **without violating the architecture ratified by Epic 3** (artifact-mediated continuity, backend authority, projection-only UI).

**Primary question this audit serves:**
> How should the UI/UX expressed in `app.dc.html` be adapted into the existing frontend architecture without violating established architectural boundaries?

**Framing assumption (per the brief):**
`app.dc.html` = *target UX*. The current frontend = *implementation reality*. The goal is **adaptation, not literal recreation** — translating `app.dc.html`'s concepts into the real application while preserving backend authority.

---

## Executive Summary

**Current frontend maturity — high (functionally), low (structurally/visually).**
The existing UI (`src/CommandCenter.UI/src/App.tsx`, a single ~3,500-line component) already implements **nearly every concept** present in `app.dc.html`: repository dashboard, the full `RepositoryExecutionState` workflow, execution-context preview with size diagnostics, live execution event streaming, handoff review, commit/push, operational-context proposal generation/review/edit/accept/reject/promote, and continuity diagnostics (trends + warnings). It is wired end-to-end to a real .NET backend through 38 Tauri commands plus direct HTTP/SSE. It is **not** a scaffold.

**Relationship between `app.dc.html` and the current frontend.**
`app.dc.html` is **predominantly a visual + structural redesign of an already-implemented conceptual surface**, not a request for new product capability. Roughly 80% of its panels map directly onto data the backend already projects. Its repository state set, readiness states, artifact roles, execution-context size model, operational-context sections, semantic-change types, and continuity trend columns are **near-identical** to the backend's existing domain — strong evidence the mockup was drawn against this system. The decisive differences are *presentation* (a polished dark "Kernritsu · Compass" design system vs. today's hardcoded light vanilla CSS) and *composition* (a tabbed, dense inspector workspace + command palette vs. today's monolithic single-screen `App.tsx`).

**Largest adaptation opportunities.**
1. **Design-system adoption** — extract `app.dc.html`'s CSS custom-property token set (color, surface, type scale) into the real app; this is the single highest-value, lowest-architectural-risk change.
2. **Component decomposition** — `app.dc.html`'s tab/panel structure matches the component breakdown **Epic 3's own plan already specified** (`RepositoryDashboard.tsx`, `RepositoryWorkspace.tsx`, `ExecutionContextPanel.tsx`, `OperationalContextSurface/ProposalPanel/ReviewPanel.tsx`, `ContinuityDiagnosticsPanel.tsx`, `api.ts`, `types.ts`) but which `App.tsx` never realized. The redesign is a natural occasion to pay down that debt.
3. **Command palette (⌘K)** — a pure presentation layer over existing backend-authoritative actions; high perceived value, near-zero architectural impact.

**Largest adaptation risks.**
1. **Global navigation conflict.** `app.dc.html`'s left-nav (`Overview`, `Executions`, `Insights`) implies **cross-repository, top-level workspaces**. Epic 3 explicitly forbids separate top-level continuity workspaces and mandates a repository-centric, projection-only UI. Adopting the global nav *literally* risks introducing client-side aggregation and views with no backing projection.
2. **Affordances implying capabilities that aren't exposed.** `app.dc.html`'s **"Abort execution"** button has internal backend plumbing (`Cancelled` state, `Cancellation` event) but **no endpoint, no shell command, no UI path** today. Drawing the button does not make the capability exist.
3. **Design-system runtime is non-portable.** `app.dc.html` depends on an external `_ds/kernritsu-design-system-*` CSS bundle, a `support.js` runtime, and a `DCLogic`/`sc-if`/`sc-for` template engine — **none of which exist in the repo**. Only the *tokens and layout intent* are portable; the runtime and template syntax must be re-expressed in React.

---

## Current Frontend State

**Stack & shell.** React 19 + Vite + TypeScript, packaged as a Tauri v2 desktop app. The Tauri shell (`src/CommandCenter.Shell/src/main.rs`, ~969 lines) is a **thin synchronous HTTP proxy** to a spawned .NET backend (`http://127.0.0.1:5000`); it owns window lifecycle, native file dialogs, and backend process start/stop only. It does **not** stream, cache, or hold workflow state.

**Existing screens (all tabs within one repository-centric screen — `App.tsx`):**
- **Repository dashboard / list** — registered repos with availability, readiness, execution-state badges (left panel).
- **Artifacts** — plan, operational context, milestones, handoff, decisions inventory + Markdown editor with draft tracking + rotation.
- **Execution context** — preview/build with size diagnostics, validation, launch.
- **Execution session** — live status, event log (HTTP poll + `EventSource` SSE), workflow steps.
- **Handoff review** — accept/reject.
- **Commit review** — scope selection, message, file diff.
- **Push review** — branch/remote confirm, publish.
- **Operational context** — proposal generate, semantic-change review, edit, accept/reject, promote.
- **Continuity diagnostics** — trends (architecture/constraints/decisions/risks), compression summary, warnings, report generation.

**State ownership.** Plain React hooks (~27 `useState`, no Redux/Zustand/React Query). All authoritative data arrives from backend projections via `invoke<T>()`; the UI holds only presentation/draft/loading state. **This already honors projection-only.** ~31 TypeScript types mirror backend projections (`RepositoryDashboardProjection`, `RepositoryWorkspaceProjection`, `ExecutionContextPreview`, `OperationalContextProposal`, `ContinuityDiagnostics`, etc.).

**User journeys today.** Register repo → open workspace → inspect/edit artifacts → preview & start execution → watch live stream → review handoff → review/promote operational-context proposal → commit → push → inspect continuity. This is the **same loop** `app.dc.html` depicts.

**Styling.** Vanilla CSS in `App.css`/`index.css`, light theme, colors hardcoded inline, **no design tokens file**. No component library, no router.

---

## `app.dc.html` UX Inventory

**Frame.** `data-product="compass"`, screen label "Command Center — Console". Fixed 264px sidebar + fluid main, dark canvas. Built on the Kernritsu design system + a `DCLogic` component class with `sc-if`/`sc-for` template directives and a `support.js` runtime (prototype-only).

**Global chrome.**
- **Command palette (⌘K / Ctrl-K)** — modal search + grouped runnable commands ("Run actions": Start execution / Build execution context / Abort execution; "Workspace": Review proposal / Open continuity / View execution stream; "Go to repository": repo switcher). `Esc` closes.
- **Sidebar** — product badge ("Kernritsu · Compass / Command Center"); search/command launcher; **global nav: Overview, Repositories (active), Executions (live pulse), Insights**; repository list (lang-color dot, name, branch, state pill, dirty count, **proposal-ready dot**); user profile footer ("R. Kessler / Staff Engineer").
- **Topbar** — breadcrumb (Repositories ▸ repo), refresh, notifications bell (with dot), **"Run plan"** primary button.

**Workspace identity header.** Repo name + state pill; path; branch; stat tiles (**milestones**, **changed**, **ctx revs**).

**Four workspace tabs.**

1. **Workspace** —
   - **Workflow rail** (5 steps: Context → Execution → Handoff → Commit → Push) with `complete/current/pending/blocked` styling.
   - **Execution context** panel: artifact rows (role, path, per-artifact size bar, KiB) + **aggregate gauge "30.0 / 128 KiB · within limits"**; Rebuild / Start execution.
   - **Live activity** log: timestamped event stream (kind-colored), provider + pid, session id, blinking cursor.
   - **Milestones** list (dot, name, criteria progress, status).
   - **Commit & push** (right inspector): ahead/behind (↑2 ↓0), scope/changes (M/A type marks), commit message, Commit/Push.
   - **Operational context** quick card: counts (revisions / open questions / active risks / stable decisions) + **"proposal ready"** pill + "Review proposal →".
   - **Execution history**: prior sessions (state dot, milestone, commit sha, duration).

2. **Execution** — full-height codex execution stream ("executing" pulse), **Session** panel (provider, pid, milestone, started, elapsed, **Abort execution**), **Context diagnostics** (aggregate KiB, validation errors, launch-not-blocked).

3. **Operational context** — **Current understanding** grouped by Architecture / Constraints / Stable decisions / Open questions / Active risks (rev label) | **Proposed revision**: semantic changes (`DecisionAdded`, `ConstraintAdded`, `QuestionResolved`, `RationaleLostWarning`), counts (+added / kept / resolved / warning), **Accept / Edit / Reject**.

4. **Continuity** — **Understanding evolution** table (Section × Added / Removed / Resolved / **Lost**) + **Continuity warnings** (rationale-not-restated, risk-unresolved-across-N-revisions).

**Information surfaces (data shapes in the DC script):** repo states `Ready / Executing / AwaitingAcceptance / Accepted / AwaitingCommit / AwaitingPush / Failed / Cancelled`; readiness `Ready / MissingMilestones`; artifact roles `Plan / Milestone / Operational context / Handoff / Decisions`; size model 30/128/512 KiB; artifact tree under `.agents/`.

---

## Mapping Matrix

Status legend: **Exists** (functionally present, may need restyle) · **Partial** (some pieces present, gaps remain) · **Missing** (no current surface) · **Conflicts** (literal adoption clashes with ratified architecture).

| `app.dc.html` Concept | Current Equivalent | Status | Notes |
|---|---|---|---|
| Repository sidebar list (lang dot, branch, state pill, dirty count) | `RepositoryDashboardProjection` list in `App.tsx` | **Exists** | Restyle only; data already projected. |
| Repository **proposal-ready dot** | `RepositoryContinuitySummary.PendingProposalExists` / `OperationalContextProposalSummary` | **Exists** | Already in dashboard projection. |
| Repo **state pills** (8 states) | `RepositoryExecutionState` (identical 8 states) | **Exists** | Exact match; pure visual mapping. |
| **Readiness** (Ready / MissingMilestones) | `ExecutionReadiness` (MissingPlan / MissingMilestones / Ready) | **Exists** | Target omits `MissingPlan`; superset already supported. |
| Workspace identity header + stat tiles (milestones/changed/ctx revs) | `RepositoryWorkspaceProjection` (MilestoneCount, git dirty, revision count) | **Exists** | "ctx revs" = `OperationalContextProjection.CurrentRevisionNumber`. |
| Tabs: Workspace / Execution / Operational / Continuity | Tab state in `App.tsx` | **Exists** | Target regroups existing tabs; re-composition, not new data. |
| **Workflow rail** (Context→Execution→Handoff→Commit→Push) | `getExecutionWorkflowSteps()` over `RepositoryExecutionState` | **Exists** | Visual rail over existing state machine. |
| **Execution context** artifact package + size gauge | `ExecutionContextPreview` + `ExecutionContextDiagnostics` + `ExecutionContextSizePolicy` (128 KiB warn / 512 KiB hard) | **Exists** | Roles, KiB, within-limits all present. |
| **Live activity / execution stream** | SSE `EventSource('/api/execution-sessions/{id}/events/stream')` + poll fallback | **Exists** | Streams **UI→backend directly, bypassing the shell**; visual adoption safe. |
| **Milestones** list | `PlanningProjection.Milestones` / `ArtifactInventory.Milestones` | **Exists** | Per-milestone "criteria progress" bar **not** projected → **Partial** for that sub-detail. |
| **Commit & push** (scope, message, buttons) | `CommitPreparation` (ScopeItems, ProposedMessage) + commit/push commands | **Exists** | Restyle. |
| Commit panel **ahead/behind (↑2 ↓0)** | `RepositoryGitStatus` / dirty-state types | **Partial** | Dirty counts present; ahead/behind counts not confirmed in projection. |
| **Operational context** current understanding (grouped) | `OperationalContextProjection` (Architecture, Constraints, StableDecisions, OpenQuestions, ActiveRisks, …) | **Exists** | 1:1 section mapping. |
| **Proposed revision** + semantic changes + Accept/Edit/Reject | `OperationalContextProposal` (SemanticChanges, CompressionSummary, Review) + review commands | **Exists** | `RationaleLostWarning` etc. already in `OperationalContextSemanticChangeType`. |
| Proposal counts (+added / kept / resolved / warning) | `OperationalContextCompressionSummary` (added/preserved/removed/warnings) | **Exists** | Label translation only. |
| **Continuity** evolution table (Added/Removed/Resolved/Lost) | `ContinuityDiagnostics` + `ContinuityTrend` (AddedCount/RemovedCount/ResolvedCount/LostCount) | **Exists** | Column-for-column match. |
| **Continuity warnings** | `ContinuityDiagnostics.ContinuityWarnings` | **Exists** | Restyle. |
| **Execution history** | `RepositoryDashboardProjection.ExecutionHistory` | **Exists** | Restyle. |
| **Command palette (⌘K)** | — (no palette, no shortcuts in `App.tsx`) | **Missing** | New presentation layer over existing actions; low architectural risk. |
| **Design tokens / dark theme** | Hardcoded light vanilla CSS; no tokens file | **Missing** | Extract token set; DCLogic/`_ds` runtime non-portable. |
| **Abort execution** button | Internal cancellation plumbing only; no endpoint/command/UI | **Partial / Missing** | Capability gap, not just visual. See Risk + Open Questions. |
| Global nav **Executions** (cross-repo) | — (repository-centric only) | **Conflicts** | Implies cross-repo aggregation view; no backing projection; tension with repo-centric mandate. |
| Global nav **Insights** | — (no analytics projection) | **Missing / Conflicts** | No backend surface; risk of client-side analytics. |
| Global nav **Overview** | — | **Missing** | Undefined scope; likely cross-repo landing. |
| **Notifications bell** | — (no notification subsystem) | **Missing** | Cosmetic unless backed; no events pushed to UI today. |
| **User profile** (name/role) | — (single-user desktop app) | **Missing (cosmetic)** | No identity/auth concept; treat as decorative. |
| **"Run plan"** topbar button | "Start execution" flow | **Partial** | Label/semantics ambiguous vs. existing start-execution. |

---

## Architectural Boundary Analysis

Boundaries are documented in `docs/architecture.md`, `docs/operational-context-schema.md`, and ratified by Epic 3 (`.agents/plan.md`). Adaptation must respect them.

**Authority boundaries.**
- *Backend owns* repository state, artifact lifecycle, execution orchestration, provider invocation, monitoring, handoff validation, acceptance, Git operations, operational-context generation/review/promotion/compression, decision analysis, and continuity diagnostics (`docs/architecture.md`; backend services under `Execution/`, `Continuity/`, `Projections/`).
- *React owns presentation state only.* *Tauri owns* desktop windowing, dialogs, sidecar lifecycle, IPC bridging — **nothing else**.

**Projection boundary (projection-only UI).**
Epic 3: *"Continuity status is projected from backend-owned repository artifacts and proposal metadata. The UI remains projection-only."* Every panel in `app.dc.html` must be fed by a backend projection. Any target element without a projection (Insights, Overview, cross-repo Executions, ahead/behind counts, per-milestone criteria progress) is a **projection gap to resolve in the backend**, never a client-side computation.

**ExecutionSession ownership.**
Two distinct state models must remain backend-owned and must not be collapsed by the UI: `ExecutionSessionState` (provider lifecycle: Created/Executing/Completed/Failed/Cancelled) vs. `RepositoryExecutionState` (workflow). *"Provider completion is not enough to mark repository work successful."* The workflow rail and "Run plan/Start execution/Abort" affordances must map to **explicit human-decision transitions**, not implicit UI lifecycle effects.

**Operational-context lifecycle.**
Review-before-mutation: Generate → Persist → Review → Validate → Promote (archive-before-replace). The UI must **not** auto-generate or auto-promote as a side effect of navigation/rendering. Accept/Edit/Reject/Promote stay explicit. All continuity services reason over the canonical `OperationalContextDocument` model (`docs/operational-context-schema.md`) — the UI must not re-parse Markdown to derive sections/changes.

**Single workflow authority (the most load-bearing constraint).**
Epic 3 forbids: *decision sessions, continuity sessions, session routers, session reuse, separate repository state machines, client-owned workflow state.* A redesign that introduces a global "Executions" workspace, a palette that mutates state outside backend transitions, or any client-held workflow status would violate this.

**Repository / filesystem authority.**
Repository-owned state lives under each repo's `.agents/`; Command Center reads/edits but does not replace it with a private database. Continuity artifacts (`operational_context.md`, archived revisions, `proposals/`, `reports/`) are the source of truth.

**Tauri integration boundary.**
The shell is a synchronous proxy with **no event emission**; live streaming already bypasses it via SSE direct from the UI. Adopting `app.dc.html`'s richer live surfaces does not require new shell responsibilities — but introducing shell-side streaming/eventing *would* expand the boundary and should be treated as a deliberate decision, not an incidental one.

**Where adaptation could accidentally violate architecture:**
- Global nav (Executions/Insights/Overview) → cross-repo views with no projection / client aggregation.
- Command palette actions → dispatching mutations the backend doesn't authorize, or holding palette/workflow state client-side.
- "Abort execution" → exposing a transition with no backend endpoint, or wiring it to a non-authoritative cancel path.
- Continuity/operational panels → deriving sections or semantic changes by re-parsing Markdown in the client instead of consuming `OperationalContextProjection`.
- Stat tiles / counts → computing values (ahead/behind, criteria progress) in the UI rather than projecting them.

---

## UX Debt Analysis (evidence-backed)

1. **Monolithic component.** `App.tsx` is ~3,500 lines with all views as inline closures — no extracted components. Hard to maintain, test, or restyle; directly contradicts Epic 3's specified component structure (`components/*`, `api.ts`, `types.ts`) which was planned but never realized.
2. **No design system.** Colors/spacing hardcoded inline in `App.css`; no token layer. Restyling requires touching many call sites. Light, utilitarian theme far from the target's dense console aesthetic.
3. **Inline IPC calls.** ~24 `invoke()` call sites scattered through `App.tsx` rather than a centralized `api.ts` client — couples views to transport.
4. **Flat navigation / discoverability.** Tab-only navigation, no command palette, no keyboard shortcuts; the target's ⌘K is a real discoverability gain over today's surface.
5. **Density mismatch.** Current layout is a two-panel list/detail; the target's multi-panel inspector (left workflow column + right inspector rail) presents more operational state per screen — today users tab between things the target shows simultaneously.
6. **Streaming bypasses the shell.** SSE direct-to-backend works but means the desktop shell has no view of execution liveness — a latent coupling/observability gap if the backend URL or transport ever changes.
7. **No surfaced "missing artifact" affordance polish.** Epic 1 M5 mandated explicit (non-fatal) missing-artifact display; the target's empty/placeholder states are more refined and worth adopting.

---

## Adaptation Opportunity Analysis

**High value.**
- **Design-token extraction + dark console theme.** Highest visual payoff, lowest architectural risk; unlocks every other restyle. (Target token families: `--bg-canvas`, `--surface-1/2/3`, `--fg-default/muted/subtle/faint`, `--accent-*`, `--info/warn/danger/success/done-fg`, `--border-*`, type scale `--text-h2/h3/h4/small/caption/micro/mono-sm`, `--font-sans/mono`.)
- **Component decomposition to Epic 3's planned structure.** Converts UX debt into the maintainable layout the redesign needs; enables panel reuse across tabs.
- **Tabbed dense workspace re-composition.** Regroup existing surfaces into Workspace/Execution/Operational/Continuity tabs + left-workflow/right-inspector layout — all from existing projections.
- **Command palette over existing actions.** Discoverability win; pure presentation over backend-authoritative commands.

**Medium value.**
- **Workflow rail** visualization of `RepositoryExecutionState`.
- **Execution-context size gauge** and **continuity evolution table** restyle (data already present, just under-presented today).
- **Proposal-ready and live-execution indicators** in the sidebar (already projected).
- **Refined empty/missing-artifact and loading states.**

**Low value / defer.**
- **User profile footer** (no identity concept — cosmetic).
- **Notifications bell** (no notification subsystem — cosmetic unless a real source is defined).
- **"Insights" analytics view** (no projection; speculative).
- **Literal global nav (Overview/Executions)** (conflicts; see Risks).

---

## Risk Analysis

**Architectural risks.**
- *Global-nav literalism* (Executions/Insights/Overview) introducing cross-repo, client-aggregated, or non-projected views → violates repository-centric + projection-only mandate. **Highest risk.**
- *Palette/redesign holding workflow state client-side* → violates single-workflow-authority.

**Workflow risks.**
- *"Abort execution" / "Run plan"* affordances implying transitions with no authoritative backend path → broken or non-deterministic UX; "Abort" specifically has internal plumbing but no exposed transition.
- *Auto-generate/auto-promote as render side effects* during redesign → violates review-before-mutation.

**Continuity risks.**
- *Re-parsing operational-context Markdown in the client* to render sections/changes instead of `OperationalContextProjection` → drift from canonical `OperationalContextDocument`.
- *Mislabeling semantic-change/compression counts* (target uses "kept/resolved/warning"; backend uses preserved/removed/resolved/warnings) → misrepresenting continuity quality.

**Authority risks.**
- *Stat tiles computed in UI* (ahead/behind, criteria progress) → UI becomes a second source of truth.

**State-management risks.**
- *Decomposition regressions*: splitting the monolith can fracture the careful `useMemo`/`useCallback`/effect chains that keep projections consistent; behavior parity must be preserved through the restructure.

**Projection-drift risks.**
- *New panels demanding fields not yet projected* (Insights, ahead/behind, milestone criteria, notifications) tempting client-side derivation → each must be resolved as a backend projection addition or deliberately dropped.

**Boundary-expansion risk.**
- *Adopting shell-mediated streaming/eventing* to power live surfaces would enlarge the Tauri boundary beyond "thin proxy" — acceptable only as an explicit decision.

**Cosmetic-debt risk.**
- *Shipping decorative chrome* (profile, notifications) that implies functionality not present → user confusion.

---

## Backend Impact Survey (likely impact areas — not designs)

- **Projections.** Possible additions to satisfy target surfaces *only if those surfaces are kept*: ahead/behind counts (commit panel), per-milestone criteria progress (milestones list), any cross-repo "Executions"/"Insights"/"Overview" read model. Each is a projection question, not a UI computation.
- **DTOs.** Largely sufficient already; label/field reconciliation between target wording and existing DTO fields (e.g. compression counts) is a mapping concern, not new data.
- **Endpoints.** "Abort/cancel execution" is the one clear capability gap: backend models cancellation internally (`ExecutionMonitoringService.RecordCancellationAsync`, `IExecutionProviderObserver.OnProviderCancelledAsync`, `Cancelled` states) but exposes **no** endpoint/command. Adopting the affordance implies a backend transition surface (decision required).
- **Workspace / dashboard models.** Any global navigation target would require new cross-repository projections that do not exist today.
- **Execution-context visibility.** Already projected (`ExecutionContextPreview`/diagnostics); no change needed for restyle.
- **Continuity visibility.** Already projected (`ContinuityDiagnostics`, trends, warnings, evolution ledger); no change needed for restyle.
- **Repository summaries.** Sufficient for the sidebar and identity header as drawn.

*No backend change is required to restyle the existing surfaces; backend impact arises only where the target adds genuinely new surfaces (global nav, abort, notifications, certain counts).*

---

## Frontend Impact Survey (likely impact areas — not designs)

- **React components.** Decompose `App.tsx` into the Epic 3-specified set (`RepositoryDashboard`, `RepositoryWorkspace`, `ArtifactWorkspace`, `ExecutionWorkspace`, `ExecutionContextPanel`, `HandoffReview`, `GitWorkflow`, `OperationalContextSurface`, `OperationalContextProposalPanel`, `OperationalContextReviewPanel`, `ContinuityDiagnosticsPanel`) plus the target's new chrome (sidebar, topbar, command palette, workflow rail).
- **Layout structure.** Move from two-panel list/detail to fixed-sidebar + main with workflow-left / inspector-right composition; introduce a dark themed shell.
- **Navigation.** Tab model (Workspace/Execution/Operational/Continuity) is retained; *global* nav is the contested area (see Risks). Command palette is additive.
- **Workspace composition.** Identity header + stat tiles + workflow rail assembled from existing projections.
- **Dashboard composition.** Sidebar repo rows gain state pill / dirty / proposal-ready / lang-color treatments (data already present).
- **Review surfaces.** Operational-context current-vs-proposed side-by-side, semantic-change list, count tiles, Accept/Edit/Reject/Promote — restyle of existing flow.
- **Execution surfaces.** Live stream, session panel (provider/pid/elapsed), context diagnostics, and the **Abort** control (capability-gated).
- **Cross-cutting.** Introduce `api.ts` (centralize the ~24 `invoke`/HTTP/SSE call sites) and `types.ts` (consolidate the ~31 DTO types) — prerequisites for clean decomposition; a token/theme CSS layer; preserve existing effect/memoization behavior through the refactor.

---

## Open Questions (must be answered before `plan.md`)

1. **Global navigation:** Are `Overview`, `Executions`, and `Insights` in-scope, or is the redesign repository-centric only? If in-scope, what cross-repository projections back them, and how is that reconciled with Epic 3's "no separate top-level workspace" constraint?
2. **Abort execution:** Is user-invokable cancellation a goal? If yes, it requires a backend transition + shell command + UI; if no, the affordance must be omitted/disabled. Is "abort" distinct from existing reject/cancel semantics?
3. **"Run plan" vs. "Start execution":** Is the topbar "Run plan" the same as starting milestone execution, or a new plan-level action?
4. **Theme scope:** Dark theme only, or dark/light? Is the Kernritsu design system licensed/available as real CSS, or must tokens be re-implemented from the mockup values?
5. **Command palette scope:** Read-only navigation/jump, or may it trigger state-mutating actions? If the latter, which actions, and how is single-workflow-authority preserved?
6. **Decomposition appetite:** Is the full `App.tsx` → components refactor in-scope (recommended), or is a styling-only pass preferred first?
7. **Milestone criteria progress:** The target shows "4 / 6 criteria" per milestone — is criteria tracking a real backend concept to project, or mockup-only?
8. **Ahead/behind counts:** Add to git projection, or drop from the commit panel?
9. **Notifications & user profile:** Real subsystems to build, or decorative chrome to omit?
10. **Streaming ownership:** Keep SSE direct-to-backend (shell bypass) or route liveness through the shell? (Boundary decision.)
11. **Behavior-parity bar:** Must the redesign preserve exact current behavior of every existing flow, or is some flow change acceptable alongside the restyle?

---

## Recommended Planning Inputs

**Information `plan.md` must use:**
- The **mapping matrix** above as the authoritative inventory of Exists / Partial / Missing / Conflicts — most target panels are restyle-of-existing, a few are genuine gaps.
- The **existing projection/DTO catalog** (`RepositoryDashboardProjection`, `RepositoryWorkspaceProjection`, `ExecutionContextPreview` + diagnostics, `OperationalContextProjection`, `OperationalContextProposal` + semantic-change/compression types, `ContinuityDiagnostics` + trends) — the redesign should bind to these, not invent client data.
- The **Epic 3-specified component structure** (`api.ts`, `types.ts`, `components/*`) as the intended decomposition target.
- The **target design tokens** (color/surface/type scale) extracted from `app.dc.html`, re-expressed as real CSS (DCLogic/`_ds`/`support.js` are not portable).
- The resolved **Open Questions** above (especially global nav, abort, theme scope, palette mutation policy).

**Architectural constraints `plan.md` must respect:**
- **Projection-only UI** — every surface fed by a backend projection; no client-side aggregation/derivation/Markdown re-parsing.
- **Backend authority** over all state and workflow transitions; **two distinct execution state models** preserved.
- **Single workflow authority** — no new sessions, routers, parallel state machines, or client-owned workflow state.
- **Review-before-mutation** for operational context — no auto-generate/auto-promote from UI lifecycle.
- **Canonical `OperationalContextDocument`** as the only basis for continuity rendering.
- **Repository filesystem authority** — `.agents/` artifacts remain source of truth.
- **Thin Tauri shell** — do not expand its responsibilities incidentally.

**Areas requiring explicit milestone coverage in the future plan:**
1. Design-token/theme foundation (extract + apply).
2. `App.tsx` decomposition + `api.ts`/`types.ts` extraction with behavior parity.
3. Tabbed dense-workspace re-composition (Workspace/Execution/Operational/Continuity) from existing projections.
4. Command palette as a presentation layer over existing actions (with an explicit mutation policy).
5. Resolution of each **Conflicts/Missing** row (global nav, abort, insights, notifications, ahead/behind, criteria progress) — adopt-with-backing-projection, translate, or omit.
6. Regression safety for the existing operational-context and execution workflows during restyle/refactor.

---

*Audit complete. This document observes, maps, classifies, and identifies constraints/risks/opportunities only. It does not propose implementation, roadmap, milestones, APIs, schema, or state-model changes — those belong to a subsequent `plan.md`.*
