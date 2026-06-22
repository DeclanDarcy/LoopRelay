# Milestone 3: Decision Proposal Generation

## Goal

transform validated candidates into structured decision proposals with options, tradeoffs, assumptions, evidence, and advisory recommendations.

## Backend Work

- [ ] Add `DecisionProposal`, `DecisionProposalState`, `DecisionOption`, `DecisionTradeoff`, `DecisionRecommendation`, `DecisionAssumption`, and generation diagnostics.
- [x] Implement `IDecisionGenerationService`.
- [x] Generate deterministic first-pass proposals from `DecisionContext` and candidate evidence.
- [x] Require at least one viable option and a clear statement when only one option is available.
- [x] Avoid fake alternatives.
- [x] Bind every recommendation to evidence, constraints, assumptions, and tradeoffs.
- [x] Persist proposals under `.agents/decisions/proposals`.
- [x] Render `proposal.md`.
- [ ] Move proposal state through draft, generated, viewed, needs refinement, refined, ready for resolution, resolved, expired, and discarded.
  - [x] Backend-owned transitions for generated -> viewed, viewed -> needs refinement, generated/viewed -> ready for resolution, and allowed expiration paths.
  - [x] Refinement transition through explicit revision artifacts.
  - [ ] Resolution and discard transitions.

## Tests

- [x] Candidate-to-proposal tests.
- [x] Option generation tests.
- [x] Tradeoff modeling tests.
- [x] Recommendation evidence tests.
- [x] Assumption visibility tests.
- [x] Proposal persistence tests.
- [ ] Proposal state transition tests.
  - [x] Review transition success, persistence, projection refresh, and conflict-path coverage.
  - [x] Refinement transition, revision artifact, endpoint, and conflict-path coverage.
- [x] Proposal expiration tests.

## Exit Criteria

- [x] Proposals expose alternatives and tradeoffs.
- [x] Recommendation is advisory and traceable.
- [ ] Proposal lifecycle is distinct from review notes and decision state.
- [x] Proposal generation does not mutate decisions, milestones, operational context, or execution state.
