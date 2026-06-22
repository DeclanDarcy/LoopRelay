# Handoff

## New State From This Slice

- Continued M5 refinement workflow by adding the authorized proposal lineage read model/projection.
- Added `DecisionProposalLineage`, `DecisionProposalLineageEvent`, and `DecisionProposalRevisionSnapshot`.
- Extended `IDecisionRefinementService` with `GetProposalLineageAsync`.
- `DecisionRefinementService` now builds a read-only lineage projection containing:
  - current authoritative proposal and fingerprint
  - review status
  - ordered proposal history, revision, review-note, and review-state events
  - historical revision snapshots with revision comparisons
  - review notes
  - diagnostics that explicitly preserve the current-proposal versus historical-revision authority boundary
- Added backend endpoint:
  - `GET /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/lineage`
- Added Tauri bridge command:
  - `get_decision_proposal_lineage`
- Added TypeScript API/type surface and dev mock support for proposal lineage.
- Updated `.agents/milestones/m5-refinement-workflow.md` to mark proposal lineage projection complete.
- Rotated the previous handoff to `.agents/handoffs/handoff.0023.md`.

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` passes with 297 tests.
- `npm run lint --prefix src/CommandCenter.UI` passes.
- `npm run test --prefix src/CommandCenter.UI` passes with 42 files and 151 tests.
- `cargo build --manifest-path src/CommandCenter.Shell/Cargo.toml` passes.

## Next Slice

- Start M5 read-only UI work using the new lineage projection:
  - add a revision history panel
  - add a revision comparison view
  - make the current proposal visibly distinct from historical revisions
- Keep refinement mutation UI deferred until the read-only lineage/revision surfaces are working and characterized.
