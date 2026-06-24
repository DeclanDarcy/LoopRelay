# Decisions

## Newly Authorized

- Treat Milestone 10 as implementation-complete.
- Do not add more workflow behavioral capability before milestone closure.
- Move the workflow capability from implementation to validation and closure.
- Keep `PushSkipped` deferred until domain-owned evidence exists.
- Before closure, run:
  - full backend test suite.
  - full solution build.
  - remaining UI and shell validation required by the roadmap.
- Before formal program closure, verify the high-value recovery/certification scenario remains covered and passing:
  - persisted timeline is `Completed`.
  - domain projection is `Commit`.
  - domain projection wins.
  - recovery is required.
  - certification finding is produced.
- Final closeout should review `m10-certification.md` and `handoff.md` for accuracy against implemented reality.
