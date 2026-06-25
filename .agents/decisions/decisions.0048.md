# Decisions

## Newly Authorized

- Continue Milestone 6 with grouped reasoning diagnostics next.
- Grouped reasoning diagnostics must remain backend-owned; the UI must not decide which diagnostics belong together.
- Reasoning diagnostic categories should include evidence, confidence, materialization, reconstruction, capture, authority boundary, lifecycle risk, and validation.
- The UI should render grouped diagnostics verbatim from backend projections.
- The grouped diagnostics model should be stable enough for Milestone 8 explainability adapters to consume directly.
- Prefer a model shaped like `DiagnosticGroup` with category, optional title, and diagnostics collection, while preserving backend authority over semantic organization.
