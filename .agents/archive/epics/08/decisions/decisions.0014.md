# Decisions

## Newly Authorized

- Stop further progression implementation for now; the canonical workflow path
  is considered sufficiently covered for this phase:
  - `Execution -> Handoff -> Decision -> OperationalContext -> Commit -> Push -> Completed -> WorkSelection`
  - no-change completion to `Completed -> WorkSelection`
- Proceed next with Milestone 9 recovery and idempotency hardening before any
  preparation work.
- Focus the next slice on:
  - completed recovery from domain evidence without relying on persisted
    workflow stage.
  - timeline/domain divergence where domain projection wins over stale or
    conflicting workflow timelines.
  - continuation idempotency across restart with no duplicate event, timeline,
    or progression for identical fingerprints.
  - no-change completion reconstruction after recovery.
  - work-selection gate recovery after completed workflow restart.
- Preserve the current authority boundary:
  - workflow advances workflow only.
  - workflow does not advance domains.
  - workflow records derived evidence only.
  - domains remain the source of truth.

## Explicitly Deferred

- Do not start preparation services yet.
- Do not add hosted continuation yet.
- Do not introduce domain command invocation from workflow yet.
- Do not add workflow-owned authority satisfaction.
