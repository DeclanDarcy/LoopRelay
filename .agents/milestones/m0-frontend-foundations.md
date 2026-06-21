# Milestone 0: Frontend Foundations

## Tracking

- [ ] Milestone complete
- [x] Workstream 0.0: Mandatory Frontend Test Infrastructure
- [x] Workstream 0.1: Centralize Types
- [x] Workstream 0.2: Centralize Transport
- [ ] Workstream 0.3: Extract Projection Hooks
- [ ] Workstream 0.4: Separate State Boundaries
- [ ] Workstream 0.5: Decompose Without Redesign
- [x] Workstream 0.6: Add Characterization Protection
- [ ] Certification complete

Goal: create stable type, transport, hook, and state boundaries with no visible UX change.

## Workstream 0.0: Mandatory Frontend Test Infrastructure

Add frontend test infrastructure before extracting types, APIs, hooks, or components.

Required tooling:

- [x] Vitest for unit and hook tests.
- [x] Testing Library for component behavior tests.
- [x] Playwright for browser-level characterization and layout/navigation checks.
- [x] A stable render helper for components that need shell state, mock Tauri commands, and projection fixtures.
- [ ] Script entries:
  - [x] `npm run test`
  - [x] `npm run test:e2e`
  - [x] Optional `npm run test:watch`

Required setup:

- [x] Make `devTauriMock.ts` usable by tests or extract its fixtures into reusable builders.
- [x] Add fixtures for at least one repository in every repository execution state: `Ready`, `Executing`, `AwaitingAcceptance`, `AwaitingCommit`, `AwaitingPush`, `Failed`, and `Cancelled`.
- [x] Add browser tests that launch the Vite app with `?mock=workspace-certification`.
- [x] Add a small performance helper for client-side navigation timing in Playwright.

### Certification

- [x] A trivial Vitest test and a trivial Playwright test pass before any M0 extraction begins.
- [x] `npm run lint`, `npm run build`, `npm run test`, and `npm run test:e2e` are available from `src/CommandCenter.UI`.
- [x] Subsequent M0 work must add or update characterization coverage before moving behavior.

## Workstream 0.1: Centralize Types

- [x] Move all frontend DTOs out of `App.tsx` into `src/types`.
- [x] Import those types from `App.tsx`, feature components, hooks, API modules, and `devTauriMock.ts`.
- [x] Include these type families:
  - [x] Repository and dashboard projections.
  - [x] Workspace projection and artifact inventory.
  - [x] Execution states, session summaries, status, events, context preview, context diagnostics.
  - [x] Git status, dirty state, commit preparation, commit request, push result.
  - [x] Operational-context projection, proposal summary, proposal, semantic change, compression summary, review state, promotion state.
  - [x] Continuity diagnostics, trends, compression trend, and reports.
  - [x] Planning projection and milestones.

### Certification

- [x] No projection DTO definitions remain in `App.tsx`.
- [x] `devTauriMock.ts` imports shared DTOs instead of maintaining a parallel type universe where practical.
- [x] `npm run build` succeeds.

## Workstream 0.2: Centralize Transport

- [x] Create API modules wrapping every Tauri command currently invoked from `App.tsx`.
- [x] Create one low-level `invokeCommand<T>()` wrapper in `src/api/tauri.ts` to normalize unknown errors.
- [x] Create execution event subscription helpers that own `EventSource` construction and cleanup.
- [x] Keep command names and endpoint-specific knowledge inside `src/api`.
- [x] Keep UI components unaware of Tauri command names, backend URLs, request serialization, and SSE framing.

### Certification

- [x] No direct `invoke()` imports remain in rendering components.
- [x] No direct `EventSource` construction remains outside the execution API or event hook.
- [x] Error formatting is centralized.

## Workstream 0.3: Extract Projection Hooks

Create hooks that own loading, refreshing, errors, and cleanup:

- [x] `useRepositories()`
- [x] `useRepositoryWorkspace(repositoryId)`
- [x] `useArtifactContent(repositoryId, relativePath)`
- [x] `useExecutionContextPreview(repositoryId, milestonePath)`
- [x] `useExecutionSession(repositoryId, sessionId)`
- [x] `useExecutionEvents(sessionId)`
- [x] `useGitStatus(repositoryId)`
- [ ] `useCommitPreparation(sessionId)`
- [ ] `useOperationalContextProposal(repositoryId, proposalId)`
- [x] `useContinuityDiagnostics(repositoryId)`

Closure audit disposition:

- [x] Certified extracted read-only projection hooks have one frontend loading authority.
- [x] Deferred `useCommitPreparation(sessionId)` because current commit preparation is workflow-review setup that initializes commit message draft, path selection, and commit readiness.
- [x] Deferred `useOperationalContextProposal(repositoryId, proposalId)` because current proposal loading initializes proposal edit draft, review note draft, and operational-context comparison content.
- [x] Recorded authority matrix in `.agents/audits/m0-closure-authority-matrix.md`.

Rules:

- [x] Hooks may call API modules.
- [x] Hooks may expose `data`, `isLoading`, `error`, and command functions.
- [x] Hooks must not own workflow authority.
- [x] Hooks must not mutate backend state except through explicit command functions called by a user action.

### Certification

- [ ] Projection-loading effects are removed from `App.tsx`.
- [ ] Existing loading and error behavior is preserved.
- [ ] Existing manual refresh behavior is preserved.

Audit note: remaining direct `App.tsx` loads are accepted for M0 only where they are workflow review setup, draft initialization, comparison-content loading, or post-mutation reconciliation.

## Workstream 0.4: Separate State Boundaries

Create shell/navigation state in `src/state/shellState.ts`.

Navigation state:

- [x] Selected repository id.
- [x] Active primary tab: `workspace`, `execution`, `operational-context`, `continuity`.
- [x] Selected artifact path by repository.
- [x] Selected milestone path by repository.
- [x] Command palette open/closed.
- [ ] Optional section anchors and expanded sections.

Projection state:

- [ ] Repository dashboard projection.
- [ ] Workspace projection.
- [ ] Execution projection/status/events.
- [ ] Operational context projection/proposal.
- [ ] Continuity diagnostics/reports.
- [ ] Git and commit projections.

Draft state:

- [x] Artifact editor draft.
- [ ] Commit message draft.
- [ ] Commit path selection.
- [ ] Operational-context proposal edit draft.
- [ ] Review note draft.

### Certification

- [x] Changing artifact draft state does not trigger projection reloads.
- [x] Changing tabs does not trigger backend mutations.
- [x] Navigation state never stores projection objects.
- [x] M0 closure audit classifies remaining commit/proposal/review draft state as intentionally local and not shell/navigation state.

## Workstream 0.5: Decompose Without Redesign

- [ ] Extract current large rendering sections into feature components while preserving current layout and class names.
- [ ] Extract pure helpers from `App.tsx` into `src/lib`, including formatting, markdown rendering, artifact categories, dirty path counts, and workflow-display mapping.
- [ ] Keep the user-visible interface unchanged during this milestone.

Slice progress:

- [x] Added `src/lib` extraction for formatting helpers, artifact category/path helpers, and dirty-path counting.
- [x] Added `src/lib/markdown.tsx` extraction for current markdown preview rendering.
- [x] Added `src/lib/executionWorkflow.ts` extraction for current execution workflow rail display mapping.
- [x] Added `src/lib/operationalContext.ts` extraction for current operational-context section item display parsing.
- [x] Centralized execution event merge behavior in `useExecutionEvents` for both SSE streams and status snapshot composition.
- [x] Extracted git path bucket rendering as presentation-only `GitPathBucket` with characterization coverage for empty and ordered path lists.
- [x] Extracted execution event feed rendering as presentation-only `ExecutionEventFeed` with characterization coverage for empty and ordered event rows.
- [x] Extracted execution session summary rendering as presentation-only `ExecutionSessionPanel` with characterization coverage for active-session labels and missing-field fallbacks.
- [x] Extracted execution history rendering as presentation-only `ExecutionHistoryPanel` with characterization coverage for empty history, provided order, labels, and missing-field fallbacks.
- [x] Extracted execution context summary row rendering as presentation-only `ExecutionContextSummaryRows` with characterization coverage for existing labels and caller-provided status strings.
- [x] Extracted execution context artifact list rendering as presentation-only `ExecutionContextArtifactList` with characterization coverage for provided order, labels, and empty-list behavior.
- [x] Extracted execution context missing optional list rendering as presentation-only `ExecutionContextMissingOptionalList` with characterization coverage for provided order and the existing `None` fallback.
- [x] Audited remaining execution context preview regions in `.agents/audits/m0-execution-context-preview-inventory.md` before authorizing further extraction.
- [x] Extracted execution context validation list rendering as presentation-only `ExecutionContextValidationList` with characterization coverage for empty state, backend-provided ordering, and verbatim message text.
- [x] Re-inventoried remaining execution context preview regions in `.agents/audits/m0-execution-context-preview-inventory.md` after validation-list extraction.
- [x] Extracted repository snapshot rendering as presentation-only `ExecutionRepositorySnapshotPanel` with characterization coverage for missing snapshot, branch fallback, clean/dirty label, captured timestamp formatting, and path buckets.
- [x] Reassessed remaining execution context preview regions in `.agents/audits/m0-execution-context-preview-inventory.md` after repository snapshot extraction.
- [x] Extracted artifact diagnostics rendering as presentation-only `ExecutionContextArtifactDiagnosticsList` with characterization coverage for provided order, byte-count text, existing warning/hard-limit suffix labels, and empty-list behavior.
- [x] Reassessed remaining execution context preview regions in `.agents/audits/m0-execution-context-preview-inventory.md` after artifact diagnostics extraction.
- [x] Extracted artifact content preview rendering as presentation-only `ExecutionContextArtifactContentPreviews` with characterization coverage for provided order, summary labels, `OperationalContext` default-open behavior, markdown rendering, and the `Empty artifact.` fallback.
- [x] Completed the authorized execution-context preview extraction inventory for M0.5.
- [x] Extracted continuity diagnostics body rendering as presentation-only `ContinuityDiagnosticsPanel` with characterization coverage for existing summary labels, rounded average text, preservation/compression labels, repeated-signal ordering, and empty repeated/warning fallbacks.
- [x] Extracted current operational-context display rendering as presentation-only `OperationalContextCurrentPanel` with characterization coverage for summary labels, section ordering, item text, empty section fallbacks, missing-context fallback, and proposal status fallbacks.
- [x] Extracted repository dashboard item content rendering as presentation-only `RepositoryDashboardItemContent` with characterization coverage for projected labels, execution summary metadata, and missing/null continuity fallbacks.
- [x] Extracted selected repository summary rendering as presentation-only `SelectedRepositorySummary` with characterization coverage for repository identity, workspace-over-dashboard facts, execution detail fallbacks, and artifact presence summary labels.
- [x] Extracted artifact editor metadata and markdown preview rendering as presentation-only `ArtifactMetadata` and `ArtifactMarkdownPreview` with characterization coverage for existing labels, loading fallback, markdown rendering, and empty fallback.
- [x] Extracted operational-context proposal summary and compression summary rendering as presentation-only `OperationalContextProposalSummaryPanel` and `OperationalContextCompressionSummaryPanel` with characterization coverage for existing labels, timestamp/null fallbacks, optional section ordering, and empty optional-section behavior.
- [x] Extracted loaded operational-context proposal status rendering as presentation-only `OperationalContextProposalStatusPanel` with characterization coverage for metadata labels, date/none fallbacks, stale-review notice, and promotion failure notices.
- [x] Extracted operational-context semantic change rendering as presentation-only `OperationalContextSemanticChangeList` with characterization coverage for the existing heading, empty fallback, backend-provided ordering, and type/description labels.
- [x] Audited decision-continuity review and deliberately left it in `App.tsx` because the block carries acceptance guidance and sits inside proposal review coordination.
- [x] Extracted operational-context proposal comparison rendering as presentation-only `OperationalContextProposalComparison` with characterization coverage for headings, empty fallbacks, and existing markdown rendering.
- [x] Audited generated-handoff review and extracted only neutral generated-content rendering as `GeneratedHandoffContent` with characterization coverage for loading, empty, and markdown behavior.
- [x] Left generated-handoff path metadata, accept/reject buttons, decision pending state, confirmation, generated-handoff loading ownership, and backend decision commands in `App.tsx`.
- [x] Left feature component extraction in `App.tsx` for later Workstream 0.5 slices.

### Certification

- [ ] Existing UI behavior is unchanged.
- [ ] The old layout still renders while `App.tsx` becomes substantially smaller.

## Workstream 0.6: Add Characterization Protection

Use the mandatory frontend test infrastructure to protect current behavior before structural redesign.

Minimum scenarios:

- [x] Repository list loads and first repository selection behavior is preserved.
- [x] Selecting a repository loads the same workspace projection.
- [x] Refresh reloads workspace and reconciles selected artifacts.
- [x] Markdown preview rendering preserves current headings, lists, paragraphs, fenced code blocks, and literal unsupported markdown behavior.
- [x] Execution workflow rail mapping preserves current ready, previewed, executing, review, commit, push, completed, and failed display states.
- [x] Operational-context section parsing preserves h2 section matching, list ordering, trimming, flattened nested bullets, section omission, and empty-state behavior.
- [x] Milestone selection builds execution context only when requested.
- [x] Execution events merge by sequence and preserve ordering.
- [x] SSE cleanup occurs when session changes or unmounts.
- [x] Proposal generation, load, edit, accept, reject, and promote keep current gating.
- [x] Commit preparation, selection, commit, and push keep current gating.
- [x] Continuity diagnostics and report generation remain read-only except for explicit report generation.
- [x] Artifact save and current handoff/decisions rotation remain local/draft-only except for explicit artifact actions.
- [x] Execution launch and generated handoff accept/reject remain projection/review-only except for explicit execution actions.

Use `?mock=workspace-certification` to certify all repository execution states:

- [x] `Ready`
- [x] `Executing`
- [x] `AwaitingAcceptance`
- [x] `AwaitingCommit`
- [x] `AwaitingPush`
- [x] `Failed`
- [x] `Cancelled`

### Certification

- [x] Frontend tests cover the behavior above.
- [ ] `npm run lint`, `npm run build`, `npm run test`, `npm run test:e2e`, and `dotnet test CommandCenter.slnx` pass.

Closure audit note: M0 already has boundary characterization for transport, shell navigation, extracted projection hooks, execution event cleanup/order, certification fixture state coverage, and artifact-draft projection isolation. Remaining workflow characterization should be added alongside Workstream 0.5 decomposition and feature workspace migration.

Slice note: `app.smoke.test.tsx` now characterizes milestone selection as navigation state only. Changing the selected milestone does not invoke `preview_execution_context`; only the explicit `Build Execution Context` action invokes the backend preview command for the selected repository and milestone.

Slice note: `app.smoke.test.tsx` now characterizes commit workflow authority. Selecting an awaiting-commit repository does not invoke `prepare_commit`; only the Git Workflow refresh action prepares the commit review. Editing the commit message and changing selected paths remain local draft state and do not invoke `prepare_commit` or `commit_execution`; only `Commit Selected` invokes `commit_execution` with the selected path set and preparation snapshot. Selecting or refreshing an awaiting-push repository does not invoke `push_execution`; only `Push Commit` invokes the backend push command.

Slice note: `app.smoke.test.tsx` now characterizes operational-context proposal workflow authority. Repository/artifact navigation and draft edits do not invoke proposal workflow commands. Existing proposals are loaded only through `Load Latest`; generation, edit save, accept, reject, and promote each require their explicit action and send the selected repository/proposal payload.

Slice note: `app.smoke.test.tsx` now characterizes continuity diagnostics/report authority. Diagnostics initial load, repository selection, and `Refresh Diagnostics` remain read-only projection retrieval through `get_continuity_diagnostics`; repository/artifact navigation does not invoke report generation; only `Generate Report` invokes `generate_continuity_report` for the selected repository.

Slice note: `app.smoke.test.tsx` now characterizes artifact mutation authority. Artifact draft edits do not invoke save or rotation commands; only `Save` invokes `save_artifact_content`; only confirmed `Rotate` while a current handoff or current decisions artifact is selected invokes the matching rotation command.

Slice note: `app.smoke.test.tsx` now characterizes execution launch and generated handoff decision authority. Rendering, repository navigation, and context build do not invoke workflow mutations; only `Start Execution` invokes `start_execution`; only `Accept Handoff` or confirmed `Reject Handoff` invokes the corresponding generated-handoff decision command.

Slice note: `.agents/audits/m0-closure-authority-matrix.md` now includes a complete workflow-mutating frontend command inventory for M0.6. Workstream 0.6 is closed; return to M0.5 decomposition before final M0 certification.
