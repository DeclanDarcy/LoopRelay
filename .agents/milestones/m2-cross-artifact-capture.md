# Milestone 2: Cross-Decision and Cross-Artifact Capture

Goal: use the event substrate to preserve reasoning evolution across decisions and artifacts without adding new state machines.

## Backend Work

- [x] Add explicit commands for recording decision evolution events:
  - [x] Decision superseded.
  - [x] Decision reframed.
  - [x] Decision reconsidered.
  - [x] Assumption invalidated.
  - [x] Constraint changed.
- [x] Add explicit commands for recording hypothesis, alternative, contradiction, and direction events as event classifications.
- [x] Add reference helpers for decisions, proposals, candidates, governance findings, operational-context revisions, handoffs, execution outputs, and artifacts.
- [x] Add event templates for common reasoning captures with required provenance fields.
- [x] Add assisted-capture adapters that pre-populate references and provenance after successful decision operations, starting with supersession. Keep the decision operation authoritative.
- [ ] Add inferred-capture adapters for objective domain transitions once idempotency is stable:
  - [x] Decision superseded.
  - [x] Proposal resolved.
  - [x] Decision archived.
  - [x] Governance report generated with contradiction findings.
  - [x] Operational-context proposal promoted.
  - [x] Execution handoff accepted or rejected.
- [x] Ensure inferred capture is idempotent by fingerprinting the source transition and refusing duplicate events for the same source transition.
- [x] Keep manual capture available for narrative details that cannot be inferred from source artifacts.
- [x] Add workspace projection summary counts by event family and recent thread activity.

## UI Work

- [ ] Add event creation forms scoped to current repository.
- [ ] Add "record reasoning" affordances near decision supersession, proposal review, governance findings, and operational-context revisions where the backend supports a reference.
- [ ] Show event family filters for hypothesis, alternative, contradiction, direction, decision evolution, assumption evolution, and constraint evolution.

## Tests

- [x] Decision A superseded by Decision B can explain why through events and relationships.
- [x] Decision supersession can create an inferred event from the source transition without a second human action once the adapter is enabled.
- [x] Inferred capture does not duplicate events when the same source transition is processed twice.
- [x] Alternative considered, rejected, and revisited is preserved as an event thread.
- [x] Contradiction discovered and resolved is preserved as an event thread.
- [x] Direction shift is recorded as an event and remains non-authoritative.
- [x] Existing decision and proposal state is not mutated by reasoning capture for proposal-resolution capture.
- [ ] Existing governance, operational-context, and execution state is not mutated by reasoning capture.
  - [x] Current governance report reads do not capture reasoning events.
  - [x] Failed operational-context promotion does not capture reasoning events.
  - [x] Failed execution handoff acceptance does not capture reasoning events.

## Exit Criteria

- [ ] Cross-decision evolution is preservable through events.
- [x] Hypothesis, alternative, contradiction, and direction history can be captured as event families.
- [ ] Observable source-domain transitions have an assisted or inferred capture path.
- [x] Reasoning capture remains append-only and non-authoritative.
