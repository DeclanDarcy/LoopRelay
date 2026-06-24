# Milestone 3: Workflow Gate Catalog

Objective: unify human authority checkpoints as workflow gates.

Deliver:

- [x] `WorkflowGate` with gate id, type, repository id, stage, status, required action, satisfying command, source domain, source artifact, created timestamp, satisfied timestamp, satisfied actor, and reason.
- [x] `WorkflowGateStatus` with open, satisfied, rejected, bypassed, expired, and unknown.
- [x] `WorkflowGateEvidence`.
- [x] `IWorkflowGateCatalogService`.
- [x] deterministic gate-to-command map:

```text
ExecutionAcceptance -> accept_execution_handoff or reject_execution_handoff
DecisionResolution -> resolve_decision
OperationalContextReview -> accept/edit/reject_context_proposal
OperationalContextPromotion -> promote_operational_context
CommitApproval -> commit_execution
PushApproval -> push_execution
WorkSelection -> explicit human repository and work target selection
```

- [x] projection integration for open gates, satisfied gates, blocking gate, gate history, current gate reason, and required human action.
- [x] `WorkflowGateDiagnostics`.
- [x] gate history markdown projection.

Gate rules:

- [x] Execution acceptance opens when execution completed and a handoff awaits acceptance.
- [x] Decision resolution opens when a proposal is ready for resolution.
- [x] Operational context review opens when an unreviewed context proposal exists.
- [x] Operational context promotion opens when a proposal was accepted or edited and is not promoted.
- [x] Commit approval opens when execution is awaiting commit.
- [x] Push approval opens when execution is awaiting push.
- [x] Work selection opens when a work cycle is completed and no explicit next work target has been selected.

Tests:

- [x] each gate opens from the correct domain evidence.
- [x] each gate satisfies only from domain evidence.
- [x] every gate maps to an existing command name.
- [x] gate catalog never executes commands.
- [x] diagnostics explain why blocked, what action is required, and what evidence would satisfy the gate.

Exit criteria:

- [x] workflow gate model exists.
- [x] gate catalog service exists.
- [x] gate satisfaction evidence exists.
- [x] gate-to-command map exists.
- [x] workflow projection includes gates.
- [x] gate diagnostics exist.
