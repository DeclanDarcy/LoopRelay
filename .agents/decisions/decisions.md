# Decisions

## Newly Authorized

- Treat the Milestone 9 backend traceability chain as the correct completed retrieval slice:
  `Resolved Decision -> Projection -> Projection Fingerprint -> Execution Session -> Influence Trace -> Retrieval`.
- Proceed next to a focused execution influence UI surface.
- Implement the UI work in this order:
  `Frontend Types -> Frontend API -> Hooks -> Execution Influence Panel`.
- Keep the first execution influence UI surface narrow.
- For a selected execution session, show:
  - execution session
  - projection fingerprint
  - influencing decisions
  - projected constraints
  - projected directives
  - projected priorities
  - projected architecture rules
- Keep the UI grounded in persisted influence records loaded from execution session influence traces.

## Not Authorized

- Do not recompute execution influence from current decisions for historical execution sessions.
- Do not expand this UI slice into execution analytics.
- Do not expand this UI slice into adherence metrics.
- Do not expand this UI slice into recommendation metrics.
- Do not start broader execution observability before the focused influence surface is stable.
