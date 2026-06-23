# Decisions

## Newly Authorized

- Close Milestone 4 as complete.
- Treat the final M4 comparison/disqualifier slice as sufficient for recommendation generation to begin.
- Begin Milestone 5 immediately.
- Replace the remaining `options[0]` recommendation default as quickly as possible.
- Introduce M5 around:
  - `IRecommendationService`
  - `OptionEvaluation`
  - `RecommendationEvidence`
  - `RecommendationMode`
- Recommendation generation should consume:
  - `DecisionGenerationContext`
  - validated options
  - structured tradeoff analysis
  - option comparisons
  - disqualifying constraints
  - known risks
  - source evidence
- Recommendation generation must reuse the existing projected context boundary and must not introduce competing repository parsing or context extraction.
- Recommendation output must be explainable and reconstructable from generated benefits, costs, risks, constraints, dependencies, and consequences.
- Recommendation generation must respect disqualifying constraints and must not silently prefer constraint-violating options.
- Recommendation generation must support a no-recommendation mode when evidence is insufficient, contradictions are unresolved, or unknown risk prevents a defensible preference.
- Keep recommendation output advisory and separate from human resolution authority.

## Not Authorized

- Do not start package versioning before M5 recommendation generation is complete.
- Do not start quality assessment before M5 recommendation generation is complete.
- Do not start dashboards before M5 recommendation generation is complete.
- Do not start certification before M5 recommendation generation is complete.
- Do not start throughput reporting before M5 recommendation generation is complete.
- Do not use opaque hidden ranking as the basis for recommendations.
- Do not allow recommendation generation to create new decision authority concepts.
