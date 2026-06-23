# Decisions

## Newly Authorized

- Proceed to Milestone 2 as planned after completing and committing the Milestone 1 state-machine slice.
- Preserve the M2 authority invariant:
  `Domain evidence -> Projection -> State machine -> Persisted workflow evidence`.
- Never treat persisted workflow evidence as domain lifecycle truth.
- Add an M2 guardrail test proving that deleting `.agents/workflow` does not change projected current stage, blocking gate, or valid transitions.
- Start M2 with `WorkflowFingerprint`, `WorkflowTimeline`, `IWorkflowRepository`, deterministic JSON/markdown persistence under `.agents/workflow`, and recovery diagnostics comparing persisted evidence against domain-derived projection.

## Explicitly Deferred

- Do not add continuation, preparation, hosted progression, or gate history mutation during the initial M2 persistence/recovery slice.
