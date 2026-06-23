# Milestone 0: Workflow Coordination Foundation

Objective: create the workflow vocabulary and read-only projection substrate.

Deliver:

- [ ] `WorkflowStage`, `WorkflowGateType`, `WorkflowProgressState`, `WorkflowInstance`, and `WorkflowTimelineEntry`.
- [ ] `IWorkflowProjectionService`.
- [ ] explicit deterministic stage derivation from execution, decision, continuity, and git state.
- [ ] `WorkflowProjectionDiagnostics` with projection inputs, chosen stage, chosen gate, reasoning, unknown states, and conflicts.
- [ ] derived workflow timeline entries for execution started/completed, decision resolved, context promoted, commit executed, and push executed where evidence exists.
- [ ] repository integration methods and endpoints for workflow, diagnostics, and timeline.

Rules:

- [ ] No workflow state machine.
- [ ] No persistence beyond optional transient projection output.
- [ ] No recovery.
- [ ] No automation.
- [ ] No cross-domain mutation.

Tests:

- [ ] execution states map to workflow stages and gates.
- [ ] decision states map to workflow stages and gates.
- [ ] continuity states map to workflow stages and gates.
- [ ] git states map to workflow stages and gates.
- [ ] identical inputs produce identical projections.
- [ ] diagnostics explain every projection.
- [ ] workflow projection does not call mutating domain methods.

Exit criteria:

- [ ] workflow contracts exist.
- [ ] workflow projection exists.
- [ ] projection diagnostics exist.
- [ ] derived timeline exists.
- [ ] authority preservation tests pass.
