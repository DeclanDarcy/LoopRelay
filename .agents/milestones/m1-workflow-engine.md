## Milestone 1: Workflow Engine Integration

### Objective

Replace client-side workflow derivation with the authoritative workflow projection and make workflow state, gates, continuation, recovery, health, and certification visible. The workflow projection becomes the operational backbone and canonical operational timeline that later workspaces consume when they need current operational status, required action, gate, recovery, health, or certification context.

### Backend and Shell

- [ ] Keep `CommandCenter.Workflow` as the workflow authority for projection and workflow-derived diagnostics.
- [ ] Reuse existing routes in `WorkflowEndpoints.cs`:
   - [ ] `GET /api/repositories/{repositoryId}/workflow`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/diagnostics`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/timeline`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/history`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/transitions`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/gates`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/gates/history`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/recovery`
   - [ ] `POST /api/repositories/{repositoryId}/workflow/recover`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/execution`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/handoff`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/decisions`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/operational-context`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/git`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/continuation/evaluation`
   - [ ] `POST /api/repositories/{repositoryId}/workflow/continuation/run`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/continuation/history`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/preparation/evaluation`
   - [ ] `POST /api/repositories/{repositoryId}/workflow/preparation/run`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/preparation/history`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/health`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/reports/repository`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/reports/progression`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/reports/human-governance`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/reports/readiness`
   - [ ] `GET /api/repositories/{repositoryId}/workflow/certification`
   - [ ] `POST /api/repositories/{repositoryId}/workflow/certification`
- [ ] Add Tauri commands in `src/CommandCenter.Shell/src/main.rs` for every Core MVP workflow read/action route. Prefer small `backend_get_value` and `backend_post_value` helpers to avoid duplicating request/error handling.
- [ ] Preserve backend error semantics. Return backend conflict, not found, and bad request messages unchanged through the shell.

### UI

- [ ] Add `src/CommandCenter.UI/src/types/workflow.ts` with TypeScript models matching `CommandCenter.Workflow.Models`.
- [ ] Export workflow types from `src/CommandCenter.UI/src/types/index.ts`.
- [ ] Add `src/CommandCenter.UI/src/api/workflow.ts` and export it from `src/CommandCenter.UI/src/api/index.ts`.
- [ ] Add workflow hooks such as `useWorkflowProjection`, `useWorkflowHistory`, `useWorkflowGates`, `useWorkflowContinuation`, `useWorkflowRecovery`, `useWorkflowHealth`, and `useWorkflowCertification`.
- [ ] Replace `getExecutionWorkflowSteps` usage with workflow projection data.
- [ ] Retire `src/CommandCenter.UI/src/lib/executionWorkflow.ts` after all consumers use authoritative workflow data.
- [ ] Replace or adapt `WorkspaceRail` and `ExecutionWorkflowRail` to render:
   - [ ] current stage
   - [ ] progress state
   - [ ] stage reasoning
   - [ ] blocking gate
   - [ ] required human action
   - [ ] current transition
   - [ ] satisfying commands
   - [ ] continuation state
   - [ ] recovery state
   - [ ] health dimensions
   - [ ] certification findings
- [ ] Add workflow panels under `src/CommandCenter.UI/src/features/workflow/` or move existing rail components there:
   - [ ] `WorkflowOverviewPanel`
   - [ ] `WorkflowHistoryPanel`
   - [ ] `WorkflowGatePanel`
   - [ ] `WorkflowContinuationPanel`
   - [ ] `WorkflowRecoveryPanel`
   - [ ] `WorkflowHealthPanel`
   - [ ] `WorkflowCertificationPanel`
- [ ] Integrate workflow into repository workspace, execution workspace, and dashboard summary without duplicating the domain model.
- [ ] Establish a shared workflow consumption pattern for later milestones:
   - [ ] repository workspace shows workflow as primary operational status
   - [ ] decision-session workspace links governance state back to workflow gates and required actions
   - [ ] execution workspace shows execution as a workflow stage, not a separate workflow model
   - [ ] operational-context workspace shows review and promotion state through workflow gates where applicable

### Tests

- [ ] Add backend endpoint tests for any route not already covered.
- [ ] Add shell command tests where feasible.
- [ ] Add UI characterization tests proving workflow panels render projection stage, gate reason, satisfying command, recovery diagnostics, health dimensions, and certification findings.
- [ ] Add a regression test that no UI workflow state is derived from `RepositoryExecutionState`.

### Exit Criteria

- [ ] Workflow projection is the sole UI workflow source.
- [ ] Users can see current stage, progress, reasoning, gates, required human actions, continuation, recovery, health, and certification.
- [ ] Workflow history is reconstructable from projected evidence.
- [ ] Workflow gates explain why progress is blocked, who owns the unblock action, and which command satisfies it.
- [ ] No other workspace creates a parallel lifecycle timeline for operational product state.
- [ ] Parallel client-side workflow derivation is removed.
- [ ] Later workspaces have a documented consumption pattern for workflow projection instead of bypassing the operational backbone.
