# Milestone 5: Materialization and Primitive Review

Goal: decide whether any analytical category, including threads, needs first-class persistence based on actual reconstruction limits.

## Backend Work

- [x] Implement `IReasoningMaterializationReviewService`.
- [x] Analyze event and query usage for repeated patterns that derived reconstruction cannot handle cleanly.
- [x] Produce a materialization review report with one recommendation per concept:
  - [x] Hypothesis.
  - [x] Alternative.
  - [x] Contradiction.
  - [x] Direction.
  - [x] Thread.
- [x] For each concept, recommend one outcome:
  - [x] Remain derived.
  - [x] Add derived cache.
  - [x] Add read-model report.
  - [x] Promote to first-class entity.
  - [x] Reject concept.
- [x] Require concrete evidence for promotion, including failed reconstruction scenarios or workflow friction.
- [x] Keep direction derived unless repeated reconstruction proves it is a stable abstraction.
- [x] Review whether persisted thread identity is still justified or whether thread-like grouping should become a derived graph cluster.
- [x] Review whether event family/type growth is creating implicit state machines that should be collapsed, renamed, or kept explicitly derived.

## UI Work

- [x] Add `ReasoningMaterializationReviewPanel`.
- [x] Show each concept's current status, reconstruction evidence, recommendation, and risk.

## Tests

- [x] Review recommends "remain derived" when scenarios are reconstructable from events.
- [x] Review flags a concept for possible materialization only when supplied fixtures demonstrate repeated reconstruction failure or excessive workflow duplication.
- [x] Review never promotes direction solely because direction events exist.
- [x] Review evaluates whether threads should remain persisted, become derived graph clusters, or be restricted to reports.
- [x] Review flags event-family growth that resembles an unapproved lifecycle state machine.
- [x] Review report is advisory and does not create new artifact families.

## Exit Criteria

- [x] Materialization decision point exists.
- [x] Specialized entity persistence remains blocked without evidence.
- [x] Thread persistence is explicitly reaffirmed or demoted.
- [x] Event families remain classification vocabulary, not hidden entity lifecycles.
- [x] Direction materialization is explicitly deferred unless justified.
