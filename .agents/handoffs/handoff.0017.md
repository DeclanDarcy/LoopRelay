# Handoff

## New State From This Slice

- Continued M4: read-only full proposal viewer.
- Added `useDecisionProposalReview` in `src/CommandCenter.UI/src/hooks/useDecisionProposalReview.ts`.
- Added `DecisionProposalViewer` in `src/CommandCenter.UI/src/features/decisions/DecisionProposalViewer.tsx`.
- `DecisionLifecycleTab` now tracks selected proposal ID as React presentation state, loads the backend-owned proposal review workspace, and renders the full viewer below the proposal browser.
- `DecisionProposalBrowser` now emits selected proposal changes to its parent while keeping proposal lifecycle state backend-owned.
- `DecisionProposalViewer` renders proposal context, options, tradeoffs, recommendation, assumptions, diagnostics, review notes, revisions, evidence, and source attribution without mutation controls.
- `devTauriMock` now seeds and serves `get_decision_proposal_review` workspaces for local UI certification.
- Added proposal viewer characterization coverage in `src/CommandCenter.UI/src/test/characterization/decisionProposalViewer.test.tsx`.
- Updated `.agents/milestones/m4-review-workspace.md`; full proposal viewer, review notes panel, proposal viewer tests, evidence/source display tests, and M4 exit criteria are now complete.
- Rotated the previous handoff to `.agents/handoffs/handoff.0016.md`.

## Verification

- `npm run lint --prefix src/CommandCenter.UI` succeeds.
- `npm run test --prefix src/CommandCenter.UI -- decisionProposalViewer` passes.
- `npm run test --prefix src/CommandCenter.UI` passes with 38 files and 141 tests.
- `npm run build --prefix src/CommandCenter.UI` succeeds.

## Next Slice

- Continue M4 with the remaining review workspace UI gaps:
  1. Add the candidate browser with active, promoted, dismissed, expired, and duplicate filters.
  2. Add dedicated option comparison and evidence/source attribution navigation surfaces if the current embedded viewer is not sufficient.
  3. Add navigation tests around proposal selection and review workspace loading.
  4. Keep mutation controls deferred until the remaining inspection/navigation surface is complete.
