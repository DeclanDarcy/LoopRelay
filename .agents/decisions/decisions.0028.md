# Decisions

## Newly Authorized

- Proceed with Milestone 10 continuation certification next.
- Continuation certification must prove both endpoint and hosted continuation halt at:
  - `WorkSelection`
  - `ExecutionAcceptance`
  - `DecisionResolution`
  - `OperationalContextReview`
  - `OperationalContextPromotion`
  - `CommitApproval`
  - `PushApproval`
- Certification must fail if any continuation event advances across an open gate.
- Continuation certification must also cover:
  - hosted continuation disabled by default.
  - hosted continuation uses the same service path as endpoint continuation.
  - one repository failure does not block hosted continuation for other repositories.
- No new behavior should be added unless certification exposes a real gap.
