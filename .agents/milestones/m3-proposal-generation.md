# Milestone 3: Decision Proposal Generation

## Goal

transform validated candidates into structured decision proposals with options, tradeoffs, assumptions, evidence, and advisory recommendations.

## Backend Work

- [ ] Add `DecisionProposal`, `DecisionProposalState`, `DecisionOption`, `DecisionTradeoff`, `DecisionRecommendation`, `DecisionAssumption`, and generation diagnostics.
- [ ] Implement `IDecisionGenerationService`.
- [ ] Generate deterministic first-pass proposals from `DecisionContext` and candidate evidence.
- [ ] Require at least one viable option and a clear statement when only one option is available.
- [ ] Avoid fake alternatives.
- [ ] Bind every recommendation to evidence, constraints, assumptions, and tradeoffs.
- [ ] Persist proposals under `.agents/decisions/proposals`.
- [ ] Render `proposal.md`.
- [ ] Move proposal state through draft, generated, viewed, needs refinement, refined, ready for resolution, resolved, expired, and discarded.

## Tests

- [ ] Candidate-to-proposal tests.
- [ ] Option generation tests.
- [ ] Tradeoff modeling tests.
- [ ] Recommendation evidence tests.
- [ ] Assumption visibility tests.
- [ ] Proposal persistence tests.
- [ ] Proposal state transition tests.
- [ ] Proposal expiration tests.

## Exit Criteria

- [ ] Proposals expose alternatives and tradeoffs.
- [ ] Recommendation is advisory and traceable.
- [ ] Proposal lifecycle is distinct from review notes and decision state.
- [ ] Proposal generation does not mutate decisions, milestones, operational context, or execution state.
