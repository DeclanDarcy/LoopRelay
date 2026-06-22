# Milestone 4: Decision Review Workspace

## Goal

provide full proposal inspection before refinement or resolution.

## Backend Work

- [x] Add review state and review note models.
- [x] Implement `IDecisionReviewService`.
- [x] Support review actions:
  - [x] viewed
  - [x] needs refinement
  - [x] ready for resolution
- [x] Add read models for proposal browser, proposal viewer, option comparison, evidence inspection, source attribution, and review diagnostics.
  - [x] Add initial proposal review workspace read model with proposal, review status, notes, revisions, and diagnostics.
  - [x] Add dedicated proposal browser, option comparison, evidence inspection, and source attribution read models.
  - [x] Keep review diagnostics discoverable in the proposal viewer instead of adding a redundant diagnostics panel.
- [x] Persist notes separately from proposal revisions.
- [x] Keep proposal state and review notes synchronized through backend-owned transitions.

## UI Work

- [x] Add Decisions tab and decision lifecycle route composition.
- [x] Add candidate browser with active, promoted, dismissed, expired, and duplicate filters.
- [x] Add proposal browser with generated, viewed, needs refinement, refined, ready for resolution, resolved, expired, and discarded filters.
- [x] Add full proposal viewer.
- [x] Add option comparison.
- [x] Add evidence/source attribution navigation.
- [x] Add review notes panel.

## Tests

- [x] Backend review transition tests.
- [x] Review note persistence tests.
- [x] Proposal viewer characterization tests.
- [x] Evidence and attribution display tests.
- [x] Candidate/proposal filter tests.
- [x] Navigation tests.

## Exit Criteria

- [x] Users can inspect the full proposal without resolving it.
- [x] Proposal state and review notes are visible and persisted.
- [x] Evidence remains visible where it matters.
- [x] UI does not mutate proposal content during review.
