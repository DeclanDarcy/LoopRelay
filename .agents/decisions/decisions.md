# Decisions

## Newly Authorized

- Accept the Milestone 8 service-level persisted quality-history slice as correct.
- Preserve the architectural rule that persisted assessments are the source for trend history, not freshly recomputed repository state.
- Continue keeping quality semantics in quality services while repository implementations remain persistence mechanisms.
- Treat fixed, validator-compatible quality artifact timestamp IDs as required before endpoints, reports APIs, or dashboards.
- Continue finishing Milestone 8 backend semantics before adding endpoints or UI dashboards.
- Implement recommendation stability next as a quality signal derived from historical recommendation behavior.
- Implement tradeoff quality, context quality, and constraint quality as explainable quality signals, not opaque score-only aggregation.
- Keep `DecisionQualitySignal` as the primary abstraction and use scores only as secondary aggregation.
- Preserve the backend-first, evidence-first ordering so Milestone 8 can provide durable evidence for later Milestone 10 certification.

## Not Authorized

- Do not introduce endpoints, reports APIs, or dashboards before recommendation stability, tradeoff quality, context quality, and constraint quality semantics are covered by backend tests.
- Do not replace explainable quality signals with a single opaque quality score.
- Do not reconstruct quality trend history from current repository state when persisted assessment history is available.
