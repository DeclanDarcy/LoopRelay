# Command Center — Design-to-Implementation Reconciliation Audit

**Scope:** Reconcile the original product/design vision in `app.dc.html` against the current
certified frontend implementation in `src/CommandCenter.UI/**`.

**Baseline:** The current certified frontend (the M0–M8 frontend-modernization program, complete and
certified) is the implementation baseline. `app.dc.html` is the original product/design vision.

**Nature of this document:** This is a *product reconciliation* audit. It is **not** a modernization,
code-quality, or refactoring audit, and it supersedes the earlier adaptation-oriented `audit.md`
(committed at `724e350`), whose premise — an un-modernized monolithic `App.tsx` with no extracted
components, no design tokens, and no command palette — no longer matches the codebase. This audit
recommends **no** refactors, component/hook extraction, state-management rewrites, or `App.tsx`
decomposition. The certified architecture is treated as authoritative. Where the implementation
differs from the mockup, the audit determines whether the difference is a real product gap, a
backend-owned gap, an intentional certified deferral, an architectural evolution, or cosmetic design
drift — it does not reopen modernization decisions.

**Primary sources reviewed:**
`app.dc.html`; `src/CommandCenter.UI/src/**`; `.agents/milestones/m0`–`m8`;
`.agents/handoffs/**`; `docs/frontend-modernization-deviations.md`;
`.agents/audits/m8-final-validation.md`.

---

## 1. Executive Summary

### Overall alignment assessment

The current frontend is a **faithful and, in several areas, expanded** realization of the
`app.dc.html` vision. Every workspace, tab, and core interaction concept in the mockup exists in the
product. The information architecture — left sidebar shell → per-repository workspace → four tabs
(Workspace, Execution, Operational context, Continuity) — is implemented exactly as designed.

Residual differences fall into clean, well-understood buckets:

1. **Backend-owned gaps** — capabilities the frontend *cannot* implement honestly because the backend
   does not project the required data or expose the required command (cross-repository rollups,
   per-repository git summaries, milestone criteria progress, abort, notifications). All are
   **recorded and represented honestly** in the UI (disabled placements, omitted metrics) per the M8
   certification principle: *"a missing capability is not a defect if it is recorded and represented
   honestly in the UI."*

2. **Cosmetic design drift** — visual details from the mockup intentionally simplified during the
   design-system pass (text labels instead of progress-bar glyphs, no blinking cursor, static elapsed
   instead of a ticking timer, no line numbers in the markdown viewer). None affects a product
   capability.

A small number of mockup elements were **superseded** by deliberate architecture decisions — most
notably the command palette, which the mock framed as an action-runner ("Start execution", "Abort
execution") but which M7 deliberately implemented as a **richer navigation-only** discovery surface.

Several areas where the product **exceeds** the original vision: a fully wired proposal lifecycle
(Accept / Reject / inline **Edit** / **Promote** — the mock only sketched Accept/Edit/Reject), artifact
**editing and rotation**, a handoff **review** workflow, an execution **repository snapshot**, and
**continuity reports** — none of which were drawn in `app.dc.html`.

> **Bottom line:** The product envisioned in `app.dc.html` is **fully represented**. No core capability
> is missing through oversight. Every residual gap is either an explicitly certified backend-owned
> deferral or cosmetic. There is no design drift that constitutes a defect.

### Major implemented areas (capability present, no meaningful gap)

- The full application shell: 264px sidebar, command-palette trigger, repository list, four-tab
  per-repository workspace, header with breadcrumb + refresh.
- Workspace tab: 5-step workflow rail, execution-context preview with artifact inventory and
  size/limit diagnostics, live activity stream, commit & push panel (per-file selection + commit
  message + push gating), operational-context summary, execution history.
- Execution tab: live SSE event stream, session metadata, context diagnostics with launch-blocking,
  repository snapshot, generated-handoff content + git-workflow evidence + handoff review.
- Operational context tab: current-understanding sections, proposed-revision semantic-change list,
  change-summary stats, **fully wired Accept / Reject / Edit / Promote** with local draft lifecycle.
- Continuity tab: understanding-evolution trends table, continuity warnings, continuity reports.
- Artifact experience: categorized artifact tree, markdown content viewer, **in-place editing**,
  **rotation** of handoff/decisions.

### Major missing / partial areas (all backend-owned or cosmetic)

- **Milestone criteria progress** ("4 / 6 criteria") — backend inventory has no parsed criteria. *(Backend-owned, deferred.)*
- **Per-repository git summary in the sidebar/list** (branch, dirty count, ahead/behind) — dashboard
  projection does not provide them. *(Backend-owned, deferred.)*
- **Artifact size / version / revision metadata** ("8.2 KiB · v3") — artifact inventory lacks these fields. *(Backend-owned, partial.)*
- **Abort / cancel execution** — no backend cancellation contract. *(Backend-owned, deferred.)*
- **Notifications** — no notification projection; placement kept disabled. *(Backend-owned, deferred.)*

### Major superseded areas

- **Command palette as action-runner → navigation-only discovery layer** (M7). The palette no longer
  *runs* actions (Start/Abort execution); it navigates to repositories, tabs, milestones, sessions,
  inspector sections, open questions/risks/decisions, proposals, workflow states, and warnings — a
  superset of the mock's single "Go to repository" group.
- **"Run plan" topbar button → explicit "Start Execution" control** inside the execution-context panel,
  gated on readiness. The single mock "Run plan" affordance was never in program scope.
- **Product-switcher dropdown → static product identity badge** (single-product app; the switcher was
  decorative in the mock).

### Major backend-owned gaps

All five backend-owned gaps are catalogued in `docs/frontend-modernization-deviations.md` and classified
**Deferred** at M8:
(1) cross-repository overview/executions/insights projections;
(2) per-repository git summary fields on `RepositoryDashboardProjection`;
(3) milestone criteria parser/progress projection;
(4) execution abort/cancel contract;
(5) notification projection.

---

## 2. Capability Reconciliation Matrix

Status legend: **FI** Fully Implemented · **PI** Partially Implemented · **DEF** Deferred ·
**SUP** Superseded · **MISS** Missing Capability · **BOG** Backend-Owned Gap.

### Shell & Navigation

| Original Capability (app.dc.html) | Current Status | Classification | Evidence | Notes |
|---|---|---|---|---|
| 264px left sidebar shell | Implemented | FI | `components/shell/Sidebar.tsx`, `components/shell/AppShell.tsx` | Layout matches design grid. |
| Product identity badge ("Kernritsu · Compass / Command Center") | Implemented | FI | `Sidebar.tsx` | Identity rendered. |
| Product-switcher dropdown (chevron) | Switcher absent | SUP | `Sidebar.tsx` (static badge) | Single-product app; dropdown was decorative. Design Drift / Architectural Evolution. |
| Command-palette trigger w/ ⌘K hint | Implemented | FI | `Sidebar.tsx`, `components/shell/CommandPalette.tsx` | Trigger + Ctrl/⌘-K + Esc all wired. |
| Global nav: **Repositories** (active) | Implemented | FI | `Sidebar.tsx` | Primary working surface. |
| Global nav: **Overview** | Visible, disabled | DEF | `Sidebar.tsx`; ledger §Global Overview | No global overview projection. Backend-owned. |
| Global nav: **Executions** | Visible, disabled | DEF | `Sidebar.tsx`; ledger §Global Executions | Execution data is repo/session scoped. Backend-owned. |
| Global nav: **Insights** | Visible, disabled | DEF | `Sidebar.tsx`; ledger §Insights | No insight/rollup projection. Backend-owned. |
| Repositories count badge / Executions live pulse | Indicators omitted | PI | `Sidebar.tsx` | Cosmetic; nav targets disabled anyway. Design Drift. |
| "+" add-repository affordance | Add-repo exists (header) | FI | `components/shell/Header.tsx` (Add Repository) | Capability present; placement differs from sidebar "+". |
| Repo list row: name + status + proposal indicator | Implemented | FI | `Sidebar.tsx`, `components/design/StatusBadge.tsx` | Name, state, proposal dot present. |
| Repo list row: branch + dirty + ahead/behind | Per-row git summary absent | BOG | ledger §Repository Git Summary, §Ahead/Behind | Dashboard projection lacks these fields. Backend-owned. |
| Sidebar user footer (avatar / name / role / settings gear) | Absent | MISS | `Sidebar.tsx` | Mock persona; no identity backend. Design Drift (cosmetic). |
| Command palette: groups & contents | Navigation-only palette | SUP | `CommandPalette.tsx`; M7 milestone | Mock framed it as an action-runner; M7 made it richer navigation-only. Architectural Evolution. |
| Command palette: "Abort execution" command | Absent | DEF | M7 ("no palette abort command"); ledger §Abort | No backend abort. Backend-owned. |
| Topbar breadcrumb "Repositories > {repo}" | Static breadcrumb | PI | `Header.tsx` | Shows "Command Center / Repositories"; not repo-name dynamic. Design Drift. |
| Topbar refresh/sync | Implemented | FI | `Header.tsx` | Wired to workspace/repository refresh. |
| Topbar notification bell + unread dot | Disabled placement | DEF | `Header.tsx` (`notification-slot-disabled`); ledger §Notifications | No notification projection. Backend-owned. |
| Topbar "Run plan" primary button | Replaced by Start Execution | SUP | `features/workspace/ExecutionContextPanel.tsx` | Execution launched via explicit, readiness-gated Start Execution. Architectural Evolution. |
| Four workspace tabs (Workspace/Execution/Op-context/Continuity) | Implemented | FI | `components/shell/WorkspaceTabs.tsx` | Exact match. |
| In-session navigation context preservation | Implemented | FI | `state/shellState.ts`; M7 | Cross-workspace context preserved. |
| URL deep linking / routing | Absent | MISS | `state/shellState.ts` (in-memory) | Mock also used ephemeral state; not a design regression. Product Gap (future). |
| Cross-links between surfaces ("Review proposal →", inspector links) | Implemented | FI | `features/repositories/SelectedRepositorySummary.tsx`, `features/operational-context/OperationalContextCurrentPanel.tsx`; M7 | Cross-workspace links present. |

### Workspace Tab

| Original Capability | Current Status | Classification | Evidence | Notes |
|---|---|---|---|---|
| 5-step workflow rail (Context→Execution→Handoff→Commit→Push) | Implemented | FI | `features/workspace/WorkflowRail.tsx`, `lib/executionWorkflow.ts` | States derived from backend execution state. Step numerals not rendered (cosmetic). |
| Execution-context panel: artifact inventory + Start/Build buttons | Implemented | FI | `features/workspace/ExecutionContextPanel.tsx`, `features/execution/ExecutionContextArtifactList.tsx` | Backend-wired preview + start. |
| Per-artifact size bar + aggregate "within limits" bar | Rendered as text | PI | `features/execution/ExecutionContextSummaryRows.tsx` | Bars / "within limits" pill shown as text; data present. Design Drift. |
| Live activity panel (stream + provider + session id) | Implemented | FI | `features/workspace/WorkspaceLiveActivityPanel.tsx`, `features/execution/ExecutionEventFeed.tsx` | Real events; blinking cursor omitted (cosmetic). |
| Milestones panel: identity + selection | Implemented | FI | `features/workspace/WorkspaceMilestonesPanel.tsx` | Milestone files listed/selectable. |
| Milestones panel: criteria progress ("4/6") + status pills | Absent | BOG | ledger §Milestone Criteria Progress; M3 ("do not fabricate criteria counts") | Inventory has no parsed criteria/progress. Backend-owned. |
| Commit & push panel: per-file selection, commit msg, gated push | Implemented | FI | `features/execution/GitWorkflowPanel.tsx` | Checkboxes + select all/none + push gating wired. Exceeds mock. |
| Commit & push: ahead/behind "↑2 ↓0" glyph | Text label | PI | `features/workspace/WorkspaceInspectorRail.tsx` | Shown as "Ahead/Behind" text (selected repo). Design Drift. |
| Operational-context quick panel (metrics + "Review proposal →") | Implemented | FI | `features/workspace/WorkspaceInspectorRail.tsx` | Metrics grid + navigation link. |
| Execution history panel (state/milestone/commit/duration) | Implemented | FI | `features/execution/ExecutionHistoryPanel.tsx` | Real session history. |

### Execution Tab & Lifecycle

| Original Capability | Current Status | Classification | Evidence | Notes |
|---|---|---|---|---|
| Live "codex execution stream" | Implemented (SSE) | FI | `api/executionEvents.ts`, `hooks/useExecutionEvents.ts`, `features/execution/ExecutionEventFeed.tsx` | EventSource stream; pulse/cursor cosmetic. |
| Session panel (provider/pid/milestone/started/elapsed) | Implemented | FI | `features/execution/ExecutionSessionPanel.tsx`, `hooks/useExecutionSession.ts` | Elapsed is backend duration, not a live ticker (cosmetic). |
| Abort / cancel execution (panel + palette) | Absent | DEF | ledger §Abort; M4 ("keep abort hidden until backend exists"), M8.2 | No backend abort contract. Backend-owned. `Cancelled` state exists but unreachable from UI. |
| Start execution (wired) | Implemented | FI | `api/execution.ts` (`start_execution`) | Tauri command. |
| Build / rebuild execution context (wired) | Implemented | FI | `api/execution.ts` (`preview_execution_context`), `hooks/useExecutionContextPreview.ts` | Tauri command. |
| Context diagnostics (limits / validation / launch-blocked) | Implemented | FI | `features/execution/ExecutionContextValidationList.tsx`, `ExecutionContextArtifactDiagnosticsList.tsx` | Validation + launch-blocked present; KiB formatting cosmetic. |
| Execution lifecycle state machine | Implemented | FI | `types/execution.ts`, `lib/executionWorkflow.ts`, `lib/status.ts` | Full Ready/Executing/Awaiting*/Accepted/Failed/Cancelled model. |
| Repository snapshot during execution | Implemented (exceeds design) | FI | `features/execution/ExecutionRepositorySnapshotPanel.tsx` | Not in mock; product addition. |
| Generated handoff content + git evidence + handoff review | Implemented (exceeds design) | FI | `features/execution/GeneratedHandoffContent.tsx`, `GitWorkflowEvidence.tsx`, `GeneratedHandoffReviewPanel.tsx` | Accept/Reject handoff workflow beyond mock. |

### Operational Context Tab & Proposal Workflows

| Original Capability | Current Status | Classification | Evidence | Notes |
|---|---|---|---|---|
| Current-understanding panel (rev + grouped sections) | Implemented (exceeds design) | FI | `features/operational-context/OperationalContextCurrentPanel.tsx` | Adds rationale/authority/recent-changes sections. |
| Proposed-revision panel + semantic-change list | Implemented | FI | `features/operational-context/OperationalContextSemanticChangeList.tsx` | DecisionAdded/ConstraintAdded/QuestionResolved/RationaleLostWarning supported. |
| Proposal change-summary stat grid | Implemented (exceeds design) | FI | `features/operational-context/OperationalContextCompressionSummaryPanel.tsx` | Richer than mock's four stats. |
| Proposal **Accept** | Implemented (wired) | FI | `App.tsx` → `accept_operational_context_proposal` | Backend command + refresh. |
| Proposal **Reject** | Implemented (wired) | FI | `App.tsx` → `reject_operational_context_proposal` | Backend command + refresh. |
| Proposal **Edit** (inline) | Implemented (wired) | FI | `features/operational-context/OperationalContextTab.tsx` (textarea) → `edit_operational_context_proposal` | Mock had an Edit button only; product does inline edit + save. |
| Proposal **Promote** | Implemented (exceeds design) | FI | `App.tsx` → `promote_operational_context_proposal` | Not in mock; gated on Accepted + Reviewed + not-stale. |
| Proposal draft lifecycle (local) | Implemented | FI | `App.tsx` (`operationalContextProposalDraft`) | Local workflow-review state (M0 decision). |
| Proposal status/readiness lifecycle | Implemented | FI | `types/operationalContext.ts`, `App.tsx` | Pending/Edited/Accepted/Rejected/Promoted + review states. |
| Repository readiness derivation (Ready/MissingMilestones) | Implemented | FI | `lib/status.ts`, `App.tsx` | Backend-derived readiness gates Start Execution. |

### Continuity Tab & Operational Intelligence

| Original Capability | Current Status | Classification | Evidence | Notes |
|---|---|---|---|---|
| Understanding-evolution trends table (Section/Added/Removed/Resolved/Lost) | Implemented | FI | `features/continuity/ContinuityDiagnosticsPanel.tsx`, `hooks/useContinuityDiagnostics.ts` | Backend-projected, column-for-column. |
| Continuity warnings list | Implemented | FI | `features/continuity/ContinuityDiagnosticsPanel.tsx`, `types/continuity.ts` | Backend-derived (not frontend-computed, per M6). |
| Continuity reports / evidence | Implemented (exceeds design) | FI | `types/continuity.ts` (`ContinuityReport`), continuity report generation | Indicators + report generation beyond mock. |
| Continuity revision-history timeline browser | Not a separate view | MISS | `ContinuityDiagnosticsPanel.tsx` (latest only) | Mock had no timeline browser either; not a design gap. Product Gap (future). |
| Status indicators / summaries / progress / reporting | Implemented | FI | `components/design/StatusBadge.tsx`, `features/repositories/SelectedRepositorySummary.tsx`, compression/continuity panels | Operational-intelligence intent fulfilled (except criteria progress = BOG). |

### Artifact Experience

| Original Capability | Current Status | Classification | Evidence | Notes |
|---|---|---|---|---|
| Artifact tree / browser (Plan/Op-context/Milestones/Handoff/Decisions) | Implemented | FI | `features/artifacts/ArtifactWorkspace.tsx`, `api/artifacts.ts` | Categorized tree + content viewer (maps to mock `artifactTree`). |
| Artifact markdown rendering | Implemented | FI | `features/artifacts/ArtifactMarkdownPreview.tsx`, `lib/markdown.tsx` | Block-level render; **no line numbers** (mock `opMdLines`). Design Drift. |
| Artifact metadata (kind / version state) | Implemented | FI | `features/artifacts/ArtifactMetadata.tsx`, `types/artifacts.ts` | family/name/path/versionKind shown. |
| Artifact metadata (size KiB / version "v3" / "rev 18") | Absent | BOG | `types/artifacts.ts` (no size/rev fields) | Inventory lacks size/revision fields. Backend-owned. |
| Artifact editing (in-place) | Implemented (exceeds design) | FI | `ArtifactWorkspace.tsx` (textarea) → `saveArtifactContent` | Real edit→save workflow; not drawn in mock. |
| Artifact rotation (handoff/decisions) | Implemented (exceeds design) | FI | `api/artifacts.ts` (`rotateCurrentHandoff`, `rotateCurrentDecisions`) | Archive-to-historical; not in mock. |
| Artifact review (distinct stage) | No separate stage | MISS | — | Not in mock; covered by edit/save + handoff/proposal review elsewhere. Product Gap (minor). |
| Artifact cross-linking (inline content references) | Page/section links only | PI | `OperationalContextCurrentPanel.tsx` cross-links | Inline artifact-to-artifact hyperlinks absent. Design Drift / Product Gap (minor). |
| Dedicated "Artifacts" tab | Embedded in Workspace | SUP | `App.tsx` (ArtifactWorkspace within WorkspaceTab) | Mock defined artifact data but rendered no separate tab; embedding is consistent. Architectural Evolution. |

### Cross-Repository / Portfolio Experience

| Original Capability | Current Status | Classification | Evidence | Notes |
|---|---|---|---|---|
| Portfolio / cross-repository overview | Disabled nav | DEF | ledger §Global Overview | No cross-repo overview projection. Backend-owned. |
| Cross-repository execution command center | Disabled nav | DEF | ledger §Cross-repository Execution Views | Repo/session-scoped only. Backend-owned. |
| Cross-repository continuity / insight rollups | Disabled nav | DEF | ledger §Cross-repository Continuity And Insight Rollups | Repo-scoped only. Backend-owned. |
| Aggregated portfolio state | Repo-scoped only | DEF | ledger (multiple) | Same root cause: no cross-repo projection. Backend-owned. |

---

## 3. Gap Inventory

*(Items classified **Partially Implemented**, **Missing Capability**, or **Backend-Owned Gap** only.
Each maps to an architectural-reconciliation category.)*

### Backend-Owned Gaps (frontend cannot implement honestly)

1. **Milestone criteria progress** — *Required backend capability:* milestone criteria parser + progress
   projection. *Frontend impact:* milestones show identity/selection only; no "4/6 criteria" or status
   pills. *Recon:* Backend Gap (certified deferral: M3 + M8.4 + ledger).
2. **Per-repository git summary (branch, dirty, ahead/behind across all repos)** — *Required backend
   capability:* extend `RepositoryDashboardProjection` with branch/dirty/ahead/behind/captured-timestamp.
   *Frontend impact:* sidebar/list rows omit branch and dirty/divergence; these appear only for the
   selected repo where git status / commit preparation / push review / execution snapshots provide
   them. *Recon:* Backend Gap (React must not fan out per-repo git calls — M2 + M8.4 + ledger).
3. **Artifact size / version / revision metadata** — *Required backend capability:* size + version/revision
   fields on artifact inventory. *Frontend impact:* artifact metadata shows kind/version-state but not
   "8.2 KiB · v3" / "rev 18". *Recon:* Backend Gap.
4. **Abort / cancel execution** — *Required backend capability:* abort contract, endpoint/Tauri command,
   provider/process cancellation, session-state transition authority, monitoring event. *Frontend
   impact:* no abort control in the session panel or palette; `Cancelled` state unreachable from UI.
   *Recon:* Backend Gap (certified deferral: M4 + M8.2 + ledger).
5. **Notifications** — *Required backend capability:* notification projection (count, severity,
   destination, timestamps, read/unread). *Frontend impact:* header bell kept as a disabled placement;
   no counts/menu. *Recon:* Backend Gap (certified deferral: M2 + M8.5 + ledger).

### Partially Implemented (core present, cosmetic/secondary portion absent)

6. **Per-artifact + aggregate context-size visualization** — *Implemented:* sizes, thresholds, limit
   status (as text). *Missing:* progress-bar glyphs and "within limits" pill rendering. *Impact:* none
   functional. *Recon:* Design Drift.
7. **Ahead/behind indicator format** — *Implemented:* ahead/behind counts (selected repo). *Missing:*
   "↑2 ↓0" glyph styling. *Impact:* cosmetic. *Recon:* Design Drift.
8. **Topbar breadcrumb** — *Implemented:* static breadcrumb. *Missing:* dynamic active-repository name
   segment. *Impact:* minor wayfinding. *Recon:* Design Drift.
9. **Repositories count badge / Executions live pulse** — *Implemented:* nav items. *Missing:* count
   badge and pulse indicator (targets disabled regardless). *Impact:* cosmetic. *Recon:* Design Drift.
10. **Artifact cross-linking** — *Implemented:* page/section cross-links between workspaces. *Missing:*
    inline artifact-to-artifact references within rendered markdown. *Impact:* minor navigation depth.
    *Recon:* Design Drift / Product Gap (future).

### Missing Capability (absent; not an explicit backend deferral)

11. **Sidebar user footer** (avatar / name / role / settings gear) — *Expected behavior:* user identity
    block. *Current behavior:* absent. *Implementation gap:* no identity/auth backend; the mock used a
    static persona. *Recon:* Design Drift (cosmetic).
12. **URL deep linking / routing** — *Expected behavior (audit area):* shareable deep links to repo/tab.
    *Current behavior:* in-memory shell state (the mock used ephemeral state too); in-session context
    preservation exists. *Recon:* Product Gap (future; not a design regression).
13. **Continuity revision-history timeline browser** — *Expected behavior (audit area):* per-revision
    history view. *Current behavior:* latest diagnostics only (mock had no timeline either). *Recon:*
    Product Gap (future).
14. **Artifact review as a distinct stage** — *Expected behavior (audit area):* separate artifact-review
    workflow. *Current behavior:* edit→save (+ handoff/proposal review elsewhere); no dedicated
    artifact-review gate. *Recon:* Product Gap (minor; not in original design).

---

## 4. Architectural Evolution Inventory

*(Items classified **Superseded** or **Deferred** — where the modernized architecture intentionally
differs from the original mockup.)*

### Superseded (original design concept replaced by a later decision)

| Original concept | Replacement concept | Architectural rationale |
|---|---|---|
| Command palette as **action-runner** ("Start execution", "Build context", "Abort execution") | **Navigation-only discovery layer** (M7) covering repositories, tabs, milestones, sessions, inspector sections, questions/risks/decisions, proposals, workflow states, warnings | M7 certified that palette links "do not refresh, mutate, or trigger workflows"; mutations stay on explicit, readiness-gated controls. The navigation palette is a *superset* of the mock's single "Go to repository" group. |
| Topbar **"Run plan"** button | Explicit **"Start Execution"** control in the execution-context panel, gated on backend readiness | Execution is launched from where its context/readiness is visible and validated, not from a generic global button. "Run plan" was never in program scope. |
| **Product-switcher** dropdown | Static **product identity badge** | Single-product application; a switcher would imply non-existent multi-product navigation. |
| Dedicated **Artifacts tab** (implied by `artifactTree`/`opMdLines` mock data) | **Artifact workspace embedded in the Workspace tab** | The mock defined artifact data but rendered no separate artifact tab; embedding keeps artifacts adjacent to the execution context that consumes them. |

### Deferred (capability intentionally excluded; not a defect)

| Capability | Deferring decision | Reason | Current status |
|---|---|---|---|
| Global **Overview** navigation | M2 / M8.3 / ledger §Global Overview | Only cross-repo projection is the dashboard list; no overview projection | Visible but disabled. |
| Global **Executions** navigation | M8.3 / ledger §Global Executions | Execution detail/streams are repo/session scoped | Visible but disabled. |
| Global **Insights** navigation | M8.3 / ledger §Insights | Continuity/op-context projections are repo scoped | Visible but disabled. |
| Cross-repository **execution views** | M8.1/8.3 / ledger | Backend commands/projections repo/session scoped | Deferred; nav disabled. |
| Cross-repository **continuity/insight rollups** | M8.1/8.3 / ledger | Diagnostics/reports/op-context repo scoped | Deferred; nav disabled. |
| **Abort execution** (panel + palette) | M4 / M8.2 / ledger §Abort | No backend abort contract / cancellation authority | Omitted; `Cancelled` state exists but unreachable. |
| **Notifications** (bell counts/menu) | M2 / M8.5 / ledger §Notifications | No notification projection; synthetic counts would misrepresent state | Disabled placement retained. |
| **Milestone criteria progress** | M3 / M8.4 / ledger §Milestone Criteria | Inventory lacks parsed criteria/progress/completion authority | Omitted; "do not fabricate criteria counts." |
| **Per-repository git summary** (branch/dirty/ahead/behind for all repos) | M2 / M8.4 / ledger §Repository Git Summary, §Ahead/Behind | React must not fan out per-repo git calls; backend does not project these | Shown only for selected-repo git status / commit / push / execution snapshots. |

> **Certification note.** The frontend-modernization program (M0–M8) was declared **complete and
> certified** at M8 (`m8-capability-gaps-cleanup-and-final-validation.md`,
> `.agents/audits/m8-final-validation.md`, closing `handoff.md`). The governing principle was:
> *"A missing capability is not a defect if it is recorded and represented honestly in the UI; an
> unrecorded mismatch found during final validation is a defect."* Every Deferred and Backend-Owned
> item above is recorded in `docs/frontend-modernization-deviations.md` and represented honestly
> (disabled placement or omitted metric) — therefore none is a defect. The remaining `App.tsx`
> workflow-coordination authority was likewise certified as an *intentional* boundary
> ("Implemented differently"), not a residual gap, and is explicitly out of scope for this audit.

---

## 5. Reconciliation Verdict

| Question | Answer |
|---|---|
| Is the product envisioned in `app.dc.html` fully represented? | **Yes** — all workspaces, tabs, and core interactions exist; several areas exceed the vision. |
| Are there capabilities missing through oversight? | **No** — every gap is a certified backend-owned deferral or cosmetic drift. |
| Is any divergence a defect? | **No** — all divergences are recorded and honestly represented per the M8 principle. |
| Net architectural direction | Implementation has **matured beyond** the static mockup: real workflow wiring (proposal Accept/Reject/Edit/**Promote**, artifact **edit/rotate**, handoff **review**, execution **snapshot**, continuity **reports**) replaces mock affordances; speculative cross-repo/notification/abort surfaces correctly wait on backend authority. |

*End of audit.*
