# Decisions

## Newly Authorized

- Continue Milestone 10 recovery certification with these next targets, in order:
  - corrupted continuation history.
  - corrupted preparation history.
  - completed restart idempotency.
- Corrupted continuation-history certification should prove:
  - valid domain workflow state survives corrupted continuation history.
  - recovery is required.
  - current stage remains correct.
  - continuation history corruption does not create duplicate progression.
- Corrupted preparation-history certification should prove:
  - duplicate detection still relies on domain evidence.
  - corrupted preparation history does not cause duplicate preparation events or duplicate review artifact creation.
- Completed restart idempotency certification should prove:
  - a completed workflow remains `Completed`.
  - the `WorkSelection` gate remains open.
  - hosted or endpoint continuation does not advance the terminal completed workflow again.
- Add explicit certification coverage for disposable derived workflow evidence, either as a `DerivedEvidence` finding category or folded into `Recovery`.
- Derived evidence deletion certification should delete workflow timeline, continuation history, and preparation history evidence, then prove stage, gate, eligibility, and health remain correct.
- Recovery certification should be substantially completed before moving into continuation certification, preparation certification, and full end-to-end validation.
