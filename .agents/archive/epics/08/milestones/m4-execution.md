# Milestone 4: Execution Workflow Integration

Objective: make workflow execution-aware while keeping Execution authoritative.

Deliver:

- [x] `WorkflowExecutionProjection` with execution id, repository id, status, started timestamp, completed timestamp, failed timestamp, handoff presence, changes presence, and failure reason.
- [x] `WorkflowExecutionStatus` with not started, running, completed, failed, cancelled, and awaiting acceptance.
- [x] `IWorkflowExecutionService`.
- [x] completion rules for running, completed, awaiting acceptance, failed, and cancelled.
- [x] `WorkflowExecutionFailure`.
- [x] `WorkflowExecutionDiagnostics`.
- [x] timeline events: execution started, completed, failed, cancelled, accepted, and rejected.
- [x] workflow projection fields for current execution, execution status, execution eligibility, execution failure, and execution diagnostics.
- [x] recovery integration for execution state.

Rules:

- [x] Workflow never launches execution.
- [x] Workflow never cancels execution.
- [x] Workflow never modifies execution.
- [x] Workflow never talks to provider APIs.
- [x] Workflow consumes execution sessions, events, history, and repository execution state.

Tests:

- [x] running execution projects correctly.
- [x] completed execution projects correctly.
- [x] failed and cancelled execution block workflow.
- [x] awaiting acceptance opens execution acceptance gate.
- [x] execution recovery rebuilds workflow view after restart.
- [x] workflow execution services never call execution mutators.

Exit criteria:

- [x] execution projection exists.
- [x] execution completion and failure evaluation works.
- [x] execution timeline integration works.
- [x] execution recovery integration works.
- [x] execution diagnostics exist.
