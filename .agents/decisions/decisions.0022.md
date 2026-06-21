# Decisions

## Newly Authorized

- Treat the `ExecutionHistoryPanel` extraction as architecturally safe because `selectedExecutionHistory` was already derived upstream and the component owns only `sessions -> render`.
- Preserve the boundary that feature components must not decide which sessions matter, which sessions are selected, which sessions are current, or which sessions are relevant.
- Classify remaining large `App.tsx` regions as either workflow authority or read-only summary surfaces before extracting them.
- Keep workflow authority in `App.tsx`, including execution start, commit preparation, commit readiness, push readiness, proposal review, promotion review, and handoff review.
- Treat read-only diagnostic summaries, context summaries, metadata cards, and status rows as likely Workstream 0.5 extraction candidates only after an authority audit.
- Prefer execution context summary rows as the next extraction candidate because they are more likely than launch diagnostics to satisfy `props -> render` without hidden interpretation.
- Before extracting execution context summary rows, verify the component does not answer authority questions such as whether execution can start, context is sufficient, execution should proceed, or readiness is blocked.
- Be more cautious with launch diagnostics because severity, importance, blocking, and recommendations may hide frontend interpretation unless those meanings are backend-provided.
- Continue the Milestone 0.5 loop as audit, characterize, classify, extract, and verify.
- Treat every extraction candidate as authority-sensitive until proven to be presentation-only.

## Next Authorized Slice

Audit execution context summary rows first. Extract them only if they simply display backend-provided context items, values, metadata, and descriptions from props. Audit launch diagnostics afterward.
