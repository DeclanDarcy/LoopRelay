# Decisions

## Newly Authorized

- Treat `ExecutionContextValidationList` as the next defensible Milestone 0.5 extraction candidate only if it remains a pure presentation component.
- Limit `ExecutionContextValidationList` to `string[] -> ul/li` rendering and the existing empty fallback text `No validation errors`.
- Preserve backend-provided validation message ordering.
- Render validation message text verbatim.
- Do not add warning counts, grouping, prioritization, severity assignment, derived status, readiness calculation, blocking counts, critical counts, launch impact, or count badges during validation-list extraction.
- If validation-list extraction begins to introduce severity, grouping, launch impact, readiness, or other interpretation, stop and re-audit.
- Treat artifact diagnostics as authority-adjacent because threshold labels are semantically close to launch readiness, execution gating, severity, and blocking.
- Treat M0.5 success as intentional remaining `App.tsx` responsibility, not maximum file shrinkage.
- Use this stop condition for M0.5: no remaining extraction candidates that satisfy `props -> render` without adding meaning.
- After extracting `ExecutionContextValidationList`, perform another inventory pass before assuming there is another meaningful M0.5 extraction candidate.

## Next Authorized Slice

Extract `ExecutionContextValidationList` with characterization coverage for empty state, backend ordering, and verbatim message rendering; then inventory remaining candidates again before continuing decomposition.
