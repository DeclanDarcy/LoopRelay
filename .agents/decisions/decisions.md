# Decisions

## Newly Authorized

- Complete the next Milestone 9 slice as preparation evaluation only.
- The first preparation slice may establish:
  - `IWorkflowPreparationService`
  - `WorkflowPreparationEvaluation`
  - `WorkflowPreparationDiagnostics`
  - `WorkflowPreparationEvent`
  - preparation persistence
  - preparation fingerprints
- Preparation must follow the continuation sequencing pattern:
  - evaluate
  - persist
  - recover
  - certify
  - then act
- Preparation should consume workflow projection, gate state, and
  state-machine evaluation rather than independently interpreting domain
  authority.
- Add gate-refusal coverage proving every open authority gate causes
  preparation to be refused.
- Preparation evaluation may answer:
  - whether preparation would be allowed
  - why it would or would not be allowed
  - what gate prevents it
  - what command would eventually be used
  - what fingerprint identifies the request

## Explicitly Deferred

- Do not call Decisions commands yet.
- Do not call Continuity commands yet.
- Do not call Execution commands yet.
- Do not create decision proposals yet.
- Do not create operational-context proposals yet.
- Do not create commit preparations yet.
- Do not wire actual domain command invocation before another review.
