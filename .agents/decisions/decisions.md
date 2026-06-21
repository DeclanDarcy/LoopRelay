# Decisions

## Newly Authorized

- Treat `getExecutionWorkflowSteps` as valid Workstream 0.5 extraction because it maps existing execution state into workflow presentation: step labels, step ordering, display metadata, and presentation state.
- Keep the boundary explicit: workflow presentation mapping may live in `src/lib`; workflow authority must not.
- Do not extract helpers that determine can-execute state, completion authority, health authority, or next workflow action.
- Evaluate `getOperationalContextSectionItems` and `getDecisionContinuityWarnings` independently rather than extracting them as a pair by default.
- Treat `getOperationalContextSectionItems` as a promising next candidate only if it remains projection-to-section/display grouping.
- Scrutinize `getDecisionContinuityWarnings` before extraction because warning helpers can either format backend warnings or derive frontend interpretation.
- Use this litmus test for each helper: if the backend warning/model changed, would the helper still exist? If yes, it is likely presentation; if no, it may be deriving meaning.
- Characterize section ordering, section inclusion, section omission, warning ordering, warning visibility, and empty-state behavior before extracting operational-context helpers.
- Formalize the M0.5 rule: `Input Projection -> Display Mapping -> View` may leave `App.tsx`; `Input Projection -> Meaning Derivation -> View` must not.

## Next Authorized Slice

Classify `getOperationalContextSectionItems` and `getDecisionContinuityWarnings` independently, add characterization for current display output first, and extract only the helper or helpers that are projection-to-display mapping rather than semantic interpretation.
