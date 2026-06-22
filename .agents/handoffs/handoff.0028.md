# Handoff

## New State From This Slice

- Continued M6 decision resolution by extracting proposal resolution into a first-class backend service boundary.
- Added `IDecisionResolutionService`.
- Added `DecisionResolutionService`.
  - Owns resolution command validation.
  - Owns ready-state validation before resolution.
  - Owns selected option validation.
  - Owns immutable source proposal snapshot capture.
  - Owns authoritative decision creation.
  - Owns marking the source proposal `Resolved`.
  - Owns decision/proposal markdown projection refresh and decision index refresh.
- Removed `ResolveProposalAsync` from `IDecisionGenerationService`.
- Removed resolution implementation from `DecisionGenerationService`.
- Registered `IDecisionResolutionService` in decision DI.
- Updated the resolve endpoint to call `IDecisionResolutionService` while preserving the same route, request body, response shape, and error mapping.
- Updated existing resolution-focused backend tests to resolve through `DecisionResolutionService`.
- Updated `.agents/milestones/m6-decision-resolution.md` to mark `IDecisionResolutionService` complete.
- Rotated the previous handoff to `.agents/handoffs/handoff.0027.md`.

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter DecisionGenerationServiceTests` passes with 24 tests.
- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` passes with 298 tests.

## Next Slice

- Continue M6 with accept/reject/defer semantics.
- Add focused `DecisionResolutionService` tests for `Accepted`, `Rejected`, and `Deferred` outcomes covering:
  - decision state
  - proposal state
  - decision artifact creation
  - resolution snapshot creation
  - index/projection output
- Reconcile `DecisionResolutionService` behavior with `DecisionLifecycleRules`, which already says:
  - accepted decisions transition to `Resolved`
  - rejected decisions transition to `Archived`
  - deferred decisions transition to `UnderReview`
- Do this before adding any resolution UI.
