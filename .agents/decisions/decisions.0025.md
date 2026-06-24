# Decisions

## Newly Authorized

- Proceed with Milestone 10 recovery certification next.
- Treat recovery certification as the second-most important certification category after authority certification.
- Start recovery certification with the completed-vs-commit divergence scenario:
  - persisted workflow timeline says `Completed`.
  - domain projection says `Commit`.
  - domain projection wins.
  - workflow evidence is rebuilt.
  - a certification finding is recorded.
  - persisted `Completed` evidence must never win over domain truth.
- Add recovery certification for corrupted timeline evidence:
  - recovery rebuilds projection from domain truth.
  - recovery does not duplicate progression.
  - recovery does not duplicate timeline entries.
- Add recovery certification for corrupted continuation history:
  - current state is preserved.
  - restart/recovery does not create duplicate continuation events.
- Add recovery certification for corrupted preparation history:
  - duplicate detection still works.
  - restart/recovery does not create duplicate review artifacts.
- Add completed-restart recovery certification:
  - completed workflow recovers as `Completed` with a `WorkSelection` gate.
  - completed workflow does not advance again after restart.
- Add explicit derived-evidence certification, either as a separate `DerivedEvidence` category or folded into `Recovery`.
- Derived-evidence certification must prove deleting workflow timeline, continuation history, and preparation history does not change:
  - current stage.
  - current gates.
  - current eligibility.
  - current health.
- Continue focusing Milestone 10 on proving properties rather than adding new workflow authority or capability.
