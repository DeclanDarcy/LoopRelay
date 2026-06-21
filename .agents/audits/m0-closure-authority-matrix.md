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
| Artifact editor draft | `App.tsx` | Local draft state | App smoke characterization covers draft edits not reloading repository, workspace, refresh, or artifact projections. | Complete for M0 |
| Commit preparation | `App.tsx` | Workflow-review setup, not plain projection | Audited only. It initializes commit message draft and path selection and feeds commit readiness. | Deferred to workflow/component migration |
| Commit message and path selection | `App.tsx` | Local draft/review state | Audited only. It must not move into shell state or read-only git projection hooks. | Deferred |
| Operational-context proposal loading | `App.tsx` | Workflow-review setup, not plain projection | Audited only. It initializes proposal edit draft, review note draft, and comparison content. | Deferred to operational-context migration |
| Operational-context proposal edit draft | `App.tsx` | Local draft state | Audited only. It is coupled to loaded proposal identity and backend edit action. | Deferred |
| Operational-context review note | `App.tsx` | Local review draft state | Audited only. It is consumed by backend accept/reject commands. | Deferred |
| Generated handoff review content | `App.tsx` | Workflow review surface | Audited only. It is loaded from a backend-projected handoff path during `AwaitingAcceptance`. | Deferred |
| Workflow gating | `App.tsx` | UI enablement derived from backend projections plus local pending flags | Audited only. Backend commands remain authoritative for state transitions and validation. | Deferred |
| Workflow actions | `App.tsx` | User action orchestration calling backend commands | Audited only. Actions reconcile by reloading backend projections after mutation. | Deferred |
| Post-mutation projection reconciliation | `App.tsx` plus hooks/API | Backend projection reconciliation after backend-owned mutation | Audited. Direct refresh calls are mutation follow-up, not duplicate projection ownership. | Accept for M0 |

## Findings

- No certified read-only projection currently has two competing frontend loading authorities.
- Remaining direct `App.tsx` load paths are coupled to workflow review setup, draft initialization, comparison content, or post-mutation reconciliation.
- `useCommitPreparation(sessionId)` should not be introduced as a read-only git projection hook. It needs a later workflow-review boundary that leaves commit message draft, path selection, readiness, commit mutation, and reconciliation outside the hook.
- `useOperationalContextProposal(repositoryId, proposalId)` should not be introduced until proposal projection loading can be separated from proposal edit draft, review note draft, comparison content, review actions, and promotion actions.
- Manual refresh remains preserved: the workspace hook separates initial load from explicit refresh, and workflow handlers use refresh/reload only after backend mutations.

## Closure Assessment

Milestone 0 authority foundations are sound enough to proceed to decomposition work.

The remaining open M0 items are not evidence of projection authority leaks. They are either:

- deferred workflow-coupled boundaries that should migrate with their feature workspace, or
- characterization breadth still needed before and during decomposition.

Do not close the full milestone until Workstream 0.5 decomposition and the remaining high-value characterization scenarios have either landed or been explicitly moved to later milestone checklists.
