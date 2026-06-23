# Milestone 4: Execution Workflow Integration

Objective: make workflow execution-aware while keeping Execution authoritative.

Deliver:

- [ ] `WorkflowExecutionProjection` with execution id, repository id, status, started timestamp, completed timestamp, failed timestamp, handoff presence, changes presence, and failure reason.
- [ ] `WorkflowExecutionStatus` with not started, running, completed, failed, cancelled, and awaiting acceptance.
- [ ] `IWorkflowExecutionService`.
- [ ] completion rules for running, completed, awaiting acceptance, failed, and cancelled.
- [ ] `WorkflowExecutionFailure`.
- [ ] `WorkflowExecutionDiagnostics`.
- [ ] timeline events: execution started, completed, failed, cancelled, accepted, and rejected.
- [ ] workflow projection fields for current execution, execution status, execution eligibility, execution failure, and execution diagnostics.
- [ ] recovery integration for execution state.

Rules:

- [ ] Workflow never launches execution.
- [ ] Workflow never cancels execution.
- [ ] Workflow never modifies execution.
- [ ] Workflow never talks to provider APIs.
- [ ] Workflow consumes execution sessions, events, history, and repository execution state.

Tests:

- [ ] running execution projects correctly.
- [ ] completed execution projects correctly.
- [ ] failed and cancelled execution block workflow.
- [ ] awaiting acceptance opens execution acceptance gate.
- [ ] execution recovery rebuilds workflow view after restart.
- [ ] workflow execution services never call execution mutators.

Exit criteria:

- [ ] execution projection exists.
- [ ] execution completion and failure evaluation works.
- [ ] execution timeline integration works.
- [ ] execution recovery integration works.
- [ ] execution diagnostics exist.
