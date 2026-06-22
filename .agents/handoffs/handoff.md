# Handoff

## New State From This Slice

- Continued M3 proposal lifecycle mechanics by adding backend-owned review transition actions.
- Added `DecisionProposalTransitionRequest` for proposal lifecycle transition reasons.
- Extended `IDecisionGenerationService` and `DecisionGenerationService` with:
  - `MarkProposalViewedAsync`
  - `MarkProposalNeedsRefinementAsync`
  - `MarkProposalReadyForResolutionAsync`
- Added proposal review endpoints:
  - `POST /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/review/viewed`
  - `POST /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/review/needs-refinement`
  - `POST /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/review/ready-for-resolution`
- Proposal transition mutations now share one service path that validates `DecisionLifecycleRules`, appends history, persists structured proposal JSON/history, refreshes `proposal.md`, and refreshes `decisions.md`.
- Invalid review transitions return conflict through the existing endpoint error convention.
- M3 status was updated to show review transition work as complete while refinement, resolution, and discard transitions remain open.

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` passes with 267 tests.
- `dotnet build CommandCenter.slnx` succeeds with 0 warnings and 0 errors.

## Next Slice

- Continue M3 by implementing refinement mechanics: proposal revisions, transition from `NeedsRefinement` to `Refined`, revision history persistence, and endpoint/test coverage before touching resolution.
