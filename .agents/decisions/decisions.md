# Decisions

## Newly Authorized

- Continue Milestone 10 with recovery hardening before moving into continuation or preparation certification categories.
- Prioritize the next recovery certification scenarios in this order:
  - corrupted timeline evidence.
  - corrupted continuation history.
  - corrupted preparation history.
  - completed restart idempotency.
- Certification must remain an observer, not a repair mechanism:
  - it may answer whether recovery is required.
  - it may answer whether recovery would succeed.
  - it may record whether invariants are violated.
  - it must not perform recovery mutation as part of certification.
- Corrupted timeline certification must prove:
  - persisted workflow timeline evidence can be corrupted.
  - certification records that recovery is required.
  - domain projection remains correct.
  - certification does not trust corrupted timeline state.
- Corrupted continuation-history certification must prove restart/recovery does not create duplicate continuation events or duplicate progression.
- Corrupted preparation-history certification must prove restart/preparation evaluation does not create duplicate candidates, proposals, or commit preparations because domain evidence detection still prevents duplication.
- Completed restart idempotency certification must prove a completed workflow recovers as `Completed` with a `WorkSelection` gate and does not advance again.
- Add an explicit workflow-evidence deletion certification scenario:
  - delete timeline, continuation history, and preparation history evidence.
  - projection, recovery, and certification must still produce correct stage, gate, eligibility, and health.
- Treat workflow-evidence deletion as a direct proof that workflow evidence is derived and disposable.
