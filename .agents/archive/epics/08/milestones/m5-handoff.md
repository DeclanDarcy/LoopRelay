# Milestone 5: Handoff Workflow Integration

Objective: make workflow understand execution outcomes and handoff authority.

Deliver:

- [x] `WorkflowHandoffProjection` with execution id, repository id, handoff id/path, status, created timestamp, accepted timestamp, rejected timestamp, changes presence, and summary.
- [x] `WorkflowHandoffStatus` with missing, pending, accepted, rejected, and invalid.
- [x] `IWorkflowHandoffService`.
- [x] handoff completion rules for pending, accepted, rejected, missing, and invalid.
- [x] `WorkflowHandoffValidation`.
- [x] `WorkflowHandoffDiagnostics`.
- [x] timeline events: handoff created, validated, accepted, rejected, and invalid.
- [x] projection integration for current handoff, handoff status, validation status, blocking conditions, and acceptance/rejection evidence.
- [x] recovery integration for handoff state.

Rules:

- [x] Handoff creation, validation, acceptance, and rejection remain Execution authority.
- [x] Workflow never accepts or rejects handoffs.
- [x] Existing handoff acceptance and rejection commands remain canonical.

Tests:

- [x] pending, accepted, rejected, missing, and invalid handoffs project correctly.
- [x] pending handoff opens execution acceptance gate.
- [x] accepted handoff makes decision stage eligible.
- [x] rejected handoff blocks workflow until a new execution cycle.
- [x] restart restores handoff state.
- [x] workflow never mutates handoffs.

Exit criteria:

- [x] handoff projection exists.
- [x] handoff service exists.
- [x] validation exists.
- [x] diagnostics exist.
- [x] timeline integration exists.
- [x] recovery integration exists.
