# Milestone 3: Workflow Gate Catalog

Objective: unify human authority checkpoints as workflow gates.

Deliver:

- [ ] `WorkflowGate` with gate id, type, repository id, stage, status, required action, satisfying command, source domain, source artifact, created timestamp, satisfied timestamp, satisfied actor, and reason.
- [ ] `WorkflowGateStatus` with open, satisfied, rejected, bypassed, expired, and unknown.
- [ ] `WorkflowGateEvidence`.
- [ ] `IWorkflowGateCatalogService`.
- [ ] deterministic gate-to-command map:

```text
ExecutionAcceptance -> accept_execution_handoff or reject_execution_handoff
DecisionResolution -> resolve_decision
OperationalContextReview -> accept/edit/reject_context_proposal
OperationalContextPromotion -> promote_operational_context
CommitApproval -> commit_execution
PushApproval -> push_execution
WorkSelection -> explicit human repository and work target selection
```

- [ ] projection integration for open gates, satisfied gates, blocking gate, gate history, current gate reason, and required human action.
- [ ] `WorkflowGateDiagnostics`.
- [ ] gate history markdown projection.

Gate rules:

- [ ] Execution acceptance opens when execution completed and a handoff awaits acceptance.
- [ ] Decision resolution opens when a proposal is ready for resolution.
- [ ] Operational context review opens when an unreviewed context proposal exists.
- [ ] Operational context promotion opens when a proposal was accepted or edited and is not promoted.
- [ ] Commit approval opens when execution is awaiting commit.
- [ ] Push approval opens when execution is awaiting push.
- [ ] Work selection opens when a work cycle is completed and no explicit next work target has been selected.

Tests:

- [ ] each gate opens from the correct domain evidence.
- [ ] each gate satisfies only from domain evidence.
- [ ] every gate maps to an existing command name.
- [ ] gate catalog never executes commands.
- [ ] diagnostics explain why blocked, what action is required, and what evidence would satisfy the gate.

Exit criteria:

- [ ] workflow gate model exists.
- [ ] gate catalog service exists.
- [ ] gate satisfaction evidence exists.
- [ ] gate-to-command map exists.
- [ ] workflow projection includes gates.
- [ ] gate diagnostics exist.
