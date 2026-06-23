# Milestone 5: Recommendation Generation

## Goal

derive a recommendation from context, options, tradeoffs, constraints, risks, and evidence.

## Work

- [ ] Add `IRecommendationService`.
- [ ] Add `OptionEvaluation` containing:
  - [ ] strengths
  - [ ] weaknesses
  - [ ] risks
  - [ ] constraints
  - [ ] summary
  - [ ] score/ranking metadata only if it remains explainable
- [ ] Add `DecisionRecommendation` fields if needed:
  - [ ] summary
  - [ ] rationale
  - [ ] supporting factors
  - [ ] concerns
  - [ ] assumptions
  - [ ] alternative explanation
  - [ ] mode
- [ ] Add `RecommendationEvidence` and evidence types:
  - [ ] benefit
  - [ ] cost
  - [ ] risk
  - [ ] dependency
  - [ ] consequence
  - [ ] constraint
  - [ ] prior decision
  - [ ] repository state
- [ ] Support recommendation modes:
  - [ ] preferred option
  - [ ] preferred plus alternative
  - [ ] no recommendation
- [ ] Allow no recommendation when evidence is insufficient, uncertainty is excessive, or contradiction remains unresolved.
- [ ] Explain why the recommended option won and why each alternative lost.
- [ ] Generate concerns and assumptions for every recommendation.
- [ ] Refuse to recommend an option that violates hard constraints unless the recommendation mode is no recommendation or escalation.
- [ ] Remove hardcoded `options[0]` recommendation behavior.

## Tests

- [ ] Recommendations are derived from structured option evaluations.
- [ ] Reordering options does not change the recommendation when evidence is unchanged.
- [ ] Recommended option has supporting evidence.
- [ ] Alternatives have explicit losing rationale.
- [ ] Concerns and assumptions are present.
- [ ] Excessive uncertainty produces no recommendation.
- [ ] Constraint violation prevents recommendation or produces escalation/no-recommendation.

## Exit Criteria

- [ ] The system can answer what it recommends, why, what evidence supports it, what assumptions matter, and why alternatives are weaker.
- [ ] At this point the generated proposal contains enough system-authored content for a human to resolve without writing the decision manually.
