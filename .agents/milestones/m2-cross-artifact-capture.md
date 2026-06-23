# Milestone 2: Cross-Decision and Cross-Artifact Capture

Goal: use the event substrate to preserve reasoning evolution across decisions and artifacts without adding new state machines.

## Backend Work

- [ ] Add explicit commands for recording decision evolution events:
  - [ ] Decision superseded.
  - [ ] Decision reframed.
  - [ ] Decision reconsidered.
  - [ ] Assumption invalidated.
  - [ ] Constraint changed.
- [ ] Add explicit commands for recording hypothesis, alternative, contradiction, and direction events as event classifications.
- [ ] Add reference helpers for decisions, proposals, candidates, governance findings, operational-context revisions, handoffs, execution outputs, and artifacts.
- [ ] Add event templates for common reasoning captures with required provenance fields.
- [x] Add assisted-capture adapters that pre-populate references and provenance after successful decision operations, starting with supersession. Keep the decision operation authoritative.
- [ ] Add inferred-capture adapters for objective domain transitions once idempotency is stable:
  - [x] Decision superseded.
  - [x] Proposal resolved.
  - [ ] Decision archived.
  - [ ] Governance report generated with contradiction findings.
  - [ ] Operational-context proposal promoted.
  - [ ] Execution handoff accepted or rejected.
- [x] Ensure inferred capture is idempotent by fingerprinting the source transition and refusing duplicate events for the same source transition.
- [ ] Keep manual capture available for narrative details that cannot be inferred from source artifacts.
- [ ] Add workspace projection summary counts by event family and recent thread activity.

## UI Work

- [ ] Add event creation forms scoped to current repository.
- [ ] Add "record reasoning" affordances near decision supersession, proposal review, governance findings, and operational-context revisions where the backend supports a reference.
- [ ] Show event family filters for hypothesis, alternative, contradiction, direction, decision evolution, assumption evolution, and constraint evolution.

## Tests

- [x] Decision A superseded by Decision B can explain why through events and relationships.
- [x] Decision supersession can create an inferred event from the source transition without a second human action once the adapter is enabled.
- [x] Inferred capture does not duplicate events when the same source transition is processed twice.
- [ ] Alternative considered, rejected, and revisited is preserved as an event thread.
- [ ] Contradiction discovered and resolved is preserved as an event thread.
- [ ] Direction shift is recorded as an event and remains non-authoritative.
- [x] Existing decision and proposal state is not mutated by reasoning capture for proposal-resolution capture.
- [ ] Existing governance, operational-context, and execution state is not mutated by reasoning capture.

## Exit Criteria

- [ ] Cross-decision evolution is preservable through events.
- [ ] Hypothesis, alternative, contradiction, and direction history can be captured as event families.
- [ ] Observable source-domain transitions have an assisted or inferred capture path.
- [ ] Reasoning capture remains append-only and non-authoritative.
