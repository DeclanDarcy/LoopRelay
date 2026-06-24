# Decisions

## Newly Authorized

- The Milestone 1 consumer-migration slice is accepted as the milestone-defining authority transfer from client-side workflow derivation to backend workflow projection.
- `Workflow projection is the sole UI workflow source` is now considered achieved for the current UI workflow rails.
- Removing `getExecutionWorkflowSteps`, its step types, exports, tests, and consumers is the correct retirement of the competing client-side workflow model.
- `WorkflowRail` may render workflow-owned projection facts directly, including stage, progress reasoning, blocking gate, required action, satisfying command, and transition.
- The source-level `workflowAuthority.test.ts` guard should remain to prevent reintroducing workflow progression derived from `RepositoryExecutionState`.
- Remaining Milestone 1 work should focus on surfacing existing workflow authority, not changing authority boundaries.
- The next implementation ordering should be recovery first, health second, certification third, gates/history fourth, and continuation last.
- Workflow panels should prefer direct use of `useWorkflowProjection`, `useWorkflowRecovery`, `useWorkflowHealth`, and `useWorkflowCertification`; any adapter must remain purely presentational.
- Health UI must render decomposed dimensions, findings, evidence, and diagnostics rather than collapsing health to a single summary label.
- Certification remains observational and must not introduce repair or mutation logic.
