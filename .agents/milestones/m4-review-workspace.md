# Milestone 4: Decision Review Workspace

## Goal

provide full proposal inspection before refinement or resolution.

## Backend Work

- [ ] Add review state and review note models.
- [ ] Implement `IDecisionReviewService`.
- [ ] Support review actions:
  - [ ] viewed
  - [ ] needs refinement
  - [ ] ready for resolution
- [ ] Add read models for proposal browser, proposal viewer, option comparison, evidence inspection, source attribution, and review diagnostics.
- [ ] Persist notes separately from proposal revisions.
- [ ] Keep proposal state and review notes synchronized through backend-owned transitions.

## UI Work

- [ ] Add Decisions tab and decision lifecycle route composition.
- [ ] Add candidate browser with active, promoted, dismissed, expired, and duplicate filters.
- [ ] Add proposal browser with generated, viewed, needs refinement, refined, ready for resolution, resolved, expired, and discarded filters.
- [ ] Add full proposal viewer.
- [ ] Add option comparison.
- [ ] Add evidence/source attribution navigation.
- [ ] Add review notes panel.

## Tests

- [ ] Backend review transition tests.
- [ ] Review note persistence tests.
- [ ] Proposal viewer characterization tests.
- [ ] Evidence and attribution display tests.
- [ ] Candidate/proposal filter tests.
- [ ] Navigation tests.

## Exit Criteria

- [ ] Users can inspect the full proposal without resolving it.
- [ ] Proposal state and review notes are visible and persisted.
- [ ] Evidence remains visible where it matters.
- [ ] UI does not mutate proposal content during review.
