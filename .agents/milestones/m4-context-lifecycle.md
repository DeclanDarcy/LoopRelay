# Milestone M4 - Operational Context Lifecycle

## Objective

Promote accepted proposed understanding into authoritative current understanding and preserve prior understanding revisions.

## Backend Changes

- [ ] Extend `ArtifactService` to discover historical operational-context files:

```text
.agents/operational_context.0001.md
.agents/operational_context.0002.md
```

- [ ] Extend `ArtifactInventory` with `HistoricalOperationalContexts`.
- [ ] Extend `ArtifactRotationService`:
  - [ ] Add `RotateCurrentOperationalContextAsync`.
  - [ ] Support `ArtifactFamily.OperationalContext`.
  - [ ] Current path: `.agents/operational_context.md`.
  - [ ] Historical directory: `.agents`.
  - [ ] Historical base name: `operational_context`.
- [ ] Add `IOperationalContextLifecycleService`.
- [ ] Add `PromoteOperationalContextAsync`.
- [ ] Promotion preconditions:
  - [ ] Proposal exists.
  - [ ] Proposal is accepted.
  - [ ] Proposal is latest or not superseded.
  - [ ] Proposal accepted content hash matches stored accepted content.
  - [ ] Current operational-context hash matches review baseline.
  - [ ] Rejected proposals cannot promote.
- [ ] Promotion sequence:
  - [ ] If current context exists, archive it first to the next `.agents/operational_context.NNNN.md`.
  - [ ] If archive fails, block promotion and leave current context unchanged.
  - [ ] Write accepted content to `.agents/operational_context.md`.
  - [ ] Record promotion timestamp, proposal id, revision number, and archived path in proposal metadata.
  - [ ] Refresh repository projections.
- [ ] Bootstrap sequence:
  - [ ] If no current context exists, write accepted content as first `.agents/operational_context.md`.
  - [ ] No historical revision is created for bootstrap.

## Failure Handling

- [ ] Archive failure blocks write.
- [ ] Write failure leaves current context unchanged; any copied archive remains valid as a historical duplicate of the still-current context and must be reported in promotion metadata.
- [ ] Stale proposal blocks promotion.
- [ ] Missing accepted review blocks promotion.

## UI Changes

- [ ] Add `Promote` action only for accepted, non-stale proposals.
- [ ] Show current revision count.
- [ ] Show last promotion timestamp.
- [ ] Show archived prior version path when present.
- [ ] Show understanding history available as a count, not a full history browser.

## Tests

Add backend tests:

- [ ] Bootstrap promotion creates `.agents/operational_context.md`.
- [ ] Revision promotion archives prior current context before replacement.
- [ ] Historical numbering uses highest existing number plus one.
- [ ] Promotion rejects rejected, pending, superseded, or stale proposals.
- [ ] Archive failure blocks promotion.
- [ ] Write failure does not erase current context.
- [ ] Artifact inventory includes current and historical operational-context revisions.
- [ ] Workspace projection updates after promotion.
- [ ] Promotion state survives service recreation.

## Certification

Lifecycle is certified when accepted understanding can become authoritative, prior understanding is preserved, numbering is correct, stale promotion is blocked, and repository projections reflect the lifecycle state.
