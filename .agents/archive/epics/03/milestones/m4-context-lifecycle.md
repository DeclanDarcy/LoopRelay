# Milestone M4 - Operational Context Lifecycle

## Objective

Promote accepted proposed understanding into authoritative current understanding and preserve prior understanding revisions.

## Backend Changes

- [x] Extend `ArtifactService` to discover historical operational-context files:

```text
.agents/operational_context.0001.md
.agents/operational_context.0002.md
```

- [x] Extend `ArtifactInventory` with `HistoricalOperationalContexts`.
- [x] Extend `ArtifactRotationService`:
  - [x] Add `RotateCurrentOperationalContextAsync`.
  - [x] Support `ArtifactFamily.OperationalContext`.
  - [x] Current path: `.agents/operational_context.md`.
  - [x] Historical directory: `.agents`.
  - [x] Historical base name: `operational_context`.
- [x] Add `IOperationalContextLifecycleService`.
- [x] Add `PromoteOperationalContextAsync`.
- [x] Promotion preconditions:
  - [x] Proposal exists.
  - [x] Proposal is accepted.
  - [x] Proposal is latest or not superseded.
  - [x] Proposal accepted content hash matches stored accepted content.
  - [x] Current operational-context hash matches review baseline.
  - [x] Rejected proposals cannot promote.
- [x] Promotion sequence:
  - [x] If current context exists, archive it first to the next `.agents/operational_context.NNNN.md`.
  - [x] If archive fails, block promotion and leave current context unchanged.
  - [x] Write accepted content to `.agents/operational_context.md`.
  - [x] Record promotion timestamp, proposal id, revision number, and archived path in proposal metadata.
  - [x] Refresh repository projections.
- [x] Bootstrap sequence:
  - [x] If no current context exists, write accepted content as first `.agents/operational_context.md`.
  - [x] No historical revision is created for bootstrap.

## Failure Handling

- [x] Archive failure blocks write.
- [x] Write failure leaves current context unchanged; any copied archive remains valid as a historical duplicate of the still-current context and must be reported in promotion metadata.
- [x] Stale proposal blocks promotion.
- [x] Missing accepted review blocks promotion.

## UI Changes

- [x] Add `Promote` action only for accepted, non-stale proposals.
- [x] Show current revision count.
- [x] Show last promotion timestamp.
- [x] Show archived prior version path when present.
- [x] Show understanding history available as a count, not a full history browser.

## Tests

Add backend tests:

- [x] Bootstrap promotion creates `.agents/operational_context.md`.
- [x] Revision promotion archives prior current context before replacement.
- [x] Historical numbering uses highest existing number plus one.
- [x] Promotion rejects rejected, pending, superseded, or stale proposals.
- [x] Archive failure blocks promotion.
- [x] Write failure does not erase current context.
- [x] Artifact inventory includes current and historical operational-context revisions.
- [x] Workspace projection updates after promotion.
- [x] Promotion state survives service recreation.

## Certification

Lifecycle is certified when accepted understanding can become authoritative, prior understanding is preserved, numbering is correct, stale promotion is blocked, and repository projections reflect the lifecycle state.
