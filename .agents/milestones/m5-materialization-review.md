# Milestone 5: Materialization and Primitive Review

Goal: decide whether any analytical category, including threads, needs first-class persistence based on actual reconstruction limits.

## Backend Work

- [ ] Implement `IReasoningMaterializationReviewService`.
- [ ] Analyze event and query usage for repeated patterns that derived reconstruction cannot handle cleanly.
- [ ] Produce a materialization review report with one recommendation per concept:
  - [ ] Hypothesis.
  - [ ] Alternative.
  - [ ] Contradiction.
  - [ ] Direction.
  - [ ] Thread.
- [ ] For each concept, recommend one outcome:
  - [ ] Remain derived.
  - [ ] Add derived cache.
  - [ ] Add read-model report.
  - [ ] Promote to first-class entity.
  - [ ] Reject concept.
- [ ] Require concrete evidence for promotion, including failed reconstruction scenarios or workflow friction.
- [ ] Keep direction derived unless repeated reconstruction proves it is a stable abstraction.
- [ ] Review whether persisted thread identity is still justified or whether thread-like grouping should become a derived graph cluster.
- [ ] Review whether event family/type growth is creating implicit state machines that should be collapsed, renamed, or kept explicitly derived.

## UI Work

- [ ] Add `ReasoningMaterializationReviewPanel`.
- [ ] Show each concept's current status, reconstruction evidence, recommendation, and risk.

## Tests

- [ ] Review recommends "remain derived" when scenarios are reconstructable from events.
- [ ] Review flags a concept for possible materialization only when supplied fixtures demonstrate repeated reconstruction failure or excessive workflow duplication.
- [ ] Review never promotes direction solely because direction events exist.
- [ ] Review evaluates whether threads should remain persisted, become derived graph clusters, or be restricted to reports.
- [ ] Review flags event-family growth that resembles an unapproved lifecycle state machine.
- [ ] Review report is advisory and does not create new artifact families.

## Exit Criteria

- [ ] Materialization decision point exists.
- [ ] Specialized entity persistence remains blocked without evidence.
- [ ] Thread persistence is explicitly reaffirmed or demoted.
- [ ] Event families remain classification vocabulary, not hidden entity lifecycles.
- [ ] Direction materialization is explicitly deferred unless justified.
