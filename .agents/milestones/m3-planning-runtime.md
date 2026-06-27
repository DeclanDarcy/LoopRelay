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
- [ ] Add Planning Session coordination to Repository Runtime using a Planning session role, appropriate sandbox profile, and high reasoning effort profile.
- [ ] Promote planning inputs into first-class information:
  - Planning Intent
  - Requirements Artifact
  - Plan
  - Plan Revision
  - Planning Session
  - Planning State
- [ ] Preserve existing markdown artifacts and make them durable serialization, not UI-only documents.
- [ ] Implement planning protocols:
  - write initial plan from planning intent and requirements artifacts using `WritePlanForNewCodebase`
  - write initial plan against an existing repository using `WritePlanAgainstCodebase`
  - revise plan from human feedback using `RevisePlan`
  - extract milestone files using `ExtractMilestones`
  - preserve session context across revisions
  - complete planning only when the user chooses
- [ ] Preserve the intended plan-generation protocol:
  - planning prompts read `.agents/specs` and roadmap inputs when present
  - generated plans must be self-contained after source planning inputs are removed
  - milestone extraction leaves `plan.md` milestone entries as relative `(See ./milestones/...)` pointers only
  - `.agents/milestones/m*.md` files remain checkbox-trackable execution artifacts
- [ ] Store prompt provenance on each plan revision and milestone extraction:
  - prompt name
  - generated type
  - `SourceHash`
  - planning input artifact identities
  - produced plan or milestone artifact identities
- [ ] Add planning streams with turn boundaries, replay, cancellation, reconnect, and completion.
- [ ] Add backend planning endpoints through Repository Runtime commands.
- [ ] Add UI planning workspace:
  - requirements artifact editor
  - plan viewer
  - feedback editor
  - live stream
  - revision history
  - readiness and Execute Plan action
- [ ] Add generated contracts for planning lifecycle, session, commands, artifacts, prompt provenance, stream events, and plan revisions.

## Certification

- [ ] A repository without an accepted plan enters Plan Authoring.
- [ ] Planning can run indefinitely without triggering execution.
- [ ] Plan revisions occur in the same persistent planning session.
- [ ] Current plan and revision history are durable.
- [ ] Plan and milestone artifacts are generated only through the canonical planning prompts or explicit human edits.
- [ ] Prompt provenance is present for initial plans, revisions, and milestone extraction.
- [ ] UI supports authoring, revision, cancellation, reload, and execution readiness.
