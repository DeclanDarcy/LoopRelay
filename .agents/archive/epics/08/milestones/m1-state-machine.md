# Milestone 1: Workflow State Machine

Objective: make workflow navigable without advancing it.

Deliver:

- [x] `WorkflowTransition`.
- [x] `WorkflowTransitionResult`.
- [x] `IWorkflowStateMachineService`.
- [x] canonical graph:

```text
WorkSelection -> Execution -> Handoff -> Decision -> OperationalContext -> Commit -> Push -> Completed
```

- [x] `WorkflowGateResolution`.
- [x] `WorkflowBlockingCondition`.
- [x] `WorkflowStateMachineDiagnostics`.
- [x] projection integration for valid transitions, blocked transitions, next possible stages, and blocking gates.

Rules:

- [x] The state machine evaluates what can happen next.
- [x] The state machine does not mutate domain state.
- [x] The state machine does not persist authoritative workflow state.
- [x] The state machine does not automate progression.

Tests:

- [x] valid transitions pass.
- [x] invalid transitions fail.
- [x] unresolved decisions block decision-to-context transition.
- [x] pending context review or promotion blocks context-to-commit transition.
- [x] pending commit approval blocks commit-to-push transition.
- [x] pending push approval blocks push-to-completed transition.
- [x] every rejected transition explains itself.
- [x] state-machine services never call mutating domain methods.

Exit criteria:

- [x] workflow graph exists.
- [x] transition validation exists.
- [x] blocking model exists.
- [x] gate requirements are modeled.
- [x] diagnostics are operational.
