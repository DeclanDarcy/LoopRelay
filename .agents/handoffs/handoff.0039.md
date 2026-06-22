# Handoff

## New State From This Slice

- Continued Milestone 7 decision coverage analysis.
- Added stale candidate lifecycle-hygiene detection in `DecisionGovernanceService`.
- Active candidates now create advisory `DecisionCoverage` findings when:
  - multiple active candidates share the same source fingerprint
  - an active candidate reuses a source fingerprint already represented by a terminal candidate
  - an active candidate already has resolved authority through a resolved proposal or decision snapshot
- Stale candidate findings are warnings and do not block execution projection.
- Added focused governance tests for duplicate active source fingerprints, terminal source fingerprint reuse, and active candidates with resolved authority.
- Updated `.agents/milestones/m7-decision-governance.md` to mark stale candidates complete under decision coverage analysis.
- Rotated prior handoff to `.agents/handoffs/handoff.0038.md`.

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter DecisionGovernanceServiceTests` passes: 19 tests.
- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` passes: 327 tests.

## Next Slice

- Continue Milestone 7 with repeated unresolved questions using structured repository signals rather than natural-language similarity.
- After that, add repeated governance finding detection across persisted governance reports.
