# Decisions

## Newly Authorized

- Pause after Milestone 9 behavior completion for a focused architectural review before starting Milestone 10 certification.
- Treat the final explainability pieces as accepted:
  - influence trace is evidence lineage.
  - health is named dimensions.
  - health has no score.
  - health is not an opinion system.
- Do not add new behavior during the review unless the review finds a concrete gap.
- Keep push-skip completion incomplete unless explicit Execution-owned or Git-owned push-skip evidence exists.
- Workflow must not infer push-skip authority.
- Architectural review must confirm every canonical continuation transition is covered.
- Architectural review must confirm every authority gate halts continuation.
- Architectural review must confirm persisted timeline, continuation history, preparation history, and completed state remain reconstructable from domain evidence.
- Architectural review must confirm hosted continuation:
  - is disabled by default.
  - reuses endpoint services.
  - cannot duplicate events.
  - cannot cross gates.
- Architectural review must confirm Workflow preparation only creates reviewable artifacts:
  - decision candidates.
  - decision proposals.
  - operational-context proposals.
  - commit-preparation evidence.
- Architectural review must confirm Workflow preparation never resolves, reviews, promotes, commits, pushes, or selects work.
