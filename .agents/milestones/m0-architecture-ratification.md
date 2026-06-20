# Milestone M0 - Operational Context Architecture Ratification

## Objective

Ratify operational context as the current project understanding artifact and document its authority boundaries before implementation work mutates behavior.

## Deliverables

- [ ] Update `docs/architecture.md` with an operational-context section.
- [ ] Add `docs/operational-context-schema.md` as the implementation contract for `OperationalContextDocument`.
- [ ] Define operational-context ontology:
  - [ ] Current mental model.
  - [ ] Architecture.
  - [ ] Authority boundaries.
  - [ ] Constraints.
  - [ ] Stable decisions.
  - [ ] Decision rationale.
  - [ ] Open questions.
  - [ ] Active risks.
- [ ] Define explicit exclusions:
  - [ ] Raw history.
  - [ ] Execution streams.
  - [ ] Conversation logs.
  - [ ] Complete handoff archives.
  - [ ] Git commit history.
  - [ ] Milestone status tracking.
- [ ] Document artifact responsibility boundaries for plan, milestones, handoff, decisions, and operational context.
- [ ] Document the future execution-context consumption contract:

```text
Plan
Selected Milestone
Operational Context
Current Handoff
Current Decisions
Git Snapshot
```

- [ ] Document schema expectations:
  - [ ] Canonical sections.
  - [ ] Allowed item kinds.
  - [ ] Parser fallback behavior.
  - [ ] Renderer behavior.
  - [ ] Projection mapping.
  - [ ] Coarse diff categories.
  - [ ] Compression tiers.
  - [ ] Decision-assimilation hooks.

## Implementation Notes

- [ ] No runtime workflow changes.
- [ ] Implementation may add inert schema/model types and parser tests only if needed to certify the schema contract.
- [ ] No UI workflow changes.
- [ ] No proposal generation.
- [ ] No lifecycle mutation.

## Certification

Verify the architecture document clearly answers:

- [ ] What operational context is.
- [ ] What belongs in it.
- [ ] What does not belong in it.
- [ ] How it differs from plan, milestones, handoff, and decisions.
- [ ] How it will participate in execution without replacing existing inputs.
- [ ] How Markdown maps to `OperationalContextDocument`.
- [ ] What coarse semantic changes are supported initially.
