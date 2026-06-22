# Handoff

## New State From This Slice

- Continued M4 inspection/navigation work.
- Added `DecisionCandidateBrowser` in `src/CommandCenter.UI/src/features/decisions/DecisionCandidateBrowser.tsx`.
- `DecisionLifecycleTab` now renders the dedicated candidate browser instead of the prior inline six-row candidate preview.
- Candidate browser keeps selected candidate and selected state filters as React presentation state only.
- Candidate filters cover `Discovered`, `Promoted`, `Dismissed`, `Expired`, and `Duplicate`; the default view shows active `Discovered` and `Promoted` candidates.
- Candidate browser exposes selected candidate metadata, signal count, and evidence count without mutation controls.
- `devTauriMock` now seeds representative candidates for all candidate lifecycle states.
- Added `decisionCandidateBrowser.test.tsx` for candidate filtering, local selection, and no mutation controls.
- Added `decisionLifecycleNavigation.test.tsx` proving proposal selection loads the matching backend-owned review workspace into the viewer.
- Updated `.agents/milestones/m4-review-workspace.md`; candidate browser and navigation tests are now complete.
- Rotated the previous handoff to `.agents/handoffs/handoff.0017.md`.

## Verification

- `npm run lint --prefix src/CommandCenter.UI` succeeds.
- `npm run test --prefix src/CommandCenter.UI -- decisionCandidateBrowser decisionLifecycleNavigation decisionProposalBrowser` passes.
- `npm run test --prefix src/CommandCenter.UI` passes with 40 files and 145 tests.
- `npm run build --prefix src/CommandCenter.UI` succeeds.

## Next Slice

- Continue M4 with the remaining inspection/navigation UI:
  1. Add a dedicated option comparison surface backed by the existing option comparison read model.
  2. Add evidence/source attribution navigation backed by the existing evidence inspection and source attribution read models.
  3. Add characterization coverage for option comparison and evidence/source navigation.
  4. Keep review, refinement, resolution, discard, and other mutation controls deferred until the inspection surfaces are complete.
