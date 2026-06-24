# Decisions

## Newly Authorized

- Treat the M3 gate catalog architecture as correct: workflow gates must stay read-only and derived from domain evidence through projection/state-machine/gate evaluation.
- Treat gate history as a projection, not workflow truth. Future persisted gate artifacts, if added, must remain disposable/reconstructable audit evidence.
- Treat `resolve_decision_proposal` as the correct concrete command mapping for the decision-resolution gate because it is the existing authority command exposed by the app.
- Treat the `resolve_decision_proposal` mapping as a roadmap refinement, not a deviation from the plan.
- Preserve the distinction that timeline records gate satisfaction but does not determine gate satisfaction.
- During M4, workflow may consume execution sessions, execution history, execution events, and execution state.
- During M4, workflow must not own or invoke execution commands, provider launch/control, cancellation, or retry.
- `WorkflowExecutionProjection` should explain execution status, timestamps, failure reason, eligibility, and diagnostics without modeling execution lifecycle authority independently.
- Continue enforcing that workflow stores explanations while domains store history, to avoid creating a second event-sourcing system as more domain events are integrated.

## Explicitly Deferred

- Do not let M4 make workflow the place that determines whether execution should happen.
- Do not let execution projection become independent execution state.
- Do not expand workflow timelines into duplicate domain history.
