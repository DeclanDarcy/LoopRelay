# Decisions

## Newly Authorized

- Accept the Milestone 8 backend quality-semantics slice as the intended prerequisite before opening API surface.
- Preserve `DecisionQualitySignal` as the primary explainability abstraction; keep scores as secondary aggregation.
- Treat the current backend quality model as sufficiently complete to expose through stable backend API contracts.
- Proceed next with backend quality assessment, report, and trend endpoints.
- Include API behavior tests for `200`, `400`, `404`, and `409` outcomes where applicable.
- Keep returned quality payloads signal-first, with recommendation stability, tradeoff quality, context quality, constraint quality, and human authoring burden visible as first-class response content.
- Continue sequencing Milestone 8 as backend endpoints before Tauri commands, React hooks, dashboards, or UI reporting surfaces.

## Not Authorized

- Do not make overall score the primary endpoint response shape.
- Do not introduce Tauri wiring, React hooks, dashboards, or UI reporting surfaces before backend endpoint contracts are stable.
- Do not let quality results become governance authority; quality remains observational.
