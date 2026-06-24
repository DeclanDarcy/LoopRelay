# Decisions

## Newly Authorized

- M4 execution workflow integration is aligned with the roadmap boundary:
  workflow observes Execution and does not operate Execution.
- Preserve `WorkflowExecutionService` as a projection boundary, not a command boundary.
- Keep the workflow execution endpoint read-only.
- Treat failed, cancelled, and rejected execution timeline events as explanatory
  workflow blockage evidence, not workflow-owned recovery authority.
- Keep `HasChanges` conservative; do not infer change presence from weak execution
  summary data.
- M5 should proceed as the next implementation slice.
- M5 may introduce `WorkflowHandoffProjection`, `WorkflowHandoffValidation`, and
  `WorkflowHandoffDiagnostics`.
- M5 may model handoff pending, accepted, rejected, and invalid statuses.
- M5 must avoid duplicating Execution handoff authority.
- M5 must not add workflow accept, reject, repair, or canonical-selection commands
  for handoffs.
- Workflow must not invent a "latest handoff wins" rule unless Execution already
  establishes that rule.
- When multiple handoffs exist, workflow should explain which handoff Execution
  treats as authoritative, which older handoffs were ignored, and why the selected
  handoff is valid.
- Continue preserving the central invariant: domains own lifecycle truth, and
  workflow derives coordination truth.

## Explicitly Deferred

- Do not start M5 implementation before the current M4 commit is staged, committed,
  and pushed.
