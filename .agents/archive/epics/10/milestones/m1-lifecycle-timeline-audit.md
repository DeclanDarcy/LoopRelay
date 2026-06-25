# Milestone 1 Lifecycle Timeline Audit

## Scope

Audited app-level summaries, repository summaries, workspace rails, execution views, dashboard widgets, and repository projections for any second UI lifecycle model expressing current stage, progression, workflow state, required action, blocked state, or operational status outside the authoritative workflow projection.

## Result

- The canonical lifecycle rail is `WorkflowRail`, populated from `WorkflowInstance`.
- Repository workspace and execution workspace both consume `WorkflowInstance` for stage, progress, blocking gate, required human action, transitions, timeline, recovery, health, and certification.
- The global header now renders selected repository operational status from `workflowProjectionStatus(workflow)` and does not fall back to `RepositoryExecutionState`.
- `SelectedRepositorySummary` renders workflow stage, gate, required action, and timeline count from `WorkflowInstance`.
- `ExecutionTab`, `GitWorkflowPanel`, `WorkspaceInspectorRail`, `ExecutionHistoryPanel`, and execution session panels still render execution/git statuses, but those are scoped as execution or git evidence, not as a competing lifecycle timeline.
- Decision, reasoning, continuity, and operational-context panels render their own domain statuses. Governance workflow linkage and operational-context workflow linkage remain deferred by existing Milestone 2 and Milestone 7 decisions.

## Shell Command Test Feasibility

`src/CommandCenter.Shell` is a single Tauri binary crate with command functions in `main.rs`, no `#[cfg(test)]` modules, no Rust test harness, and no HTTP mocking seam around the blocking backend helpers. Adding command-level tests would require extracting commands into a testable library or introducing Tauri command registration harness scaffolding, which is disproportionate for Milestone 1. Existing coverage instead verifies:

- backend route mapping and backend error semantics in `WorkflowEndpointTests`
- frontend command names, request shapes, and response handling in TypeScript transport characterization tests
- workflow command functions are registered in the Tauri `generate_handler!` list

## Conclusion

No other workspace currently creates a parallel lifecycle timeline for operational product state. Remaining non-workflow statuses are domain evidence and should stay visibly scoped to their domain until their later milestones link them back to workflow gates and required actions.
