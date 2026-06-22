# M0 Projection Authority Certification

## Scope

Created during Milestone 0 Workstream 0.3 after extracting read-oriented projection hooks through `useContinuityDiagnostics(repositoryId)`.

This audit certifies frontend projection authority only. It does not authorize moving workflow actions, workflow gating, draft state, or post-mutation reconciliation out of `App.tsx`.

## Certification Table

| Projection | Authority Hook | Consumer | Duplicate Load Paths | Certified |
| --- | --- | --- | --- | --- |
| Repository dashboard list | `useRepositories()` | `App.tsx` | None for dashboard loading. Workflow handlers may call `refresh()` after backend mutations to reconcile state. | Yes |
| Repository workspace | `useRepositoryWorkspace(repositoryId)` | `App.tsx` | No duplicate initial load path. `App.tsx` still calls workspace refresh APIs in workflow handlers that need immediate backend reconciliation after save, rotate, proposal, commit, push, handoff, or report actions. | Yes, with reconciliation caveat |
| Selected artifact content | `useArtifactContent(repositoryId, relativePath)` | `App.tsx` | `App.tsx` directly loads artifact content for generated handoff review and operational-context current-content comparison. Those are distinct workflow/review surfaces, not the selected artifact editor projection. | Yes, selected artifact only |
| Execution context preview | `useExecutionContextPreview(repositoryId, milestonePath)` | `App.tsx` | None for preview construction. The hook is explicit-load only and preserves the existing "build when requested" behavior. | Yes |
| Execution session status | `useExecutionSession(repositoryId, sessionId)` | `App.tsx` | None for session status. `App.tsx` performs projection reconciliation after streamed updates and terminal-state transitions. | Yes |
| Execution event stream | `useExecutionEvents(sessionId)` | `App.tsx` | None. EventSource construction and cleanup are centralized in the execution event API and hook. | Yes |
| Git status | `useGitStatus(repositoryId)` | `App.tsx` | None. Commit preparation remains separate because it is workflow review state, not read-only git status. | Yes |
| Continuity diagnostics | `useContinuityDiagnostics(repositoryId)` | `App.tsx` | None for diagnostics loading. Continuity report generation remains a workflow action and injects the backend-returned diagnostics projection through the hook setter. | Yes, with report-generation caveat |

## Non-Certified Candidates

### Commit Preparation

`useCommitPreparation(sessionId)` remains deferred.

Reason: commit preparation is currently entered from the `AwaitingCommit` workflow state, initializes commit path selection and commit message draft state, and feeds commit readiness. Extracting it as a simple projection hook is possible later only if the hook owns loading/error/projection data and leaves scope selection, message draft, commit readiness, commit execution, and post-commit refresh in `App.tsx` or a workflow-specific boundary.

### Operational-Context Proposal

`useOperationalContextProposal(repositoryId, proposalId)` remains deferred.

Reason: proposal loading currently initializes the proposal edit draft, review note draft, and current operational-context comparison content. The direct load path is therefore coupled to review workflow state. A future extraction is acceptable only if it is constrained to proposal projection loading/refresh/error/data and continues to leave generation, edit, accept, reject, promote, review readiness, promotion readiness, draft state, and current-content comparison outside the hook.

## Remaining Direct Load Paths In `App.tsx`

- `prepareCommit(sessionId)`: workflow review setup for commit scope.
- `getOperationalContextProposal(repositoryId, proposalId)`: proposal review workflow setup.
- `loadArtifactContent(repositoryId, currentOperationalContextPath)`: current-content comparison during proposal review and after promotion.
- `loadArtifactContent(repositoryId, generatedHandoffPath)`: generated handoff review content.
- `refreshRepositoryWorkspace(repositoryId)`: post-mutation reconciliation where the backend returns or changes repository-owned artifacts and workspace summary state.

These paths are intentionally not certified as duplicate projection authorities in this slice because they support workflow actions, draft initialization, or review surfaces rather than replacing an extracted read projection hook.

## Current M0 Assessment

The extracted projection hooks now have one frontend authority per certified projection. Further Workstream 0.3 extraction should be evidence-driven:

- Extract commit preparation only as a workflow-review loading boundary, not as general git projection authority.
- Extract operational-context proposal only if the proposal projection can be separated from draft/review/comparison state first.
- Otherwise proceed to Workstream 0.4 state-boundary separation, where navigation, projection, and draft ownership can be made explicit before additional decomposition.

## Closure Audit Update

The later closure matrix in `.agents/audits/m0-closure-authority-matrix.md` confirms that the remaining unextracted candidates are workflow-review boundaries rather than duplicate read-only projection authorities.

M0 should proceed to decomposition work from the certified authority map instead of extracting `useCommitPreparation(sessionId)` or `useOperationalContextProposal(repositoryId, proposalId)` as simple projection hooks.
