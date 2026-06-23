# Decisions

## Newly Authorized

- Proceed to Milestone 3 gate catalog after the completed Milestone 2 persistence/recovery slice.
- Treat best-effort startup recovery as the correct architecture: workflow recovery failures must not disable unrelated repository operations.
- Keep workflow artifacts as audit evidence, not as a workflow database.
- Before or during M3, verify workflow fingerprints are derived from normalized evidence with canonical ordering and deterministic serialization, not raw JSON blobs, dictionary iteration order, or incidental filesystem ordering.
- Strengthen recovery coverage with a path resembling:
  `Persist timeline -> Corrupt timeline -> Restart/recover -> Projection rebuilt -> Fingerprint divergence detected -> Diagnostics recorded -> Workflow continues`.
- Keep `IWorkflowRepository` persistence-only:
  save/load/list evidence and reports, never set current stage, set gate, or mutate workflow state.
- Implement M3 gate history as derived/recoverable evidence:
  `Domain Evidence -> Gate Evaluation -> Gate Projection -> Gate History Evidence`.
- Do not let persisted gates become mutable workflow truth. If a gate artifact disappears, domain evidence must reevaluate to the same gate.

## Explicitly Deferred

- Do not introduce continuation, preparation, or workflow-owned lifecycle mutation during M3.
- Do not add `SetCurrentStage`, `SetGate`, or equivalent mutable state commands to workflow persistence.
