# Decisions

## Newly Authorized

- Treat `ExecutionContextMissingOptionalList` as a clean Milestone 0.5 presentation extraction because it renders backend-provided paths in backend order and preserves the existing `None` fallback.
- Treat backend ordering and empty-state stability as the behavioral contracts protected by the missing optional list characterization tests.
- Do a Milestone 0.5 inventory/audit slice before extracting additional execution context preview UI.
- Use the inventory to classify remaining regions by presentation-only status, interpretation risk, workflow risk, and extraction candidacy.
- Audit validation surfaces carefully because validation, warning, error, missing, and required UI can encode severity, importance, or blocking state.
- Audit repository snapshot rendering for `snapshot -> render` versus derived health assessment.
- Treat artifact diagnostics as the highest-risk remaining candidate because diagnostic UI can drift into user importance, continuation, and execution authority.
- Treat artifact content previews as possible extraction candidates only when they are `content -> preview`; keep them out of presentation extraction if they answer sufficiency, completeness, or readiness questions.
- Stop extracting a region when it answers a "should" question or a "can proceed" question.
- Consider Milestone 0.5 near natural completion if the remaining regions prove authority-sensitive or low ROI after inventory.

## Next Authorized Slice

Run a Milestone 0.5 inventory/audit over the remaining execution context preview regions before any further extraction.
