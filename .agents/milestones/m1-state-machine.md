# Milestone 1: Workflow State Machine

Objective: make workflow navigable without advancing it.

Deliver:

- [ ] `WorkflowTransition`.
- [ ] `WorkflowTransitionResult`.
- [ ] `IWorkflowStateMachineService`.
- [ ] canonical graph:

```text
WorkSelection -> Execution -> Handoff -> Decision -> OperationalContext -> Commit -> Push -> Completed
```

- [ ] `WorkflowGateResolution`.
- [ ] `WorkflowBlockingCondition`.
- [ ] `WorkflowStateMachineDiagnostics`.
- [ ] projection integration for valid transitions, blocked transitions, next possible stages, and blocking gates.

Rules:

- [ ] The state machine evaluates what can happen next.
- [ ] The state machine does not mutate domain state.
- [ ] The state machine does not persist authoritative workflow state.
- [ ] The state machine does not automate progression.

Tests:

- [ ] valid transitions pass.
- [ ] invalid transitions fail.
- [ ] unresolved decisions block decision-to-context transition.
- [ ] pending context review or promotion blocks context-to-commit transition.
- [ ] pending commit approval blocks commit-to-push transition.
- [ ] pending push approval blocks push-to-completed transition.
- [ ] every rejected transition explains itself.
- [ ] state-machine services never call mutating domain methods.

Exit criteria:

- [ ] workflow graph exists.
- [ ] transition validation exists.
- [ ] blocking model exists.
- [ ] gate requirements are modeled.
- [ ] diagnostics are operational.
