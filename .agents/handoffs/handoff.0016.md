# Handoff

## New State From This Slice

- Continued M4: Decision Review Workspace UI.
- Added `DecisionProposalBrowser` in `src/CommandCenter.UI/src/features/decisions/DecisionProposalBrowser.tsx`.
- The proposal browser renders rows from `DecisionProposalBrowserItem`, supports backend-driven proposal state filters, and keeps selected proposal state local to the browser.
- Wired proposal filter state through `App.tsx` into `useDecisionProposals`, preserving the existing backend browser endpoint contract.
- Updated `DecisionLifecycleTab` to delegate proposal rendering to the dedicated proposal browser component.
- Expanded `devTauriMock` proposal browser data with generated, viewed, and ready-for-resolution examples, and made the mock browser command honor `states` filters.
- Added proposal browser characterization coverage in `src/CommandCenter.UI/src/test/characterization/decisionProposalBrowser.test.tsx`.
- Updated `.agents/milestones/m4-review-workspace.md`; the proposal browser UI and candidate/proposal filter test items are now complete.
- Rotated the previous handoff to `.agents/handoffs/handoff.0015.md`.

## Verification

- `npm run lint --prefix src/CommandCenter.UI` succeeds.
- `npm run test --prefix src/CommandCenter.UI` passes with 37 files and 138 tests.
- `npm run build --prefix src/CommandCenter.UI` succeeds.

## Next Slice

- Continue M4 with the full proposal viewer:
  1. Load the selected proposal review workspace from the backend when a proposal is selected.
  2. Render proposal context, options, tradeoffs, recommendation, assumptions, diagnostics, review state, notes, and revisions.
  3. Keep evidence near the recommendation, option, tradeoff, or assumption it supports.
  4. Do not add review, refinement, or resolution mutation controls until the viewer and evidence/source navigation are visible.
