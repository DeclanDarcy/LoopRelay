# Decisions

## Newly Authorized

- Continue Milestone 0.5 with the audit, characterize, classify, extract, verify loop.
- Treat the `ExecutionContextSummaryRows` extraction as safe because it renders a backend preview projection plus caller-provided display strings without deciding readiness, sufficiency, staleness, warning impact, or launch behavior.
- Keep launch readiness in `App.tsx`.
- Keep stale-preview interpretation in `App.tsx`.
- Keep operational-context inclusion status derivation in `App.tsx`.
- Keep size-status derivation in `App.tsx`.
- Treat launch diagnostics as authority-sensitive until audited because blocking, warning, recommendation, severity, and required-action meanings may be backend-provided or frontend-derived.
- Prioritize artifact list rendering as the next extraction candidate.
- Extract artifact list rendering only if it is genuinely `artifacts -> render`.
- Do not extract artifact list rendering if it includes importance, blocking, requiredness, or recommendation interpretation.
- Treat missing optional list rendering as likely safe only after checking for importance ranking, priority ordering, or recommendation wording.
- Treat validation list rendering as more suspicious because validation UI may contain error categorization, severity derivation, blocking logic, or recommendation generation.
- Continue using the guiding rule: extract presentation, retain meaning.

## Next Authorized Slice

Audit artifact list rendering. Extract it only if it is a direct collection rendering surface with no hidden interpretation.
