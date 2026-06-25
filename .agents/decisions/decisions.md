# Decisions

## Newly Authorized

- Accept the completed Milestone 5 recovery/monitoring transparency vertical slice as aligned with the roadmap and the prompt-manifest architectural pattern.
- Continue preserving detailed execution transparency as opt-in inspection resources rather than inflating navigation/read-model summaries.
- Preserve nullable recovery evidence for unknown facts; do not let React convert missing evidence into false semantic claims.
- Keep recovery and monitoring interpretation backend-owned; React renders the backend projection only.
- Proceed with the next Milestone 5 slice: push retry transparency.
- Keep `ExecutionSessionService.PushAsync` as the execution authority.
- Persist retry state before returning any push failure response.
- On push failure, return structured state that lets the UI immediately render previous push failure, attempted timestamp, retry eligibility, retry diagnostics, and updated execution transparency.
- Stage, commit, and push this execution session after the push retry transparency slice is complete.
