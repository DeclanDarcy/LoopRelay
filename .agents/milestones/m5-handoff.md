# Milestone 5: Handoff Workflow Integration

Objective: make workflow understand execution outcomes and handoff authority.

Deliver:

- [ ] `WorkflowHandoffProjection` with execution id, repository id, handoff id/path, status, created timestamp, accepted timestamp, rejected timestamp, changes presence, and summary.
- [ ] `WorkflowHandoffStatus` with missing, pending, accepted, rejected, and invalid.
- [ ] `IWorkflowHandoffService`.
- [ ] handoff completion rules for pending, accepted, rejected, missing, and invalid.
- [ ] `WorkflowHandoffValidation`.
- [ ] `WorkflowHandoffDiagnostics`.
- [ ] timeline events: handoff created, validated, accepted, rejected, and invalid.
- [ ] projection integration for current handoff, handoff status, validation status, blocking conditions, and acceptance/rejection evidence.
- [ ] recovery integration for handoff state.

Rules:

- [ ] Handoff creation, validation, acceptance, and rejection remain Execution authority.
- [ ] Workflow never accepts or rejects handoffs.
- [ ] Existing handoff acceptance and rejection commands remain canonical.

Tests:

- [ ] pending, accepted, rejected, missing, and invalid handoffs project correctly.
- [ ] pending handoff opens execution acceptance gate.
- [ ] accepted handoff makes decision stage eligible.
- [ ] rejected handoff blocks workflow until a new execution cycle.
- [ ] restart restores handoff state.
- [ ] workflow never mutates handoffs.

Exit criteria:

- [ ] handoff projection exists.
- [ ] handoff service exists.
- [ ] validation exists.
- [ ] diagnostics exist.
- [ ] timeline integration exists.
- [ ] recovery integration exists.
