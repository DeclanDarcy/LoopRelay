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
- Commit workflow authority now has app-level characterization: selecting an awaiting-commit repository does not call `prepare_commit`, and the backend preparation command is called only through the Git Workflow refresh action for that session.
- Commit message edits, commit scope selection, `Select All`, and `Select None` remain local draft state and do not call `prepare_commit` or `commit_execution`; `Commit Selected` calls `commit_execution` with the explicit selected path list, trimmed message, session id, and preparation snapshot id.
- Push workflow authority now has app-level characterization: selecting or refreshing an awaiting-push repository does not call `push_execution`; only `Push Commit` invokes the backend push command for the session.
- `App.tsx` no longer auto-loads commit preparation from an effect when a repository enters `AwaitingCommit`; the cleanup effect is limited to non-workflow selection and stale-session draft cleanup so workflow preparation remains an explicit backend action.
- Operational-context proposal authority now has app-level characterization: repository/artifact navigation does not call proposal generation or proposal loading, and proposal draft/review-note edits do not call edit, accept, reject, or promote commands.
- Proposal workflow actions are characterized as explicit backend actions: `Load Latest` calls `get_operational_context_proposal`; `Generate Proposal` calls `generate_operational_context_proposal`; `Save Edits` calls `edit_operational_context_proposal`; `Accept` and `Reject` call their review commands with the current review note; `Promote` calls `promote_operational_context_proposal` only after an accepted proposal is present.
- Continuity diagnostics/report authority now has app-level characterization: initial diagnostics load, repository selection, and `Refresh Diagnostics` call only `get_continuity_diagnostics`, while repository/artifact navigation does not call `generate_continuity_report`.
- Continuity report generation is characterized as an explicit backend workflow action: `Generate Report` calls `generate_continuity_report` for the selected repository and injects the returned diagnostics projection through the continuity diagnostics hook state.
- Workflow-mutating backend command inventory is now complete in `.agents/audits/m0-closure-authority-matrix.md`.
- Artifact mutation authority now has app-level characterization: artifact draft edits do not call `save_artifact_content`, `rotate_current_handoff`, or `rotate_current_decisions`; `Save` calls `save_artifact_content` with the selected repository, selected artifact path, and current draft content; confirmed `Rotate` calls only the selected artifact-family rotation command.
- Execution launch and generated handoff decision authority now have app-level characterization: repository display and context build do not call `start_execution`; only `Start Execution` calls `start_execution` with the selected repository and milestone path; generated handoff display does not call accept/reject commands; only `Accept Handoff` and confirmed `Reject Handoff` invoke their backend decision commands.
