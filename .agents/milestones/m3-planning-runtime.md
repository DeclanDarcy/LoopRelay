# Phase 3 - Planning Runtime

Goal: make planning a first-class, persistent, repository-scoped runtime conversation.

## Implementation

- [ ] Introduce Planning Session models and persistence:
  - session identity
  - lifecycle
  - conversation history
  - current plan revision
  - planning metadata
  - artifacts consumed and produced
- [ ] Add Planning Session coordination to Repository Runtime using an operational sandbox and high reasoning effort profile.
- [ ] Promote planning inputs into first-class information:
  - Planning Intent
  - Requirements Artifact
  - Plan
  - Plan Revision
  - Planning Session
  - Planning State
- [ ] Preserve existing markdown artifacts and make them durable serialization, not UI-only documents.
- [ ] Implement planning protocols:
  - write initial plan from planning intent and requirements artifacts
  - revise plan from human feedback
  - preserve session context across revisions
  - complete planning only when the user chooses
- [ ] Add planning streams with turn boundaries, replay, cancellation, reconnect, and completion.
- [ ] Add backend planning endpoints through Repository Runtime commands.
- [ ] Add UI planning workspace:
  - requirements artifact editor
  - plan viewer
  - feedback editor
  - live stream
  - revision history
  - readiness and Execute Plan action
- [ ] Add generated contracts for planning lifecycle, session, commands, artifacts, stream events, and plan revisions.

## Certification

- [ ] A repository without an accepted plan enters Plan Authoring.
- [ ] Planning can run indefinitely without triggering execution.
- [ ] Plan revisions occur in the same persistent planning session.
- [ ] Current plan and revision history are durable.
- [ ] UI supports authoring, revision, cancellation, reload, and execution readiness.
