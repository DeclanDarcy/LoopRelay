# Handoff

## New State

- Continued Milestone 10 recovery certification.
- Added `recovery-domain-evidence-wins` certification finding in `WorkflowCertificationService`.
- Certification now compares the current domain-derived projection timeline against the latest persisted workflow timeline without invoking recovery mutation.
- Missing timeline evidence is certified as rebuildable derived evidence.
- Stale persisted timeline evidence is certified as detected, with domain projection winning over persisted workflow evidence.
- Updated `m10-certification.md` to mark recovery certification started, with the remaining corruption/idempotency recovery matrix still open.

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter "FullyQualifiedName~WorkflowProjectionServiceTests"` passed: 103 tests.
- `dotnet build CommandCenter.slnx` passed with 0 warnings and 0 errors.

## Relevant Decisions

- Recovery certification remains report-only. It must not call `RecoverCurrentWorkflowAsync` from current certification because that would rewrite timeline evidence as a side effect.
- Domain-derived projection is the recovery authority. Persisted workflow timelines are evidence only; stale or missing timelines must never override domain state.
- This slice intentionally did not certify corrupted continuation/preparation history or restart duplicate progression yet.

## Next Slice

- Continue Milestone 10 recovery certification with corruption and idempotency scenarios:
  - corrupted timeline evidence is detected while preserving domain state.
  - corrupted continuation history does not duplicate continuation events after recovery/restart.
  - corrupted preparation history does not duplicate preparation events or review artifacts.
  - completed workflow restart remains `Completed` with a `WorkSelection` gate and does not progress again.
