# Decisions

## Newly Authorized

- M3 is progressing correctly but is not ready to close.
- Continue with M3 hardening before starting M4 tradeoff analysis.
- Persist generation diagnostics before tradeoff analysis begins.
- Add option validation before M4 so tradeoff analysis does not operate on invalid, duplicate, non-actionable, or evidence-unrelated options.
- Add option deduplication using normalized title, option type, and semantic evidence overlap.
- Add `DecisionOptionRelationship` infrastructure for option conflicts and dependencies before M4 invents a parallel representation.
- Keep `IOptionGenerationService` isolated from recommendation logic; recommendation derivation remains M5 ownership.
- Preserve the additive, backward-compatible `DecisionOption` metadata approach.
- Treat explicit conflict, contradiction, and constraint signals as stronger than broad architectural classification when choosing option-generation patterns.

## Not Authorized

- Do not move into M4 until M3 validation, deduplication, relationships, and diagnostics are in place.
- Do not add recommendation logic, package versioning, model-backed generation, lifecycle redesign, quality dashboards, or certification work in the next slice.
