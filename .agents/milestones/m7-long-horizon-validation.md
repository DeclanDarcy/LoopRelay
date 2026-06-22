# Milestone 7: Long-Horizon Reconstruction Validation

Goal: prove event-led reasoning survives large project histories.

## Backend Work

- [ ] Add long-horizon fixture builders for many decisions, many reasoning events, repeated alternatives, recurring contradictions, failed assumptions, and strategic shifts.
- [ ] Implement decision evolution reconstruction for chains, branches, convergence, supersession, and replacement.
- [ ] Implement direction reconstruction as an emergent narrative from events and traces.
- [ ] Implement hypothesis reconstruction for raised, supported, challenged, invalidated, and retired hypothesis events.
- [ ] Implement contradiction reconstruction for identified, investigated, resolved, accepted, and recurring contradiction events.
- [ ] Implement project narrative reconstruction across hypotheses, alternatives, contradictions, direction events, and decisions.
- [ ] Add performance diagnostics for large histories without relying on wall-clock elapsed time as a correctness criterion.

## UI Work

- [ ] Add project-level narrative reconstruction view.
- [ ] Add horizon selector for decision, milestone, epic, project, and multi-year reconstruction.
- [ ] Add source evidence collapse/expand controls for large reconstructions.

## Tests

- [ ] A large fixture can answer why current strategy exists.
- [ ] A large fixture can answer why an architecture was chosen.
- [ ] A large fixture can list rejected alternatives and their rationale.
- [ ] A large fixture can list failed assumptions and their outcomes.
- [ ] A large fixture can identify contradictions that changed direction.
- [ ] Reconstruction remains traceable to events and source evidence.
- [ ] Reconstruction remains usable enough for UI consumption.

## Exit Criteria

- [ ] Decision evolution reconstruction is operational.
- [ ] Direction reconstruction works without first-class direction persistence.
- [ ] Hypothesis reconstruction works without first-class hypothesis persistence unless materialization was approved.
- [ ] Alternative reconstruction works without first-class alternative persistence unless materialization was approved.
- [ ] Contradiction reconstruction works without first-class contradiction persistence unless materialization was approved.
- [ ] Project narrative reconstruction is operational.
