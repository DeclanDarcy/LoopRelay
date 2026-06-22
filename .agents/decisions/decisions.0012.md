# Decisions

## Newly Authorized

- M3 Proposal Generation is complete.
- The implemented M3 lifecycle chain is accepted as aligned with the roadmap: decision context, discovery, candidate, promotion, proposal, review, refinement, ready for resolution, resolution, and `DEC-*`.
- Proposal discard is authorized only before decision authority exists.
- Discard from `Resolved` proposals must remain prohibited because `DEC-*` creation moves authority into the decision layer.
- Allowed discard source states remain `Generated`, `Viewed`, `NeedsRefinement`, `Refined`, and `ReadyForResolution`.
- `Resolved`, `Discarded`, and `Expired` remain terminal proposal states for discard purposes.
- Proposal authority, review artifacts, and decision authority must remain separate as the implementation moves into M4 and later milestones.
- Review notes should not be stored inside `proposal.json` or decision history as proposal or decision state.
- M4 should begin with backend review-workspace primitives before UI work.
- Review notes should be treated as reviewer evidence and persisted as their own review-workspace layer.
- Initial M4 priority order is `DecisionReviewNote`, `DecisionReviewState`, review-note persistence, review-note endpoints, attribution metadata, proposal-to-review linkage, review workspace read models, and review-note tests.
- UI review workspace, option comparison UI, and evidence browser UI should be postponed until the review artifact model stabilizes.

## Current Milestone Status

- M0 is complete.
- M1 is complete.
- M2 is complete.
- M3 is complete.
- M4 is ready to start.
