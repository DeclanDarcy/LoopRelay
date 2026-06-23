# Milestone 0: Workflow Coordination Foundation

Objective: create the workflow vocabulary and read-only projection substrate.

Deliver:

- [x] `WorkflowStage`, `WorkflowGateType`, `WorkflowProgressState`, `WorkflowInstance`, and `WorkflowTimelineEntry`.
- [x] `IWorkflowProjectionService`.
- [x] explicit deterministic stage derivation from execution, decision, continuity, and git state.
- [x] `WorkflowProjectionDiagnostics` with projection inputs, chosen stage, chosen gate, reasoning, unknown states, and conflicts.
- [x] derived workflow timeline entries for execution started/completed, decision resolved, context promoted, commit executed, and push executed where evidence exists.
- [x] repository integration methods and endpoints for workflow, diagnostics, and timeline.

Rules:

- [x] No workflow state machine.
- [x] No persistence beyond optional transient projection output.
- [x] No recovery.
- [x] No automation.
- [x] No cross-domain mutation.

Tests:

- [x] execution states map to workflow stages and gates.
- [x] decision states map to workflow stages and gates.
- [x] continuity states map to workflow stages and gates.
- [x] git states map to workflow stages and gates.
- [x] identical inputs produce identical projections.
- [x] diagnostics explain every projection.
- [x] workflow projection does not call mutating domain methods.

Exit criteria:

- [x] workflow contracts exist.
- [x] workflow projection exists.
- [x] projection diagnostics exist.
- [x] derived timeline exists.
- [x] authority preservation tests pass.
