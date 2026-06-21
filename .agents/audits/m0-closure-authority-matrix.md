# M0 Closure Authority Matrix

## Scope

This matrix closes the Milestone 0 authority audit requested after navigation, transport, type, projection-hook, and artifact-draft boundaries were established.

It evaluates whether remaining `App.tsx` responsibilities are authority leaks or intentionally retained orchestration/draft/workflow responsibilities for later migration.

## Authority Matrix

| Responsibility | Current Owner | Authority Classification | Certification State | M0 Disposition |
| --- | --- | --- | --- | --- |
| Repository dashboard projection loading | `useRepositories()` | Backend projection loading | Hook characterization covers initial load and refresh. | Complete |
| Workspace projection loading and manual refresh | `useRepositoryWorkspace(repositoryId)` | Backend projection loading plus explicit refresh | Hook characterization covers `get_repository_workspace` vs `refresh_repository_workspace` separation. | Complete |
| Selected artifact content loading | `useArtifactContent(repositoryId, relativePath)` | Backend projection loading | Hook characterization covers load and clearing; App smoke test covers draft edits not reloading projections. | Complete |
| Execution context preview | `useExecutionContextPreview(repositoryId, milestonePath)` | Explicit backend projection build | Hook characterization covers no automatic build and stale preview preservation until rebuild/clear. | Complete |
| Execution session status | `useExecutionSession(repositoryId, sessionId)` | Backend projection loading | Hook characterization covers load, refresh, reattach, stale-load isolation, and silent refresh failures. | Complete |
| Execution event stream | `useExecutionEvents(sessionId)` | Backend event projection subscription | Hook characterization covers sequence merge, duplicate replacement, session-change cleanup, and unmount cleanup. | Complete |
| Git status | `useGitStatus(repositoryId)` | Read-only backend projection loading | Hook characterization covers load, refresh, and clearing when repository selection is removed. | Complete |
| Continuity diagnostics | `useContinuityDiagnostics(repositoryId)` | Read-only backend projection loading | Hook characterization covers load, refresh, and clearing when repository selection is removed. | Complete |
| Repository/artifact/milestone navigation | `useShellState()` | Client navigation state | Shell-state characterization covers repository reconciliation, path memory, tab state, command-palette state, and no projection storage. | Complete |
| Artifact editor draft | `App.tsx` | Local draft state | App smoke characterization covers draft edits not reloading projections and not invoking `save_artifact_content`, `rotate_current_handoff`, or `rotate_current_decisions`. | Complete for M0 |
| Artifact save and rotation | `App.tsx` | Explicit backend-owned artifact mutation | App smoke characterization covers `save_artifact_content`, `rotate_current_handoff`, and `rotate_current_decisions` only running from explicit artifact actions. | Complete for M0 |
| Execution launch | `App.tsx` | Explicit backend-owned workflow mutation | App smoke characterization covers `start_execution` only running after an explicit context build and explicit `Start Execution` action. | Complete for M0 |
| Generated handoff decisions | `App.tsx` | Explicit backend-owned workflow mutation | App smoke characterization covers repository navigation/review display not invoking decisions; `accept_execution_handoff` and `reject_execution_handoff` require explicit buttons. | Complete for M0 |
| Commit preparation | `App.tsx` | Workflow-review setup, not plain projection | App smoke characterization covers `prepare_commit` only running from explicit Git Workflow refresh while awaiting commit. It initializes commit message draft and path selection and feeds commit readiness. | Complete for M0; feature boundary deferred |
| Commit message and path selection | `App.tsx` | Local draft/review state | App smoke characterization covers edits and path selection not invoking `prepare_commit` or `commit_execution`; `commit_execution` requires explicit `Commit Selected`. | Complete for M0; feature boundary deferred |
| Push execution | `App.tsx` | Explicit backend-owned workflow mutation | App smoke characterization covers `push_execution` only running from explicit `Push Commit`. | Complete for M0 |
| Operational-context proposal loading | `App.tsx` | Workflow-review setup, not plain projection | App smoke characterization covers proposal loading only through `Load Latest`. It initializes proposal edit draft, review note draft, and comparison content. | Complete for M0; feature boundary deferred |
| Operational-context proposal edit draft | `App.tsx` | Local draft state | App smoke characterization covers proposal draft and review-note edits not invoking edit, accept, reject, or promote commands. | Complete for M0; feature boundary deferred |
| Operational-context proposal actions | `App.tsx` | Explicit backend-owned workflow mutations | App smoke characterization covers generate, edit save, accept, reject, and promote requiring explicit action buttons. | Complete for M0 |
| Continuity report generation | `App.tsx` | Explicit backend-owned workflow mutation | App smoke characterization covers diagnostics as read-only retrieval and `generate_continuity_report` only from explicit `Generate Report`. | Complete for M0 |
| Generated handoff review content | `App.tsx` | Workflow review surface | App smoke characterization covers handoff decisions as explicit actions; content loading remains a review-surface responsibility loaded from a backend-projected handoff path during `AwaitingAcceptance`. | Deferred feature boundary |
| Workflow gating | `App.tsx` | UI enablement derived from backend projections plus local pending flags | Audited only. Backend commands remain authoritative for state transitions and validation. | Deferred |
| Workflow actions | `App.tsx` | User action orchestration calling backend commands | Audited only. Actions reconcile by reloading backend projections after mutation. | Deferred |
| Post-mutation projection reconciliation | `App.tsx` plus hooks/API | Backend projection reconciliation after backend-owned mutation | Audited. Direct refresh calls are mutation follow-up, not duplicate projection ownership. | Accept for M0 |

## Workflow-Mutating Command Inventory

All frontend-accessible workflow-mutating backend commands are now covered by app-level characterization:

| Command | Certified Explicit Action |
| --- | --- |
| `save_artifact_content` | Enabled `Save` button after artifact draft edits. |
| `rotate_current_handoff` | Confirmed `Rotate` action while current handoff is selected and clean. |
| `rotate_current_decisions` | Confirmed `Rotate` action while current decisions is selected and clean. |
| `start_execution` | `Start Execution` after an explicit execution-context build. |
| `accept_execution_handoff` | `Accept Handoff` in generated handoff review. |
| `reject_execution_handoff` | Confirmed `Reject Handoff` in generated handoff review. |
| `prepare_commit` | Git Workflow `Refresh` while repository state is `AwaitingCommit`. |
| `commit_execution` | `Commit Selected` with non-empty selected path set and prepared snapshot. |
| `push_execution` | `Push Commit` while repository state is `AwaitingPush`. |
| `generate_operational_context_proposal` | `Generate Proposal`. |
| `get_operational_context_proposal` | `Load Latest`; treated as workflow-review setup because it initializes review drafts and comparison state. |
| `edit_operational_context_proposal` | `Save Edits`. |
| `accept_operational_context_proposal` | `Accept`. |
| `reject_operational_context_proposal` | `Reject`. |
| `promote_operational_context_proposal` | `Promote` after accepted proposal is loaded. |
| `generate_continuity_report` | `Generate Report`. |

## Findings

- No certified read-only projection currently has two competing frontend loading authorities.
- Remaining direct `App.tsx` load paths are coupled to workflow review setup, draft initialization, comparison content, explicit workflow actions, or post-mutation reconciliation.
- `useCommitPreparation(sessionId)` should not be introduced as a read-only git projection hook. It needs a later workflow-review boundary that leaves commit message draft, path selection, readiness, commit mutation, and reconciliation outside the hook.
- `useOperationalContextProposal(repositoryId, proposalId)` should not be introduced until proposal projection loading can be separated from proposal edit draft, review note draft, comparison content, review actions, and promotion actions.
- Manual refresh remains preserved: the workspace hook separates initial load from explicit refresh, and workflow handlers use refresh/reload only after backend mutations.
- Navigation, draft editing, projection loading, and review-surface display are certified not to invoke workflow mutations unless the user takes an explicit workflow action.

## Closure Assessment

Workstream 0.6 authority characterization is complete enough to proceed with M0.5 decomposition work.

The remaining open M0 items are not evidence of projection authority leaks. They are either:

- deferred workflow-coupled boundaries that should migrate with their feature workspace, or
- decomposition and final certification breadth still needed before full Milestone 0 closure.

Do not close the full milestone until Workstream 0.5 decomposition and the remaining high-value characterization scenarios have either landed or been explicitly moved to later milestone checklists.
