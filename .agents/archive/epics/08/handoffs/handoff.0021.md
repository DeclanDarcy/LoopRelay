# Handoff

## New State

- Continued Milestone 9 by implementing Decision proposal-generation preparation for promoted candidates.
- `WorkflowDecisionProjection` now exposes `CandidateState`, allowing workflow preparation to distinguish discovered candidates from promoted candidates without parsing diagnostics.
- `WorkflowPreparationCommand` now separates Decision-stage preparation into:
  - `DiscoverDecisionCandidates` for candidate discovery.
  - `GenerateDecisionProposal` for proposal/package review artifact creation from a promoted candidate.
- `WorkflowPreparationService` now accepts optional `IDecisionGenerationService` and calls the existing Decisions-owned `GenerateProposalAsync(repositoryId, candidateId)` only when:
  - workflow is at Decision stage.
  - the current candidate is promoted.
  - no proposal/package duplicate evidence already exists.
  - the only open gate being tolerated is `DecisionResolution`.
- Generated proposal evidence is recorded as `decision-proposal:<proposalId>` in `WorkflowPreparationEvent.CreatedArtifactIds`.
- `DecisionResolution` remains open after proposal preparation; workflow does not mark proposals viewed, ready, refined, resolved, expired, or discarded, and does not promote candidates.
- Updated `.agents/milestones/m9-continuation.md` to mark preparation rules complete.
- Rotated previous `.agents/handoffs/handoff.md` to `.agents/handoffs/handoff.0020.md`.

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter WorkflowProjectionServiceTests` passed: 92 tests.
- `dotnet build CommandCenter.slnx` passed with 0 warnings and 0 errors.

## Notes

- Decision proposal generation is treated as review-artifact creation, analogous to commit preparation and operational-context proposal generation.
- The relevant authority invariant is that proposal generation may create reviewable evidence before the human decision gate is satisfied, but it must not perform proposal review transitions or decision resolution.

## Next Slice

- Continue Milestone 9 by implementing the hosted continuation/preparation runner behind `CommandCenter:Workflow:ContinuationEnabled` and interval configuration, with startup/restart idempotency coverage before enabling any background behavior.
