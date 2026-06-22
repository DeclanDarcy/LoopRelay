# Decisions

## Newly Authorized

- Proceed with an M0 closure audit before any additional extraction work.
- Do not begin Workstream 0.5 until the audit answers whether remaining responsibilities are intentionally owned.
- Treat another extraction slice as lower leverage than authority-boundary closure at this point.
- Focus the M0 closure audit on authority leaks, misplaced responsibilities, and intentional ownership of remaining `App.tsx` responsibilities.
- Do not use code size, hook count, or `App.tsx` line count as closure-audit success criteria.
- Audit operational-context proposal boundaries as:
  - Proposal loading is projection.
  - Proposal generation is workflow action.
  - Proposal editing is draft state.
  - Proposal review is workflow authority.
  - Proposal promotion is workflow authority.
- Leave operational-context proposal loading in its current location if extraction would not improve authority clarity.
- Treat commit preparation as likely intentional `App.tsx` ownership for M0 because it intersects selection state, draft state, workflow review, and readiness evaluation.
- Review remaining refresh paths to confirm they are mutation followed by immediate reconciliation, not projection ownership leaks.
- Produce an M0 closure authority matrix with responsibility, authority, certification state, and deferral state.
- Consider M0 effectively complete with documented deferrals if the audit confirms remaining responsibilities are intentionally owned.

## Expected Audit Outcomes

- Operational Context Proposal: likely deferred.
- Commit Preparation: likely deferred.
- Workflow Gating: likely deferred.
- Workflow Actions: likely deferred.
