# Milestone 2: Decision Discovery

## Goal

identify decision candidates without generating recommendations.

## Backend Work

- [x] Add `DecisionCandidate`, `DecisionCandidateState`, `DecisionSignal`, `DecisionEvidence`, priority, and diagnostics models.
- [x] Implement `IDecisionDiscoveryService`.
- [ ] Detect signals for:
  - [x] ambiguity
  - [x] conflict
  - [x] missing direction
  - [x] blocked execution
  - [x] architectural forks
  - [x] milestone/context drift
  - [x] repeated continuity uncertainty
  - [x] stale open decisions
- [x] Classify candidates as architectural, strategic, tactical, or operational.
- [x] Prioritize candidates by impact, urgency, blocking status, and risk.
- [x] Persist candidates under `.agents/decisions/candidates`.
- [x] Support dismissal, expiration, and duplicate marking using the explicit candidate expiration policy.
- [x] Add explicit candidate-to-proposal promotion boundary.

## Tests

- [x] Signal extraction tests.
- [x] Candidate classification tests.
- [x] Evidence attribution tests.
- [x] Prioritization tests.
- [x] Candidate persistence tests.
- [x] Duplicate candidate suppression tests.
- [x] Dismissed candidate tests.
- [ ] Expired candidate tests.

## Exit Criteria

- [x] Discovery produces candidates, not recommendations.
- [x] Every candidate has evidence and source attribution.
- [x] Candidate lifecycle state survives restart.
- [x] Promotion to proposal is explicit and not automatic.
- [ ] Dismissed, expired, and duplicate candidates do not accumulate as active work.
