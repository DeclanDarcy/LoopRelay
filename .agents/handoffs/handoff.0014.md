# Handoff

## New State From This Slice

- Continued M4: Decision Review Workspace.
- Added dedicated backend read models for proposal review inspection:
  - `DecisionProposalBrowserItem`
  - `DecisionOptionComparison`
  - `DecisionOptionComparisonItem`
  - `DecisionEvidenceInspection`
  - `DecisionEvidenceInspectionItem`
  - `DecisionSourceAttribution`
- Extended `IDecisionReviewService` and `DecisionReviewService` with read-only projection methods for:
  - proposal browser items with optional proposal-state filtering
  - option comparison
  - evidence inspection
  - source attribution navigation
- Proposal browser items are composed from repository-backed proposals, source candidates, and separate review status. They expose state, classification, priority, created/updated timestamps, review status, and resolution status without becoming authority.
- Option comparison groups option descriptions with related tradeoff benefits, costs, recommendation status, and evidence.
- Evidence inspection groups proposal, option, tradeoff, recommendation, and assumption evidence with source attribution.
- Source attribution exposes source kind, relative path, section, excerpt, and the original source reference for UI navigation.
- Added read-only endpoints:
  - `GET /api/repositories/{repositoryId}/decisions/proposals/browser?states=Viewed,Refined`
  - `GET /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/options`
  - `GET /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/evidence`
  - `GET /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/sources`
- Updated `.agents/milestones/m4-review-workspace.md`; the dedicated backend proposal browser, option comparison, evidence inspection, and source attribution read-model item is now complete.
- Rotated the previous handoff to `.agents/handoffs/handoff.0013.md`.

## Verification

- `dotnet build CommandCenter.slnx` succeeds with 0 warnings and 0 errors.
- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter DecisionReviewServiceTests` passes with 6 tests.
- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` passes with 291 tests.

## Next Slice

- Continue M4 by starting the Decisions UI now that backend read contracts are stable.
- Recommended order:
  1. Add UI decision types and API calls for review workspace, proposal browser, option comparison, evidence inspection, and source attribution.
  2. Add Decisions tab routing and a basic lifecycle shell.
  3. Add proposal browser filters for generated, viewed, needs-refinement, refined, ready-for-resolution, resolved, expired, and discarded.
  4. Add full proposal viewer with option comparison and adjacent evidence/source attribution.
  5. Add review notes panel using the existing note endpoints.
