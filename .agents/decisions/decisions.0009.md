# Decisions

## Newly Authorized

- Proceed with Milestone 9, but keep the first slice evaluation-only.
- Start M9 by adding:
  `IWorkflowContinuationService`, `WorkflowContinuationEvaluation`,
  `WorkflowContinuationDiagnostics`, and `WorkflowContinuationFingerprint`.
- Continuation evaluation should answer only whether workflow can mechanically
  advance, from which stage, to which stage, why, which gate stops it, and which
  fingerprint makes the evaluation idempotent.
- Continuation must consume aggregate workflow projection, state machine
  diagnostics, gates, and completion evaluation.
- Continuation must not independently inspect domain services or domain
  artifacts.
- Preserve the layering:
  domain services -> workflow domain projections -> aggregate workflow
  projection -> state machine/gates -> continuation evaluation.
- Git completion must never bypass execution, handoff, decision, or
  operational-context gates.

## Explicitly Deferred

- Do not persist continuation events in the first M9 slice.
- Do not run hosted continuation in the first M9 slice.
- Do not prepare decisions, operational context, or commits in the first M9
  slice.
- Do not invoke domain commands from continuation evaluation.
- Do not continue implementation in this response; stage, commit, push, and
  stop.
