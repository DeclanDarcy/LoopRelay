# Decisions

## Newly Authorized

- Treat the first M7 slice as correct because it certified what was tested: `Repository Truth -> Recovery -> Equivalent Reconstruction`.
- Define the M7 objective as proving reconstruction can survive time, not merely proving reconstruction can work.
- Treat M7 as validation of durability of meaning: the system must still answer later.
- Continue treating `Materialization pressure != Materialization authorization` as the key architectural invariant.
- In survivability validation, continue proving derived reasoning remains sufficient before permitting any additional persisted artifacts.
- Do not add caches, specialized read models, or entity directories as a result of validation pressure alone.
- Move the next M7 layer from graph-equivalence infrastructure tests to answer-level certification tests.
- Certify architecture choice reconstruction after recovery: why approach B was selected over approach A.
- Certify rejected-alternative reconstruction after recovery without introducing Alternative entities.
- Certify failed-assumption reconstruction after recovery using assumption classifications, contradicting evidence, and later traces.
- Prioritize contradiction-that-changed-direction reconstruction as the highest-value next answer-level test.
- Certify contradiction and direction answers without materializing Direction or Contradiction entities.
- Consider M7 complete when the system reliably answers these questions after persistence, recovery, restart, and reconstruction:
  - Why did we choose this?
  - Why did we stop doing that?
  - What assumption failed?
  - What contradiction mattered?
  - What changed our thinking?
- Keep events, relationships, references, threads, and provenance as the only authoritative reasoning inputs for M7 completion.
- Resist specialized reconstruction engines unless answer-level tests demonstrate that the generic `Graph -> Trace -> Reconstruction` pipeline cannot answer the required question.
- Improve reconstruction output only when a real usability gap is demonstrated.
- Keep current program status as M0-M6 complete and M7 in progress.
