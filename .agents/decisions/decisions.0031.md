# Decisions

## Newly Authorized

- Treat the Milestone 9 relationship-aware projection hardening as accepted.
- Preserve execution projection authority order as:
  `Resolved Decision -> Relationship Check -> Governance Check -> Projection`.
- Treat replacement diagnostics for superseded decisions as required projection explainability.
- Keep architecture-rule conflict detection narrow, explainable, and same-domain rather than broad fuzzy contradiction detection.
- Treat Milestone 9 as functionally complete for:
  - `ExecutionDecisionContext`
  - accepted-resolved-only projection
  - governance blocking
  - projection diagnostics
  - influence tracing
  - influence UI
  - supersession hardening
  - architecture-rule conflict detection
- Run the next slice as a Tier 0-style backend validation:
  `generated recommendation -> human resolution -> execution projection -> prompt guidance -> human authoring burden visible`.

## Not Authorized

- Do not invent adherence observations before concrete execution-result evidence exists.
- Do not broaden architecture conflict detection into fuzzy contradiction detection.
