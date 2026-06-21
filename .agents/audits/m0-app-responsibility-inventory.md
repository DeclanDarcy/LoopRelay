# M0 App Responsibility Inventory

## Scope

Captured after extracting `useGitStatus(repositoryId)` and updated during the M0 closure authority audit after `useContinuityDiagnostics(repositoryId)` and shell navigation state were in place. This is an inventory only; no refactor is authorized by this artifact.

## Remaining `App.tsx` Responsibilities

### Navigation State

- Hook-owned in `useShellState()`:
  - `selectedRepositoryId`
  - `selectedArtifactPath`
  - `selectedMilestonePath`
  - selected artifact paths by repository
  - selected milestone paths by repository
  - active primary tab
  - command palette open/closed state

### Projection State

- Hook-owned: repositories, workspace, artifact content, execution context preview, execution session status, execution events, git status, continuity diagnostics.
- Still `App.tsx`-owned: operational-context proposal, generated handoff content, operational-context current content used while reviewing proposals.
- Action-coupled refreshes still in `App.tsx`: workspace refresh after save, rotate, accept/reject, commit, and push workflows.

### Draft State

- `draftContent`
- `commitMessage`
- `selectedCommitPaths`
- `operationalContextProposalDraft`
- `operationalContextReviewNote`

### Workflow Actions

- Repository registration/removal.
- Artifact save and artifact rotation.
- Execution start.
- Generated handoff load, accept, and reject.
- Commit preparation, commit, and push.
- Operational-context proposal generate, edit, accept, reject, and promote.
- Continuity report generation.

### Workflow Gating

- Execution launch readiness and blocked reason.
- Generated handoff review readiness.
- Commit preparation currency and commit readiness.
- Push readiness.
- Operational-context review/promote readiness.

### View Composition And Presentation

- Top-level repository selection, artifact selection, milestone selection, workflow rail, context panels, git workflow panel, operational-context review, continuity diagnostics, and history rendering remain composed in `App.tsx`.

## Projection Ownership Audit

- `useRepositories()` owns repository dashboard projection loading.
- `useRepositoryWorkspace(repositoryId)` owns workspace projection loading and manual workspace refresh.
- `useArtifactContent(repositoryId, relativePath)` owns selected artifact content loading.
- `useExecutionContextPreview(repositoryId, milestonePath)` owns explicit execution-context preview loading.
- `useExecutionSession(repositoryId, sessionId)` owns execution status loading and refresh.
- `useExecutionEvents(sessionId)` owns SSE subscription, cleanup, event ordering, and duplicate sequence replacement.
- `useGitStatus(repositoryId)` owns git status loading, explicit refresh, loading state, error state, and projection clearing.
- `useContinuityDiagnostics(repositoryId)` owns continuity diagnostics loading, explicit refresh, loading state, error state, projection clearing, and report-result projection injection through `setData`.

## Remaining Projection Loading Paths In `App.tsx`

- Operational-context proposal loading remains in `App.tsx`.
- Generated handoff content loading remains in `App.tsx`.
- Direct workspace refresh calls remain in workflow action handlers where backend mutation results need immediate reconciliation.
- Commit preparation remains in `App.tsx` because it is workflow-coupled and not part of the read-only git status projection.

## Characterization Added After Inventory

- Milestone selection now has app-level characterization: selecting a different milestone does not call `preview_execution_context`, and the backend preview command is called only by the explicit `Build Execution Context` button with the selected repository and milestone path.
- This protects the boundary that milestone selection is client navigation state while context preview construction remains a backend-owned projection build.
