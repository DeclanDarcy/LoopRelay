# Handoff

## New State From This Slice

- Completed M0C for the decision lifecycle foundation.
- Added `IDecisionArtifactProjectionService` and registered `DecisionArtifactProjectionService` in `AddDecisions()`.
- Added deterministic markdown projection paths for `decision.md`, `candidate.md`, `proposal.md`, and the current `.agents/decisions/decisions.md` index.
- `DecisionArtifactProjectionService` now renders human-readable lifecycle markdown from structured decision, candidate, and proposal records.
- The generated `decisions.md` index summarizes decisions, candidates, and proposals while explicitly preserving `structured JSON -> markdown` authority direction.
- Projection ordering is deterministic for decisions, candidates, proposals, relationships, evidence, sources, options, tradeoffs, assumptions, and history.
- Existing artifact discovery remains compatible with `.agents/decisions/decisions.md` and `.agents/decisions/decisions.NNNN.md`; structured JSON lifecycle files remain outside generic artifact discovery.
- Existing `ArtifactRotationService.RotateCurrentDecisionsAsync` can rotate generated decision index snapshots.
- Updated the M0 checklist to mark M0C and its completed projection/discovery/rotation test items.

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` passes with 240 tests.
- `dotnet build CommandCenter.slnx` succeeds with 0 warnings and 0 errors.

## Next Slice

- Start M0D: implement safe recovery from structured artifacts when generated markdown projections are missing, projection regeneration on recovery/restart paths, and full M0 regression coverage.
