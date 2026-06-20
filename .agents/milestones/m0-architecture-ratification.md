# Milestone M0 - Operational Context Architecture Ratification

## Objective

Ratify operational context as the current project understanding artifact and document its authority boundaries before implementation work mutates behavior.

## Deliverables

- [x] Update `docs/architecture.md` with an operational-context section.
- [x] Add `docs/operational-context-schema.md` as the implementation contract for `OperationalContextDocument`.
- [x] Define operational-context ontology:
  - [x] Current mental model.
  - [x] Architecture.
  - [x] Authority boundaries.
  - [x] Constraints.
  - [x] Stable decisions.
  - [x] Decision rationale.
  - [x] Open questions.
  - [x] Active risks.
- [x] Define explicit exclusions:
  - [x] Raw history.
  - [x] Execution streams.
  - [x] Conversation logs.
  - [x] Complete handoff archives.
  - [x] Git commit history.
  - [x] Milestone status tracking.
- [x] Document artifact responsibility boundaries for plan, milestones, handoff, decisions, and operational context.
- [x] Document the future execution-context consumption contract:

```text
Plan
Selected Milestone
Operational Context
Current Handoff
Current Decisions
Git Snapshot
```

- [x] Document schema expectations:
  - [x] Canonical sections.
  - [x] Allowed item kinds.
  - [x] Parser fallback behavior.
  - [x] Renderer behavior.
  - [x] Projection mapping.
  - [x] Coarse diff categories.
  - [x] Compression tiers.
  - [x] Decision-assimilation hooks.

## Implementation Notes

- [x] No runtime workflow changes.
- [x] Implementation may add inert schema/model types and parser tests only if needed to certify the schema contract.
- [x] No UI workflow changes.
- [x] No proposal generation.
- [x] No lifecycle mutation.

## Certification

Verify the architecture document clearly answers:

- [x] What operational context is.
- [x] What belongs in it.
- [x] What does not belong in it.
- [x] How it differs from plan, milestones, handoff, and decisions.
- [x] How it will participate in execution without replacing existing inputs.
- [x] How Markdown maps to `OperationalContextDocument`.
- [x] What coarse semantic changes are supported initially.
