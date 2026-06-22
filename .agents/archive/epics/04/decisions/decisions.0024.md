# Decisions

## Newly Authorized

- Continue Milestone 0.5 with the `backend projection -> render` extraction boundary.
- Treat the `ExecutionContextArtifactList` extraction as correctly scoped because it renders authoritative backend values only: `role`, `relativePath`, and `byteCount`.
- Continue avoiding frontend interpretation of readiness, priority, validation, size-limit meaning, recommendations, importance, requiredness, context sufficiency, relevance, severity, and blocking.
- Audit `ExecutionContextMissingOptionalList` next.
- Extract the missing optional list only if the current implementation is limited to backend-provided paths rendered directly plus the existing empty-state text `None`.
- Do not extract the missing optional list if it answers whether an artifact should be included, whether an omission is important or risky, or whether execution should be blocked.
- Keep validation, artifact diagnostics, and artifact content previews under stricter authority review before any extraction.
- After the missing optional list candidate, consider a quick Milestone 0.5 inventory before continuing because the remaining candidates may be less pure and the marginal value of extraction may be diminishing.

## Next Authorized Slice

Audit missing optional list rendering. Extract it only if it remains `paths -> render` plus `empty -> "None"`.
