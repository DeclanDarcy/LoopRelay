# Command Center Frontend Modernization Plan

## Objective

Modernize the Command Center frontend into a dense, dark, repository-centered operational console while preserving the current backend-owned workflow model.

The target application has these visible structural elements:

- A persistent left sidebar, approximately 264px wide, containing product identity, command launcher, global navigation, and repository selection.
- A top header containing breadcrumb, selected repository identity, refresh, notification placement, and primary action placement.
- Four primary repository workspaces: Workspace, Execution, Operational Context, and Continuity.
- A command palette opened by Ctrl+K or Meta+K.
- A Workspace tab with a workflow rail, execution context summary, live activity, milestones, and a right inspector rail.
- A dedicated Execution tab with full event stream, session details, execution diagnostics, context diagnostics, and launch readiness.
- A dedicated Operational Context tab for current understanding, proposed understanding, semantic changes, compression effects, decision continuity, review, and promotion visibility.
- A dedicated Continuity tab for understanding evolution, warnings, compression trends, decision retention, question lifecycle, risk lifecycle, and report visibility.

The migration is complete when the live application presents that structure using real backend projections and real platform capabilities, without moving business authority into React.

## Current Codebase Facts

- React UI lives in `src/CommandCenter.UI/src`.
- `src/CommandCenter.UI/src/App.tsx` currently contains projection DTOs, Tauri `invoke()` calls, backend `EventSource` setup, state hooks, draft state, workflow helpers, and the entire rendered UI.
- `src/CommandCenter.UI/src/App.css` currently contains the whole frontend visual system and layout.
- `src/CommandCenter.UI/src/devTauriMock.ts` duplicates many DTOs and provides a `?mock=workspace-certification` development state for frontend certification.
- The Tauri shell in `src/CommandCenter.Shell/src/main.rs` exposes commands for repositories, artifacts, execution context preview, operational-context proposal review/promotion, continuity diagnostics/reports, execution start, handoff accept/reject, git status, commit, and push.
- Backend endpoint registration is in `src/CommandCenter.Backend/Program.cs`.
- Backend projections and services already exist for repositories, workspaces, execution sessions, execution events, execution context, operational context, continuity diagnostics, continuity reports, git status, commit, and push.
- Backend tests are extensive under `tests/CommandCenter.Backend.Tests`.
- The frontend currently has `npm run lint` and `npm run build`, but no dedicated frontend test runner.

## Non-Negotiable Architecture Rules

1. Backend authority remains intact.
   Data and workflow decisions flow `Backend -> Projection -> UI`.

2. React owns presentation state only.
   React may own selected repository, selected tab, selected artifact, command-palette visibility, local editor drafts, expanded sections, focus, and scroll targets.

3. React must not become workflow authority.
   React must not decide execution transitions, handoff validity, commit readiness, push readiness, proposal lifecycle validity, promotion validity, continuity health, or repository readiness.

4. Navigation state, projection state, and draft state stay separate.
   Navigation state is synchronous and client-owned. Projection state is asynchronous and backend-owned. Draft state is local and ephemeral.

5. Missing backend capability must be represented honestly.
   If a target affordance requires data or behavior that does not exist, either add a backend projection/capability or render a disabled/omitted placeholder. Do not fabricate values in React.

6. The command palette is navigation-only until explicit backend workflow authority exists for palette actions.
   It may switch repositories, tabs, and sections. It must not start execution, abort execution, commit, push, accept, reject, or promote.

7. Existing repository-owned artifact semantics remain unchanged.
   Repository `.agents` files remain authoritative. Command Center reads, edits, rotates, proposes, reviews, promotes, and projects those artifacts; it does not replace them with hidden client state.

8. Manual refresh behavior is preserved unless a later backend projection explicitly introduces a different policy.

9. Do not over-implement the target mock.
   The target UX is authoritative for layout, interaction, navigation, density, and visual hierarchy. Mock data, fake statistics, and imaginary capabilities are not authoritative.

## Backend Projection Inventory

Use these existing projection and command surfaces before adding anything new:

- Dashboard list: `list_repositories`, backed by `GET /api/repositories`.
  Provides repository identity, availability, readiness, execution state, active execution session, execution summary, execution history, milestone count, current handoff/decisions flags, and continuity summary.

- Workspace: `get_repository_workspace` and `refresh_repository_workspace`, backed by `/api/repositories/{repositoryId}/workspace` and `/api/repositories/{repositoryId}/refresh`.
  Provides repository identity, availability, readiness, execution state, execution summary/history, artifact inventory, milestone count, artifact presence flags, operational-context proposal summary, and operational-context projection.

- Artifact content: `load_artifact_content`, `save_artifact_content`, `rotate_current_handoff`, and `rotate_current_decisions`.

- Execution context: `preview_execution_context`.
  Provides artifacts, repository git snapshot, aggregate diagnostics, per-artifact diagnostics, missing optional artifacts, validation errors, hard/warning thresholds, and launch-blocking status.

- Execution session and events: `start_execution`, `get_active_execution`, `get_execution_session`, execution status, execution events, and SSE event stream from `/api/execution-sessions/{sessionId}/events/stream`.

- Handoff decision: `accept_execution_handoff` and `reject_execution_handoff`.

- Git, commit, and push: `get_git_status`, `prepare_commit`, `commit_execution`, and `push_execution`.

- Operational context: `generate_operational_context_proposal`, `list_operational_context_proposals`, `get_operational_context_proposal`, `edit_operational_context_proposal`, `accept_operational_context_proposal`, `reject_operational_context_proposal`, and `promote_operational_context_proposal`.

- Continuity: `get_continuity_diagnostics`, `generate_continuity_report`, and `list_continuity_reports`.

## Target Frontend File Structure

Create a modular frontend structure under `src/CommandCenter.UI/src`:

```text
api/
  artifacts.ts
  backend.ts
  continuity.ts
  execution.ts
  git.ts
  operationalContext.ts
  repositories.ts
  tauri.ts
  index.ts

components/
  design/
    Badge.tsx
    Button.tsx
    EmptyState.tsx
    IconButton.tsx
    InspectorSection.tsx
    Metric.tsx
    Panel.tsx
    SectionHeader.tsx
    StatusBadge.tsx
    Table.tsx
    Tabs.tsx
  shell/
    AppShell.tsx
    CommandPalette.tsx
    Header.tsx
    Sidebar.tsx
    WorkspaceTabs.tsx

features/
  artifacts/
  continuity/
  execution/
  operational-context/
  repositories/
  workspace/

hooks/
  useArtifactContent.ts
  useContinuityDiagnostics.ts
  useExecutionContextPreview.ts
  useExecutionEvents.ts
  useExecutionSession.ts
  useGitStatus.ts
  useOperationalContextProposal.ts
  useRepositories.ts
  useRepositoryWorkspace.ts

lib/
  executionWorkflow.ts
  formatting.ts
  markdown.tsx
  status.ts

state/
  shellState.ts

styles/
  base.css
  tokens.css
  theme.css

test/
  characterization/
  fixtures/
  render.tsx

e2e/
  workspace-certification.spec.ts

types/
  artifacts.ts
  continuity.ts
  execution.ts
  git.ts
  operationalContext.ts
  planning.ts
  repositories.ts
  index.ts
```

`App.tsx` should become a thin composition root that wires providers, hooks, and shell state. It should not contain DTO declarations, transport calls, projection loading effects, or large rendering branches.

## Milestone 0: Frontend Foundations

Goal: create stable type, transport, hook, and state boundaries with no visible UX change.

### Workstream 0.0: Mandatory Frontend Test Infrastructure

Add frontend test infrastructure before extracting types, APIs, hooks, or components.

Required tooling:

- Vitest for unit and hook tests.
- Testing Library for component behavior tests.
- Playwright for browser-level characterization and layout/navigation checks.
- A stable render helper for components that need shell state, mock Tauri commands, and projection fixtures.
- Script entries:
  - `npm run test`
  - `npm run test:e2e`
  - Optional `npm run test:watch`

Required setup:

- Make `devTauriMock.ts` usable by tests or extract its fixtures into reusable builders.
- Add fixtures for at least one repository in every repository execution state: `Ready`, `Executing`, `AwaitingAcceptance`, `AwaitingCommit`, `AwaitingPush`, `Failed`, and `Cancelled`.
- Add browser tests that launch the Vite app with `?mock=workspace-certification`.
- Add a small performance helper for client-side navigation timing in Playwright.

Certification:

- A trivial Vitest test and a trivial Playwright test pass before any M0 extraction begins.
- `npm run lint`, `npm run build`, `npm run test`, and `npm run test:e2e` are available from `src/CommandCenter.UI`.
- Subsequent M0 work must add or update characterization coverage before moving behavior.

### Workstream 0.1: Centralize Types

- Move all frontend DTOs out of `App.tsx` into `src/types`.
- Import those types from `App.tsx`, feature components, hooks, API modules, and `devTauriMock.ts`.
- Include these type families:
  - Repository and dashboard projections.
  - Workspace projection and artifact inventory.
  - Execution states, session summaries, status, events, context preview, context diagnostics.
  - Git status, dirty state, commit preparation, commit request, push result.
  - Operational-context projection, proposal summary, proposal, semantic change, compression summary, review state, promotion state.
  - Continuity diagnostics, trends, compression trend, and reports.
  - Planning projection and milestones.

Certification:

- No projection DTO definitions remain in `App.tsx`.
- `devTauriMock.ts` imports shared DTOs instead of maintaining a parallel type universe where practical.
- `npm run build` succeeds.

### Workstream 0.2: Centralize Transport

- Create API modules wrapping every Tauri command currently invoked from `App.tsx`.
- Create one low-level `invokeCommand<T>()` wrapper in `src/api/tauri.ts` to normalize unknown errors.
- Create execution event subscription helpers that own `EventSource` construction and cleanup.
- Keep command names and endpoint-specific knowledge inside `src/api`.
- Keep UI components unaware of Tauri command names, backend URLs, request serialization, and SSE framing.

Certification:

- No direct `invoke()` imports remain in rendering components.
- No direct `EventSource` construction remains outside the execution API or event hook.
- Error formatting is centralized.

### Workstream 0.3: Extract Projection Hooks

Create hooks that own loading, refreshing, errors, and cleanup:

- `useRepositories()`
- `useRepositoryWorkspace(repositoryId)`
- `useArtifactContent(repositoryId, relativePath)`
- `useExecutionContextPreview(repositoryId, milestonePath)`
- `useExecutionSession(repositoryId, sessionId)`
- `useExecutionEvents(sessionId)`
- `useGitStatus(repositoryId)`
- `useCommitPreparation(sessionId)`
- `useOperationalContextProposal(repositoryId, proposalId)`
- `useContinuityDiagnostics(repositoryId)`

Rules:

- Hooks may call API modules.
- Hooks may expose `data`, `isLoading`, `error`, and command functions.
- Hooks must not own workflow authority.
- Hooks must not mutate backend state except through explicit command functions called by a user action.

Certification:

- Projection-loading effects are removed from `App.tsx`.
- Existing loading and error behavior is preserved.
- Existing manual refresh behavior is preserved.

### Workstream 0.4: Separate State Boundaries

Create shell/navigation state in `src/state/shellState.ts`.

Navigation state:

- Selected repository id.
- Active primary tab: `workspace`, `execution`, `operational-context`, `continuity`.
- Selected artifact path by repository.
- Selected milestone path by repository.
- Command palette open/closed.
- Optional section anchors and expanded sections.

Projection state:

- Repository dashboard projection.
- Workspace projection.
- Execution projection/status/events.
- Operational context projection/proposal.
- Continuity diagnostics/reports.
- Git and commit projections.

Draft state:

- Artifact editor draft.
- Commit message draft.
- Commit path selection.
- Operational-context proposal edit draft.
- Review note draft.

Certification:

- Changing draft state does not trigger projection reloads.
- Changing tabs does not trigger backend mutations.
- Navigation state never stores projection objects.

### Workstream 0.5: Decompose Without Redesign

- Extract current large rendering sections into feature components while preserving current layout and class names.
- Extract pure helpers from `App.tsx` into `src/lib`, including formatting, markdown rendering, artifact categories, dirty path counts, and workflow-display mapping.
- Keep the user-visible interface unchanged during this milestone.

Certification:

- Existing UI behavior is unchanged.
- The old layout still renders while `App.tsx` becomes substantially smaller.

### Workstream 0.6: Add Characterization Protection

Use the mandatory frontend test infrastructure to protect current behavior before structural redesign.

Minimum scenarios:

- Repository list loads and first repository selection behavior is preserved.
- Selecting a repository loads the same workspace projection.
- Refresh reloads workspace and reconciles selected artifacts.
- Milestone selection builds execution context only when requested.
- Execution events merge by sequence and preserve ordering.
- SSE cleanup occurs when session changes or unmounts.
- Proposal generation, load, edit, accept, reject, and promote keep current gating.
- Commit preparation, selection, commit, and push keep current gating.
- Continuity diagnostics and report generation remain read-only except for explicit report generation.

Use `?mock=workspace-certification` to certify all repository execution states:

- `Ready`
- `Executing`
- `AwaitingAcceptance`
- `AwaitingCommit`
- `AwaitingPush`
- `Failed`
- `Cancelled`

Certification:

- Frontend tests cover the behavior above.
- `npm run lint`, `npm run build`, `npm run test`, `npm run test:e2e`, and `dotnet test CommandCenter.slnx` pass.

## Milestone 1: Design System Foundation

Goal: establish the dark operational visual system without changing information architecture.

### Workstream 1.1: Token System

Create `src/styles/tokens.css` and use CSS variables for:

- Canvas and surfaces:
  - `--bg-canvas`
  - `--bg-inset`
  - `--surface-1`
  - `--surface-2`
  - `--surface-3`
  - `--surface-overlay`
- Borders:
  - `--border-subtle`
  - `--border-default`
  - `--border-strong`
- Text:
  - `--fg-default`
  - `--fg-muted`
  - `--fg-subtle`
  - `--fg-faint`
- Status:
  - success, warning, danger, info, done.
- Accent:
  - subtle, muted, foreground, emphasis.
- Radius:
  - 4px, 6px, 8px, 12px where needed.
- Shadows:
  - panel, popover, modal.

Use the dark console palette consistently. Do not leave major surfaces on the current light theme.

### Workstream 1.2: Typography

Create type tokens for:

- Body.
- Large body.
- Small text.
- Caption.
- Micro uppercase labels.
- H2, H3, H4.
- Mono small.

Rules:

- Use dense, operational typography.
- Keep letter spacing at `0` for normal text.
- Use uppercase letter spacing only for micro labels.
- Do not scale font size with viewport width.

### Workstream 1.3: Shared Primitives

Create visual primitives:

- `Button`: primary, secondary, danger, ghost.
- `IconButton`: for refresh, notification, expand, search, and similar icon actions.
- `Badge` and `StatusBadge`.
- `Panel`.
- `InspectorSection`.
- `Metric`.
- `Table`.
- `Tabs`.
- `EmptyState`.
- `SectionHeader`.

Rules:

- Cards and panels use 8px radius or less unless modal/palette needs 12px.
- Do not nest cards inside cards.
- Use icons for familiar actions where available through the existing dependency set or inline only where no icon library exists.

### Workstream 1.4: Status Language

Centralize status presentation in `src/lib/status.ts`.

Cover:

- `RepositoryAvailability`
- `ExecutionReadiness`
- `RepositoryExecutionState`
- `ExecutionSessionState`
- `OperationalContextProposalStatus`
- `OperationalContextReviewState`
- Continuity warning severity, where projected.

Certification:

- Equivalent states render with the same label, color, and badge style across repository list, header, workspace, execution, operational context, and continuity.

### Workstream 1.5: Apply Theme Without Layout Migration

- Convert current surfaces to the new tokens and primitives.
- Preserve current component hierarchy and user workflows.
- Avoid introducing sidebar, header, command palette, workflow rail changes, or tab changes in this milestone.

Certification:

- The app is visibly on the dark console theme.
- Existing interactions are unchanged.
- `npm run lint`, `npm run build`, and frontend characterization tests pass.

## Milestone 2: Application Shell

Goal: replace the top-level layout with the final shell while keeping existing workflow surfaces mostly intact.

This is a high-risk milestone because it is where navigation state becomes a first-class concern. The shell must feel instant even when backend projections are still loading. Client-owned navigation feedback should happen immediately; asynchronous projection refresh may continue after the navigation response.

### Workstream 2.1: Shell State

Use `shellState.ts` as the single source for client-owned navigation:

- Selected repository.
- Active primary tab.
- Selected artifact.
- Selected milestone.
- Palette visibility.
- Optional section target.

Certification:

- Tab changes mutate navigation state only.
- Repository switching does not mutate backend workflow state.
- In the mock certification app, tab switch visible response is p95 under 100ms.
- In the mock certification app, palette open, close, filter, and item navigation visible response is p95 under 100ms.
- Repository selection visually updates within 100ms and starts workspace loading without blocking the sidebar.
- Repository switching does not trigger duplicate workspace loads for the same selected repository.

### Workstream 2.2: Sidebar

Implement `components/shell/Sidebar.tsx`.

Required content:

- Product identity: Kernritsu, Compass, Command Center.
- Command launcher button with Ctrl+K / Meta+K hint.
- Global navigation entries: Overview, Repositories, Executions, Insights.
- Repository list sourced from dashboard projections.
- Repository row content:
  - Name.
  - Execution state.
  - Proposal indicator from continuity/proposal projection.
  - Dirty indicator only when backed by a projection.
  - Branch only when backed by a projection.

Rules:

- Do not issue per-repository git calls from the sidebar to manufacture branch or dirty data.
- If dashboard projections do not include branch or dirty state, omit those values or show a restrained unavailable state until a backend projection exists.
- Global navigation entries without backend projections should be disabled or route to explicit placeholder surfaces.

### Workstream 2.3: Header

Implement `components/shell/Header.tsx`.

Required content:

- Breadcrumb.
- Selected repository name and path.
- Branch if backed by selected-repository git status or workspace projection.
- Execution state badge.
- Refresh action using existing refresh behavior.
- Notification placement, disabled or empty until real notification data exists.
- Primary action placement.

Rules:

- Header consumes projections and hook command functions.
- Header owns no workflow state.

### Workstream 2.4: Primary Workspace Tabs

Implement `WorkspaceTabs` with:

- Workspace.
- Execution.
- Operational Context.
- Continuity.

Initial tab mapping:

- Workspace hosts the current repository workspace experience.
- Execution hosts existing execution-oriented surfaces.
- Operational Context hosts existing operational-context surfaces.
- Continuity hosts existing continuity diagnostics/report surfaces.

Certification:

- Switching tabs never mutates backend state.
- Existing actions still work from their new locations.
- Tab switch latency remains p95 under 100ms in Playwright against `?mock=workspace-certification`.

### Workstream 2.5: Command Palette v1

Implement `CommandPalette`.

Required behavior:

- Open/close with Ctrl+K or Meta+K.
- Close with Escape and outside click.
- Search/filter visible navigation targets.
- Navigate to repositories.
- Navigate to primary tabs.
- Navigate to major existing sections.

Forbidden in v1:

- Start execution.
- Abort execution.
- Commit.
- Push.
- Accept/reject handoff.
- Generate/edit/accept/reject/promote operational context.

Certification:

- Palette actions only update navigation state.
- Palette owns no workflow authority.
- Palette open, close, filtering, and navigation remain p95 under 100ms in Playwright against `?mock=workspace-certification`.

## Milestone 3: Workspace Migration

Goal: make Workspace the primary operational surface with simultaneous visibility.

This is a major cognitive-flow migration, not a cosmetic recomposition. The current interface is workflow-navigation oriented; the target Workspace is operational-inspection oriented. Treat this as one of the largest milestones. Implement it in small vertical slices and certify each slice before adding the next.

Recommended internal order:

1. Workspace grid skeleton with preserved existing surfaces.
2. Workflow rail.
3. Execution context panel.
4. Live activity panel.
5. Milestones panel.
6. Inspector rail skeleton.
7. Commit/push summary.
8. Operational-context summary.
9. Execution history.
10. Artifact workspace integration and final density pass.

### Workstream 3.1: Workflow Rail

Implement `features/workspace/WorkflowRail.tsx`.

Display steps:

- Context.
- Execution.
- Handoff.
- Commit.
- Push.

Inputs:

- `RepositoryExecutionState`.
- Selected execution context presence.
- Existing execution summary/status where projected.

Rules:

- The rail is display-only.
- It must not trigger transitions.
- UI mapping may convert projected states into visual labels, but must not invent new workflow states.

### Workstream 3.2: Workspace Layout

Implement `features/workspace/WorkspaceTab.tsx`.

Target structure:

```text
WorkflowRail
WorkspaceGrid
  MainColumn
    ExecutionContextPanel
    LiveActivityPanel
    MilestonesPanel
    ArtifactWorkspace or preserved artifact editor placement
  InspectorRail
    CommitPushPanel
    OperationalContextSummary
    ExecutionHistory
```

Use a desktop-first grid with a right rail around 364px wide. Collapse cleanly on narrow viewports.

### Workstream 3.3: Execution Context Panel

Display from `ExecutionContextPreview`:

- Artifact role.
- Relative path.
- Byte count.
- Character count.
- Per-artifact warnings and hard-limit status.
- Aggregate bytes and characters.
- Warning/hard thresholds.
- Missing optional artifacts.
- Validation errors.
- Launch blocked status.
- Repository snapshot branch and dirty-state summary.

Rules:

- Do not recalculate aggregate totals or validation.
- Use backend diagnostics as the source of truth.

### Workstream 3.4: Live Activity Panel

Display the current execution stream:

- Timestamp.
- Event type.
- Provider/session context where projected.
- Message.

Rules:

- Reuse `useExecutionEvents`.
- Do not create a second event store.
- Workspace and Execution tab must see the same event data.

### Workstream 3.5: Milestones Panel

Display milestones from artifact inventory and planning projection if needed:

- Current selected milestone.
- Milestone file path/name.
- Status when projected.
- Progress only when projected.

Rules:

- Do not fabricate criteria counts or progress metrics.
- If only milestone files are available, show file names and selection state.

### Workstream 3.6: Inspector Rail

Required sections:

- Commit and push summary:
  - Current repository state.
  - Commit preparation status when available.
  - Selected/generated change scope when available.
  - Ahead/behind only when backed by git status.
  - Explicit commit/push actions only in valid workflow states.
- Operational context summary:
  - Revision count.
  - Stable decision count.
  - Open question count.
  - Active risk count.
  - Pending proposal status.
  - Link to Operational Context tab.
- Execution history:
  - Recent sessions.
  - Milestone.
  - State.
  - Duration.
  - Timestamp.
  - Commit/push summary when projected.

Rules:

- Inspector sections summarize and navigate.
- They do not own workflow state.

### Workstream 3.7: Workspace Cross-Links

Add the cross-workspace links introduced by the Workspace tab:

- Operational-context summary navigates to the Operational Context tab and proposal/current-understanding section.
- Continuity warning snippets, if shown in the inspector, navigate to the Continuity tab and warning section.
- Execution activity and execution history rows navigate to the Execution tab for the selected session.
- Milestone rows update selected milestone navigation state and can navigate to the execution context panel.
- Pending handoff, commit, and push summary states navigate to the corresponding Workspace inspector section only.

Rules:

- Links update navigation state and optional section anchors only.
- Links do not refresh projections, start execution, accept handoffs, commit, push, generate proposals, or promote proposals.

Certification:

- Execution context, activity, milestones, commit/push, operational context, and history are co-visible on desktop.
- Existing artifact editing, execution start, handoff review, commit, push, operational-context review, and continuity actions remain reachable.
- Workspace links navigate without backend mutation.
- The Workspace tab can answer what is planned, what is happening, what changed, what understanding exists, and what comes next without requiring tab hopping.

## Milestone 4: Execution Workspace

Goal: create a dedicated execution inspection workspace.

### Workstream 4.1: Execution Layout

Implement `features/execution/ExecutionTab.tsx`.

Target structure:

```text
Main stream panel
Right rail
  Session panel
  Context diagnostics panel
  Execution diagnostics panel
  Launch readiness panel
```

The execution stream gets primary visual weight.

### Workstream 4.2: Full Execution Stream

Display:

- Timestamp.
- Event type.
- Provider.
- Status.
- Session id.
- Message.

Rules:

- Use the same execution event hook as Workspace.
- Do not create client replay, client event persistence, or a second polling model.

### Workstream 4.3: Session Panel

Display:

- Provider name.
- Session id.
- Provider process id.
- Provider executable path.
- Started at.
- Completed at.
- Duration.
- Current session state.
- Repository execution state.
- Handoff path.
- Failure reason.

Abort behavior:

- Keep abort hidden or disabled until backend-owned abort exists.
- If abort is later implemented, add backend service, endpoint, Tauri command, tests, projection updates, and UI action together.

### Workstream 4.4: Context Diagnostics

Display from `ExecutionContextDiagnostics`:

- Artifact count.
- Aggregate size.
- Warning threshold.
- Hard limit.
- Validation errors.
- Missing optional artifacts.
- Launch blocked.
- Per-artifact diagnostics.

### Workstream 4.5: Execution Diagnostics

Display from execution projections/events:

- Current state.
- Last activity.
- Recent failures.
- Monitoring warnings where projected.

Do not infer failures from event text.

### Workstream 4.6: Execution Cross-Links

Add links introduced by the Execution workspace:

- Session milestone navigates to the Workspace milestone/context area.
- Context diagnostics navigates to the Workspace execution context panel when users need the broader package view.
- Handoff path navigates to the artifact workspace or handoff review surface.
- Commit and push references navigate to the Workspace inspector commit/push section.
- Failure or warning references navigate only to visible projected diagnostics; do not infer targets from raw event text.

Rules:

- Links navigate only.
- Execution events remain immutable observations.

Certification:

- Workspace execution summary and Execution tab agree for the same session.
- Abort affordance accurately reflects real capability.
- Execution cross-links do not create a second execution model or mutate workflow state.

## Milestone 5: Operational Context Workspace

Goal: make operational context review a first-class workspace without changing backend lifecycle behavior.

### Workstream 5.1: Current Understanding

Display from `OperationalContextProjection`:

- Current understanding summary.
- Architecture.
- Authority boundaries.
- Constraints.
- Stable decisions.
- Decision rationale.
- Open questions.
- Active risks.
- Recent understanding changes.
- Continuity warnings.
- Revision number, revision count, current path, last updated, and last promotion.

Rules:

- Sections come from projection fields.
- Do not parse Markdown client-side to reconstruct understanding.

### Workstream 5.2: Proposed Revision

Display from `OperationalContextProposal`:

- Proposal id.
- Status.
- Generated at.
- Generated content location.
- Edited content location.
- Review state.
- Promotion state.
- Current vs proposed understanding where practical.

Reviewers should be able to evaluate understanding changes without relying only on raw Markdown. Keep the raw Markdown editor available for edits.

### Workstream 5.3: Semantic Changes

Group `OperationalContextSemanticChange` records by semantic category:

- Decision added/removed/warning.
- Constraint added/removed.
- Question added/resolved/removed.
- Risk added/retired/removed.
- Rationale changed/warning.
- Section added/removed/changed.
- Preservation warning.

Rules:

- Show projected semantic changes.
- Do not compute a new diff in React.

### Workstream 5.4: Compression Effects

Display from `OperationalContextCompressionSummary`:

- Preserved item count.
- Added item count.
- Modified item count.
- Removed item count.
- Compressed item count.
- Permanent/active/historical/noise item counts.
- Resolved question count.
- Retired risk count.
- Warnings.
- Revision summary.
- Noise removed indicators.
- Stable-understanding retention warnings.

Compression is review metadata. It must not block actions unless backend state already does.

### Workstream 5.5: Decision Continuity

Display:

- Stable decisions.
- Open decision signals where projected.
- Decision rationale.
- Decision warnings from semantic changes and compression warnings.
- Missing rationale warnings where projected.

Do not turn this workspace into a decision archive viewer.

### Workstream 5.6: Review Actions

Expose existing actions with existing gating:

- Generate proposal.
- Load latest proposal.
- Edit/save proposed content.
- Accept.
- Reject.
- Promote accepted proposal.

Rules:

- Backend state controls whether actions are enabled.
- UI does not invent lifecycle transitions.
- Refresh workspace projection after lifecycle mutations, preserving selected artifact and tab state.

### Workstream 5.7: Operational Context Cross-Links

Add links introduced by the Operational Context workspace:

- Open questions and active risks navigate to the same section anchors from palette/discovery targets.
- Continuity warnings navigate to the Continuity tab and warning section.
- Compression warnings navigate to the Continuity tab and compression section.
- Decision continuity warnings navigate to the continuity decision-retention section when available.
- Proposal source paths navigate to artifact content surfaces when the artifact exists.
- Promotion archive paths navigate to artifact surfaces when the artifact exists.

Rules:

- Links navigate only.
- Links do not generate, edit, accept, reject, or promote proposals.
- Operational context remains current understanding, not a decision archive or execution history browser.

Certification:

- Proposal lifecycle behavior matches existing behavior.
- Current understanding remains distinct from proposal, decisions history, execution history, and raw session memory.
- Operational Context links do not mutate lifecycle state.

## Milestone 6: Continuity Workspace

Goal: make understanding evolution observable without creating continuity governance.

### Workstream 6.1: Understanding Evolution

Display a table from `ContinuityDiagnostics`:

Columns:

- Section.
- Added.
- Removed.
- Resolved.
- Lost.

Rows:

- Architecture.
- Constraints.
- Stable decisions.
- Rationale.
- Open questions.
- Active risks.

Rules:

- Display observed diagnostic counts.
- Do not compute a score.
- Do not add gates.

### Workstream 6.2: Continuity Warnings

Display:

- Continuity warnings.
- Compression warnings.
- Decision/rationale warnings.
- Repeated investigation indicators.
- Repeated question indicators.
- Decision rework indicators.

Warnings are observations. They must not block actions.

### Workstream 6.3: Compression Trends

Display from `compressionTrend`:

- Proposal count.
- Compressed item count.
- Removed item count.
- Resolved question count.
- Retired risk count.
- Warning count.
- Warnings.
- Noise removed indicators.

### Workstream 6.4: Decision, Question, and Risk Lifecycle

Display:

- Stable decisions retained/removed where diagnostics expose it.
- Rationale preservation and warnings.
- Questions added/resolved/lost.
- Risks added/retired/lost.

Use existing trend fields and reports. If a specific lifecycle value is not projected, omit it or mark it unavailable.

### Workstream 6.5: Reports

Display continuity report visibility:

- Latest report.
- Report history.
- Report generated time.
- Relative path.
- Diagnostics summary.

Rules:

- Reports are supporting artifacts.
- Corrupt or unreadable reports should remain safely ignored by backend behavior.

### Workstream 6.6: Continuity Cross-Links

Add links introduced by the Continuity workspace:

- Understanding evolution rows navigate to corresponding Operational Context sections.
- Warning rows navigate to the relevant Continuity subsection when available.
- Decision warnings navigate to Operational Context stable decisions or decision rationale sections when available.
- Question lifecycle rows navigate to Operational Context open questions.
- Risk lifecycle rows navigate to Operational Context active risks.
- Report paths navigate to artifact/report surfaces when the backend projection exposes a valid relative path.

Rules:

- Links navigate only.
- Continuity diagnostics remain observational and never become workflow gates.

Certification:

- Continuity tab gives complete observable state available from projections.
- It does not create scores, gates, auto-correction, auto-promotion, or auto-rejection.
- Continuity links do not mutate lifecycle state.

## Milestone 7: Navigation, Discovery, and Cohesion

Goal: make the application feel like one operational environment rather than separate screens.

### Workstream 7.1: Command Palette v2

Expand navigation targets:

- Repositories.
- Repository Workspace.
- Repository Execution.
- Repository Operational Context.
- Repository Continuity.
- Open questions.
- Active risks.
- Stable decisions.
- Continuity warnings.
- Milestones.
- Execution sessions.
- Inspector sections.

Rules:

- Targets must be built from existing projections.
- Selecting a target only changes navigation state and optional section anchor.
- No workflow mutations.

### Workstream 7.2: Cross-Workspace Link Hardening

Audit and complete the links introduced during Workspace, Execution, Operational Context, and Continuity implementation:

- Operational-context summary -> Operational Context tab.
- Continuity warnings -> Continuity tab and warning section.
- Execution summaries -> Execution tab.
- Milestones -> Workspace milestone section or execution context selection.
- Pending proposal -> Operational Context proposal section.
- Context diagnostics -> Workspace execution context panel.
- Decision/rationale warnings -> relevant Operational Context or Continuity section.
- Report paths -> report/artifact surfaces where projected.

Rules:

- Links navigate only and preserve selected repository context.
- Links do not refresh, mutate, or trigger workflows.
- Broken or unavailable link targets degrade to the nearest valid workspace section.

### Workstream 7.3: Context Preservation

Preserve:

- Selected repository.
- Active tab per repository where useful.
- Selected artifact per repository.
- Selected milestone per repository.
- Expanded sections.
- Current palette query until close.

Certification:

- Switching tabs does not wipe drafts unless the draft's owning object changes.
- Switching repositories restores that repository's selected artifact and milestone when still valid.

### Workstream 7.4: Discovery Layer

Expose projection-derived shortcuts:

- Pending proposal.
- Current execution.
- Awaiting handoff review.
- Awaiting commit.
- Awaiting push.
- Continuity warnings.
- Open questions.
- Active risks.

Rules:

- Discovery surfaces point to projected information.
- They do not interpret text, score health, or compute product meaning.

### Workstream 7.5: Cohesion Audit

Audit:

- Status labels.
- Empty states.
- Loading states.
- Error states.
- Disabled capability states.
- Layout density.
- Keyboard behavior.
- Focus behavior.
- Responsive behavior.

Certification:

- Similar concepts behave similarly across Workspace, Execution, Operational Context, and Continuity.

## Milestone 8: Capability Gaps, Cleanup, and Final Validation

Goal: resolve remaining discrepancies between visible UX and real capability, then remove legacy migration scaffolding.

### Workstream 8.1: Capability Gap Closure

Classify every unresolved target affordance as:

- Implemented.
- Deferred.
- Rejected.

Known gaps to resolve:

- User-invokable abort execution.
- Global Overview.
- Global Executions.
- Insights.
- Notifications.
- Dashboard/sidebar branch and dirty state for all repositories.
- Ahead/behind counts outside selected repository git status.
- Milestone criteria progress.
- Cross-repository execution views.
- Cross-repository continuity/insight rollups.

Rules:

- Implement only with backend projection or backend capability.
- Deferred and rejected items must be explicit in product UI or docs.

### Workstream 8.2: Abort Execution Decision

Option A: implement abort.

Required backend work:

- Add service contract method for abort/cancel.
- Add execution-session state transition tests.
- Add provider/process cancellation behavior.
- Add monitoring event.
- Add endpoint.
- Add Tauri command.
- Update workspace/session projections.
- Add UI action and disabled/error states.

Option B: omit or disable abort.

Required UI work:

- Remove active abort affordance.
- Show a restrained disabled state only if useful.
- Ensure no palette abort command exists.

### Workstream 8.3: Global Navigation Decisions

Overview, Executions, and Insights need backend-backed behavior before they become functional product surfaces.

Possible outcomes:

- Overview becomes a repository landing page using dashboard projections only.
- Executions becomes a cross-repository execution projection after backend support.
- Insights becomes a continuity/operational insight projection after backend support.
- Any of these can remain disabled/deferred until backend authority exists.

### Workstream 8.4: Repository Summary Completion

If the final sidebar/header requires branch, dirty count, ahead, behind, criteria progress, or rollups for every repository, add them to backend projections rather than calling git repeatedly from React.

Potential backend additions:

- Extend `RepositoryDashboardProjection` with branch, dirty count, ahead count, behind count, and captured timestamp.
- Add milestone criteria projection only if criteria parsing is a real backend capability.
- Add tests in `RepositoryProjectionServiceTests` and `GitServiceTests`.
- Update Tauri DTOs and frontend shared types.

### Workstream 8.5: Notifications Strategy

Choose one:

- Implement a backend-backed notification projection.
- Keep notification icon as disabled placement.
- Remove notification UI.

Do not show fake notification counts.

### Workstream 8.6: Deviation Ledger

Create `docs/frontend-modernization-deviations.md` before final validation.

Record every intentional difference between the target UX and the real product:

- Description.
- Location or surface.
- Reason.
- Category:
  - Capability.
  - Product decision.
  - Technical constraint.
- Outcome:
  - Implemented differently.
  - Deferred.
  - Rejected.
- Required backend projection or capability, if any.
- Follow-up owner or issue reference, if known.

Rules:

- A missing capability is not a defect if it is recorded and represented honestly in the UI.
- An unrecorded mismatch found during final validation is a defect.
- The ledger must be self-contained and explain each difference directly.

### Workstream 8.7: Remove Legacy Structure

- Delete unused CSS from the old layout.
- Delete duplicate DTOs.
- Delete obsolete helpers after feature modules own them.
- Remove any temporary migration adapters.
- Keep `devTauriMock.ts` aligned with shared types and current visible states.
- Ensure `App.tsx` is only composition.

### Workstream 8.8: Final UX Validation

Validate:

- Layout.
- Navigation.
- Interaction.
- Workspace structure.
- Information density.
- Projection ownership.
- Workflow ownership.
- Responsive behavior.
- Keyboard behavior.
- Error/loading/empty states.

Every remaining deviation must be classified as:

- Intentional product decision.
- Capability-based deferral.
- Defect to fix.

Certification:

- Every intentional or capability-based deviation is recorded in `docs/frontend-modernization-deviations.md`.
- Every defect is fixed or converted into an explicit deferred/rejected product decision before completion.

## Verification Plan

Run these gates at minimum:

```powershell
dotnet test CommandCenter.slnx
cd src\CommandCenter.UI
npm run lint
npm run build
```

From Milestone 0 onward, also run:

```powershell
cd src\CommandCenter.UI
npm run test
npm run test:e2e
```

Manual or browser-driven certification should cover:

- Desktop 1440x900.
- Desktop 1280x800.
- Narrow/mobile around 390x844.
- `?mock=workspace-certification`.
- Real backend through Tauri/dev server where available.

Critical scenarios:

- Register repository.
- Remove repository registration.
- Select repository.
- Refresh workspace.
- Select artifact.
- Edit and save artifact.
- Rotate current handoff.
- Rotate current decisions.
- Select milestone.
- Build execution context.
- Start execution.
- Stream execution events.
- Accept generated handoff.
- Reject generated handoff.
- Prepare commit.
- Select commit scope.
- Commit.
- Push.
- Generate operational-context proposal.
- Edit proposal.
- Accept proposal.
- Reject proposal.
- Promote proposal.
- Load continuity diagnostics.
- Generate continuity report.
- Navigate with tabs.
- Navigate with command palette.
- Navigate cross-workspace links.

## Definition of Done

The modernization is done when:

- `App.tsx` is a thin composition root.
- Projection DTOs live in `src/types`.
- Transport lives in `src/api`.
- Projection loading lives in hooks.
- Navigation state, projection state, and draft state are separated.
- The dark operational design system is tokenized and reused.
- Sidebar, header, primary tabs, command palette, workflow rail, Workspace inspector, Execution workspace, Operational Context workspace, and Continuity workspace exist.
- All displayed values come from backend projections or explicit local draft/navigation state.
- All workflow mutations use existing or newly added backend-owned commands.
- Missing capabilities are either implemented backend-first or explicitly disabled/deferred.
- Intentional UX/capability deviations are recorded in `docs/frontend-modernization-deviations.md`.
- Shell navigation latency remains within the defined p95 bounds in the mock certification app.
- Legacy CSS, duplicate components, duplicate DTOs, and unused migration scaffolding are removed.
- Backend tests, frontend lint/build, and frontend characterization/e2e tests pass.
