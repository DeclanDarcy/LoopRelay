# Handoff

## New State From This Slice

- Continued M5 read-only UI work for proposal evolution.
- Added `useDecisionProposalLineage` to load backend-owned lineage through `get_decision_proposal_lineage`.
- Added `DecisionRevisionHistory` with:
  - lineage summary
  - current proposal authority callout
  - revision list navigation
  - backend-provided revision comparison rendering
  - evolution event sequence
  - diagnostics preserving current proposal versus historical revision authority
- Wired lineage loading into `DecisionLifecycleTab` for the selected proposal.
- Updated `DecisionProposalViewer` to label current proposal content as authoritative and historical revisions as explanatory.
- Updated dev mock revision fixtures to satisfy the fuller revision model used by lineage/comparison.
- Added characterization coverage for revision history/comparison and updated navigation coverage for lineage loading.
- Marked M5 UI revision history, revision comparison, and current-versus-historical distinction complete.
- Rotated the previous handoff to `.agents/handoffs/handoff.0024.md`.

## Verification

- `npm run lint --prefix src/CommandCenter.UI` passes.
- `npm run test --prefix src/CommandCenter.UI` passes with 43 files and 153 tests.
- `npm run build --prefix src/CommandCenter.UI` passes.

## Next Slice

- Finish M5 by adding the refinement request form.
- Keep mutation authority backend-owned: the form should submit structured refinement requests and render returned proposal/revision state rather than editing proposal history client-side.
- Add focused characterization tests for refinement request input state, disabled/error states, and returned lineage refresh after refinement.
