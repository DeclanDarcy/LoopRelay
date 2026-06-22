# Handoff

## New State From This Slice

- Continued Milestone 7 governance hardening.
- Added `ProjectionIntegrity` governance category across backend and UI types.
- Added projection failure detection in `DecisionGovernanceService` for file-backed lifecycle artifacts:
  - missing or stale `decision.md` beside structured decision records
  - missing or stale `candidate.md` beside structured candidates
  - missing or stale `proposal.md` beside structured proposals
  - missing or stale `.agents/decisions/decisions.md` index
- Projection findings are blocking and set `BlocksExecutionProjection = true` because they indicate repository lifecycle projections are not coherent with structured authority.
- In-memory governance fixtures remain unaffected unless structured decision artifact directories exist on disk.
- Added focused governance test coverage for missing decision markdown projection.
- Updated `.agents/milestones/m7-decision-governance.md` to mark projection failures complete.
- Rotated prior handoff to `.agents/handoffs/handoff.0036.md`.

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter DecisionGovernanceServiceTests` passes: 14 tests.
- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` passes: 322 tests.

## Next Slice

- Continue M7C/M7D with unresolved stale proposal detection.
- After that, move into repeated-signal coverage analysis: repeated ambiguity, blockers, forks, unresolved questions, stale candidates, and repeated governance findings.
