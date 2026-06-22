# Decisions

## Newly Authorized

- The M3 review-transition slice is accepted as aligned with the roadmap.
- The lifecycle distinction remains authoritative: `ProposalState`, review content, and decision authority are separate concepts.
- Review transitions for `Generated -> Viewed`, `Viewed -> NeedsRefinement`, and `Viewed -> ReadyForResolution` belong in M3 and do not require refinement artifacts, review notes, or resolution to exist first.
- Invalid proposal transitions must continue to be centrally enforced by backend lifecycle rules so endpoints, future UI, and future Tauri commands cannot drift.
- Authoritative proposal state changes must refresh repository projections.
- `ReadyForResolution` must remain non-expirable under the current transition matrix; weakening that path would blur proposal review completion with human decision authority.
- Review notes must remain separate from proposal lifecycle state and should not be embedded into proposal state.
- The next M3 slice should implement refinement before resolution.
- Refinement work should introduce `DecisionProposalRevision` and `DecisionRefinementRequest` before implementing refinement transitions.
- Proposal revisions should persist as separate `revisions/REV-*.json` and `revisions/REV-*.md` artifacts.
- `NeedsRefinement -> Refined` should occur only through explicit refinement operations, not direct state mutation.
- Revisions should record reason, changed fields, source proposal, and timestamp.
- Tests should prove that `NeedsRefinement -> Refined` requires a revision artifact.

## Current Milestone Status

- M0 is complete.
- M1 is complete.
- M2 is complete.
- M3 is in progress.
