# Decisions

## Newly Authorized

- Keep Milestone 5 open after the first recommendation-generation slice.
- Treat the removal of `options[0]` recommendation behavior as the key milestone transition already achieved.
- Continue M5 by completing deterministic no-recommendation behavior for:
  - evidence-insufficient cases
  - unresolved contradiction cases
  - no viable option surviving validation
  - insufficient supporting evidence
  - critical dependency unknown
- Verify that recommendation scores remain explainable and reconstructable from evidence, rather than becoming opaque ranking output.
- Ensure recommendations explain why the preferred option won through concrete factors such as constraint avoidance, blocker resolution, fewer unresolved risks, and dependency impact.
- Make context-derived recommendation evidence explicit where applicable, especially:
  - `PriorDecision`
  - `RepositoryState`
  - `Constraint`
  - `Risk`
  - `Dependency`
- Preserve `NoRecommendation` as a first-class `RecommendationMode`, not null/missing/empty recommendation state.
- Preserve the advisory boundary:
  - generated recommendations remain non-authoritative
  - humans remain decision authorities
  - human resolution remains required before execution guidance
- Prefer additive recommendation schema evolution.
- Do not declare M5 complete until evidence-insufficient no-recommendation, contradiction-specific no-recommendation, explicit prior-decision evidence, and explicit repository-state evidence are implemented and verified.

## Not Authorized

- Do not close Milestone 5 yet.
- Do not let score values become the recommendation explanation.
- Do not treat recommendation output as decision authority.
- Do not move to package versioning, quality assessment, dashboards, throughput reporting, or certification before M5 is complete.
