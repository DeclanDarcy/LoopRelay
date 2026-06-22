# Milestone 2: Decision Discovery

## Goal

identify decision candidates without generating recommendations.

## Backend Work

- [ ] Add `DecisionCandidate`, `DecisionCandidateState`, `DecisionSignal`, `DecisionEvidence`, priority, and diagnostics models.
- [ ] Implement `IDecisionDiscoveryService`.
- [ ] Detect signals for:
  - [ ] ambiguity
  - [ ] conflict
  - [ ] missing direction
  - [ ] blocked execution
  - [ ] architectural forks
  - [ ] milestone/context drift
  - [ ] repeated continuity uncertainty
  - [ ] stale open decisions
- [ ] Classify candidates as architectural, strategic, tactical, or operational.
- [ ] Prioritize candidates by impact, urgency, blocking status, and risk.
- [ ] Persist candidates under `.agents/decisions/candidates`.
- [ ] Support dismissal, expiration, and duplicate marking using the explicit candidate expiration policy.
- [ ] Add explicit candidate-to-proposal promotion boundary.

## Tests

- [ ] Signal extraction tests.
- [ ] Candidate classification tests.
- [ ] Evidence attribution tests.
- [ ] Prioritization tests.
- [ ] Candidate persistence tests.
- [ ] Duplicate candidate suppression tests.
- [ ] Dismissed candidate tests.
- [ ] Expired candidate tests.

## Exit Criteria

- [ ] Discovery produces candidates, not recommendations.
- [ ] Every candidate has evidence and source attribution.
- [ ] Candidate lifecycle state survives restart.
- [ ] Promotion to proposal is explicit and not automatic.
- [ ] Dismissed, expired, and duplicate candidates do not accumulate as active work.
