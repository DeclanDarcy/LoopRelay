# Decisions

## Newly Authorized

- Treat the Milestone 9 execution influence UI slice as accepted and complete.
- Preserve the historical influence model:
  `Resolved Decision -> Projection Fingerprint -> Execution Session -> Persisted Influence Trace -> Execution Inspector`.
- Continue Milestone 9 with projection correctness hardening before closing the milestone.
- Implement the next slice as:
  - mutually exclusive architecture-rule conflict detection
  - superseded-authority-still-projecting diagnostics
- Land projection correctness hardening before broader execution UI, analytics, or observability work.

## Not Authorized

- Do not recompute historical execution influence from current decisions.
- Do not add adherence observations without concrete execution result evidence.
- Do not expand the execution influence surface into execution analytics.
- Do not add adherence scoring or recommendation metrics in the next slice.
