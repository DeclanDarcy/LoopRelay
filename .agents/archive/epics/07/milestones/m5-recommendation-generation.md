# Milestone 5: Recommendation Generation

## Goal

derive a recommendation from context, options, tradeoffs, constraints, risks, and evidence.

## Work

- [x] Add `IRecommendationService`.
- [x] Add `OptionEvaluation` containing:
  - [x] strengths
  - [x] weaknesses
  - [x] risks
  - [x] constraints
  - [x] summary
  - [x] score/ranking metadata only if it remains explainable
- [x] Add `DecisionRecommendation` fields if needed:
  - [x] summary
  - [x] rationale
  - [x] supporting factors
  - [x] concerns
  - [x] assumptions
  - [x] alternative explanation
  - [x] mode
- [x] Add `RecommendationEvidence` and evidence types:
  - [x] benefit
  - [x] cost
  - [x] risk
  - [x] dependency
  - [x] consequence
  - [x] constraint
  - [x] prior decision
  - [x] repository state
- [x] Support recommendation modes:
  - [x] preferred option
  - [x] preferred plus alternative
  - [x] no recommendation
- [x] Allow no recommendation when evidence is insufficient, uncertainty is excessive, or contradiction remains unresolved.
  - [x] Excessive uncertainty and all-options-disqualified cases produce no recommendation.
  - [x] Evidence-insufficient and contradiction-specific no-recommendation heuristics are implemented.
- [x] Explain why the recommended option won and why each alternative lost.
- [x] Generate concerns and assumptions for every recommendation.
- [x] Refuse to recommend an option that violates hard constraints unless the recommendation mode is no recommendation or escalation.
- [x] Remove hardcoded `options[0]` recommendation behavior.

## Tests

- [x] Recommendations are derived from structured option evaluations.
- [x] Reordering options does not change the recommendation when evidence is unchanged.
- [x] Recommended option has supporting evidence.
- [x] Alternatives have explicit losing rationale.
- [x] Concerns and assumptions are present.
- [x] Excessive uncertainty produces no recommendation.
- [x] Constraint violation prevents recommendation or produces escalation/no-recommendation.
- [x] Insufficient evidence produces no recommendation.
- [x] Unresolved contradiction produces no recommendation.
- [x] Prior-decision and repository-state recommendation evidence is explicit.

## Exit Criteria

- [x] The system can answer what it recommends, why, what evidence supports it, what assumptions matter, and why alternatives are weaker.
- [x] At this point the generated proposal contains enough system-authored content for a human to resolve without writing the decision manually.
