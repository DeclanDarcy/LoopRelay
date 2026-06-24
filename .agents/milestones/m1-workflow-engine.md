## Milestone 1: Workflow Engine Integration

### Objective

Replace client-side workflow derivation with the authoritative workflow projection and make workflow state, gates, continuation, recovery, health, and certification visible. The workflow projection becomes the operational backbone and canonical operational timeline that later workspaces consume when they need current operational status, required action, gate, recovery, health, or certification context.

### Backend and Shell

- [x] Keep `CommandCenter.Workflow` as the workflow authority for projection and workflow-derived diagnostics.
- [x] Reuse existing routes in `WorkflowEndpoints.cs`:
   - [x] `GET /api/repositories/{repositoryId}/workflow`
   - [x] `GET /api/repositories/{repositoryId}/workflow/diagnostics`
   - [x] `GET /api/repositories/{repositoryId}/workflow/timeline`
   - [x] `GET /api/repositories/{repositoryId}/workflow/history`
   - [x] `GET /api/repositories/{repositoryId}/workflow/transitions`
   - [x] `GET /api/repositories/{repositoryId}/workflow/gates`
   - [x] `GET /api/repositories/{repositoryId}/workflow/gates/history`
   - [x] `GET /api/repositories/{repositoryId}/workflow/recovery`
   - [x] `POST /api/repositories/{repositoryId}/workflow/recover`
   - [x] `GET /api/repositories/{repositoryId}/workflow/execution`
   - [x] `GET /api/repositories/{repositoryId}/workflow/handoff`
   - [x] `GET /api/repositories/{repositoryId}/workflow/decisions`
   - [x] `GET /api/repositories/{repositoryId}/workflow/operational-context`
   - [x] `GET /api/repositories/{repositoryId}/workflow/git`
   - [x] `GET /api/repositories/{repositoryId}/workflow/continuation/evaluation`
   - [x] `POST /api/repositories/{repositoryId}/workflow/continuation/run`
   - [x] `GET /api/repositories/{repositoryId}/workflow/continuation/history`
   - [x] `GET /api/repositories/{repositoryId}/workflow/preparation/evaluation`
   - [x] `POST /api/repositories/{repositoryId}/workflow/preparation/run`
   - [x] `GET /api/repositories/{repositoryId}/workflow/preparation/history`
   - [x] `GET /api/repositories/{repositoryId}/workflow/health`
   - [x] `GET /api/repositories/{repositoryId}/workflow/reports/repository`
   - [x] `GET /api/repositories/{repositoryId}/workflow/reports/progression`
   - [x] `GET /api/repositories/{repositoryId}/workflow/reports/human-governance`
   - [x] `GET /api/repositories/{repositoryId}/workflow/reports/readiness`
   - [x] `GET /api/repositories/{repositoryId}/workflow/certification`
   - [x] `POST /api/repositories/{repositoryId}/workflow/certification`
- [x] Add Tauri commands in `src/CommandCenter.Shell/src/main.rs` for every Core MVP workflow read/action route. Prefer small `backend_get_value` and `backend_post_value` helpers to avoid duplicating request/error handling.
- [x] Preserve backend error semantics. Return backend conflict, not found, and bad request messages unchanged through the shell.

### UI

- [x] Add `src/CommandCenter.UI/src/types/workflow.ts` with TypeScript models matching `CommandCenter.Workflow.Models`.
- [x] Export workflow types from `src/CommandCenter.UI/src/types/index.ts`.
- [x] Add `src/CommandCenter.UI/src/api/workflow.ts` and export it from `src/CommandCenter.UI/src/api/index.ts`.
- [x] Add workflow hooks such as `useWorkflowProjection`, `useWorkflowHistory`, `useWorkflowGates`, `useWorkflowContinuation`, `useWorkflowRecovery`, `useWorkflowHealth`, and `useWorkflowCertification`.
- [x] Replace `getExecutionWorkflowSteps` usage with workflow projection data.
- [x] Retire `src/CommandCenter.UI/src/lib/executionWorkflow.ts` after all consumers use authoritative workflow data.
- [ ] Replace or adapt `WorkspaceRail` and `ExecutionWorkflowRail` to render:
   - [x] current stage
   - [x] progress state
   - [x] stage reasoning
   - [x] blocking gate
   - [x] required human action
   - [x] current transition
   - [x] satisfying commands
   - [x] continuation state
   - [x] recovery state
   - [x] health dimensions
   - [x] certification findings
- [ ] Add workflow panels under `src/CommandCenter.UI/src/features/workflow/` or move existing rail components there:
   - [x] `WorkflowOverviewPanel`
   - [x] `WorkflowHistoryPanel`
   - [x] `WorkflowGatePanel`
   - [x] `WorkflowContinuationPanel`
   - [x] `WorkflowRecoveryPanel`
   - [x] `WorkflowHealthPanel`
   - [x] `WorkflowCertificationPanel`
- [x] Integrate workflow into repository workspace, execution workspace, and dashboard summary without duplicating the domain model.
- [ ] Establish a shared workflow consumption pattern for later milestones:
   - [x] repository workspace shows workflow as primary operational status
   - [ ] decision-session workspace links governance state back to workflow gates and required actions
   - [x] execution workspace shows execution as a workflow stage, not a separate workflow model
   - [ ] operational-context workspace shows review and promotion state through workflow gates where applicable
   - [x] consumption-pattern artifact documents ownership boundaries for later workspaces

### Tests

- [ ] Add backend endpoint tests for any route not already covered.
- [ ] Add shell command tests where feasible.
- [x] Add UI characterization tests proving workflow panels render projection stage, gate reason, satisfying command, recovery diagnostics, health dimensions, and certification findings.
- [x] Add a regression test that no UI workflow state is derived from `RepositoryExecutionState`.

### Exit Criteria

- [x] Workflow projection is the sole UI workflow source.
- [x] Users can see current stage, progress, reasoning, gates, required human actions, continuation, recovery, health, and certification.
- [x] Workflow history is reconstructable from projected evidence.
- [x] Workflow gates explain why progress is blocked, who owns the unblock action, and which command satisfies it.
- [ ] No other workspace creates a parallel lifecycle timeline for operational product state.
- [ ] Parallel client-side workflow derivation is removed.
- [x] Later workspaces have a documented consumption pattern for workflow projection instead of bypassing the operational backbone.
