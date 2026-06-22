# Handoff

## New State From This Slice

- Completed M0D and marked M0 domain foundation exit criteria complete.
- Added `IDecisionArtifactProjectionService.RecoverMissingProjectionsAsync`.
- `DecisionArtifactProjectionService` now regenerates missing `decision.md`, `candidate.md`, `proposal.md`, and `.agents/decisions/decisions.md` projections from structured JSON artifacts.
- Recovery writes only missing generated markdown projections; existing markdown is not overwritten during repository projection reads.
- `RepositoryProjectionService` now invokes decision projection recovery before artifact discovery when the decision projection service is available.
- `CommandCenter.Middle` now references `CommandCenter.Decisions` for backend-owned workspace/dashboard recovery.
- Added tests proving deleted generated markdown is regenerated equivalently from structured artifacts after service restart.
- Added workspace refresh coverage proving missing `decisions.md` is restored from structured records before `HasCurrentDecisions` is projected.

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` passes with 242 tests.
- `dotnet build CommandCenter.slnx` succeeds with 0 warnings and 0 errors.

## Next Slice

- Start M1: decision context resolution. Implement deterministic context snapshots from repository artifacts, diagnostics, validation, and repository-owned context snapshot persistence under `.agents/decisions/contexts/`.
