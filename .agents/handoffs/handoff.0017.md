# Handoff

## New State This Slice

- Continued Milestone 3: Decision Pipeline Completion, focusing on proposal review transparency.
- `DecisionProposalViewer` now accepts the selected proposal lifecycle eligibility projection from `DecisionLifecycleTab`.
- The proposal viewer now renders an authoritative `Proposal review state` block with:
  - current backend review state
  - backend review updated timestamp
  - backend proposal lifecycle state
  - last lifecycle transition from `proposal.history`
  - last transition reason
  - review reason
  - allowed next transitions and allowed actions from lifecycle eligibility
  - unavailable transition reasons and governing rules from lifecycle eligibility
- `DecisionLifecycleTab` passes the selected proposal eligibility into `DecisionProposalViewer`.
- Added characterization coverage for review state, last transition, allowed transition, unavailable transition reason, and governing rule rendering.
- Scoped an existing lifecycle-navigation assertion because blocked proposal reasons now intentionally render in both the viewer and the action-control panel.
- Updated `.agents/milestones/m3-decision-pipeline.md` to mark proposal viewer review-state and transition transparency complete.
- Rotated previous handoff to `.agents/handoffs/handoff.0016.md`.

## Verification

- `npm test -- --run src/test/characterization/decisionProposalViewer.test.tsx` passed with 4 tests.
- `npm test -- --run src/test/characterization/decisionLifecycleNavigation.test.tsx` passed with 4 tests.
- `npm run build` passed.

## Remaining Milestone 3 Work

- Perform the review-state placement audit and decide whether the separate Proposal Actions panel should remain as-is or be consolidated with the viewer in a later cleanup.
- Classify lower-priority lifecycle features as Core MVP, Deferred, Internal, or Remove:
  - proposal review notes
  - proposal revision list
  - revision comparison
  - context snapshot listing
- Add broader end-to-end lifecycle characterization after the disposition audit.
