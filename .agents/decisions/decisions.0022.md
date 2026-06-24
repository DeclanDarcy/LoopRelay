# Decisions

## Newly Authorized

- The hosted continuation slice is accepted as the operational completion point for the behavioral portion of Milestone 9.
- Hosted continuation is valid only as scheduled invocation of the existing continuation and preparation services, not as a new authority system.
- Proceed next with `WorkflowInfluenceTrace` before `WorkflowHealthAssessment`.
- `WorkflowInfluenceTrace` should remain evidence lineage, answering:
  - why the workflow is at its current stage.
  - what evidence influenced progression.
  - what evidence influenced preparation.
  - what evidence opened a gate.
  - what evidence blocked a transition.
- Influence trace should explain observed evidence paths rather than create workflow opinions.
- `WorkflowHealthAssessment` should be derived after influence trace exists.
- Workflow health should be decomposed into explainable dimensions:
  - projection health.
  - recovery health.
  - gate health.
  - continuation health.
  - preparation health.
- Do not introduce a single opaque workflow score or readiness percentage.
- After `WorkflowInfluenceTrace` and `WorkflowHealthAssessment` are complete, perform a full Milestone 9 architectural review before final certification work.
