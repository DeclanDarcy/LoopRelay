# Decisions

## Newly Authorized

- The M3 opening proposal-generation slice is accepted as aligned with the roadmap.
- The explicit lifecycle boundary remains authoritative: candidate discovery, candidate promotion, and proposal generation are separate steps.
- Proposal generation must continue to require `DecisionCandidateState.Promoted`; discovery and promotion must not imply proposal creation authority.
- Proposal options must remain evidence-driven. A proposal should use one option by default and add alternatives only when conflict or fork evidence supports them.
- M3 should remain scoped to proposal lifecycle mechanics before content evolution, refinement, resolution, governance, or execution projection.
- The next M3 slice should prioritize proposal lifecycle state transitions before refinement work.
- `ProposalState`, `ReviewState`, and `DecisionState` must remain separate concepts.
- Review notes should be represented separately from proposal lifecycle state; `NeedsRefinement` must not be the only record of review activity.
- The next implementation slice should add backend-owned review transitions for viewed, needs refinement, and ready for resolution, plus valid, invalid, terminal, persistence, success-path, and conflict-path coverage.

## Current Milestone Status

- M0 is complete.
- M1 is complete.
- M2 is complete.
- M3 is in progress.
