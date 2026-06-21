# M0 App Responsibility Inventory

## Scope

Captured after extracting `useGitStatus(repositoryId)`. This is an inventory only; no refactor is authorized by this artifact.

## Remaining `App.tsx` Responsibilities

### Navigation State

- `selectedRepositoryId`
- `selectedArtifactPath`
- `selectedMilestonePath`
- `selectedArtifactPathsByRepository`

### Projection State

- Hook-owned: repositories, workspace, artifact content, execution context preview, execution session status, execution events, git status.
- Still `App.tsx`-owned: operational-context proposal, continuity diagnostics, generated handoff content, operational-context current content used while reviewing proposals.
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

## Remaining Projection Loading Paths In `App.tsx`

- Operational-context proposal loading remains in `App.tsx`.
- Continuity diagnostics loading remains in `App.tsx`.
- Generated handoff content loading remains in `App.tsx`.
- Direct workspace refresh calls remain in workflow action handlers where backend mutation results need immediate reconciliation.
- Commit preparation remains in `App.tsx` because it is workflow-coupled and not part of the read-only git status projection.
