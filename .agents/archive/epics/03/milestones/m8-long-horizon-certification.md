# Milestone M8 - Long-Horizon Certification

## Objective

Certify that understanding remains coherent, useful, bounded, and reviewable across repeated execution and operational-context update cycles.

## Certification Harness

Add backend certification tests using temporary repositories and fake services:

```text
Cycle 1:
  execution summary
  handoff update
  decision update
  generate proposal
  review
  promote

Cycle 2:
  execution summary
  handoff update
  decision update
  generate proposal
  review
  promote

Cycle 3:
  execution summary
  handoff update
  decision update
  generate proposal
  review
  promote
```

Verify after each cycle:

- [x] Architecture remains present.
- [x] Constraints remain present.
- [x] Stable decisions remain present.
- [x] Decision rationale remains present.
- [x] Unresolved questions remain visible.
- [x] Resolved questions compress appropriately.
- [x] Active risks remain visible.
- [x] Retired risks do not accumulate indefinitely.
- [x] Context size remains bounded relative to inputs.
- [x] Semantic changes correspond to input changes.
- [x] Restart and service recreation preserve proposals, reviews, current context, and history.

## Context Reconstruction Test

Verify a reviewer can reconstruct project mental model from:

```text
Plan
Current Milestone
Operational Context
```

without reading handoff archives, decision archives, execution events, or session history.

- [x] Fresh participant reconstruction is covered from plan, selected milestone, and current operational context.
- [x] Historical handoff, decision, and operational-context archives are not required for reconstruction.

## Drift Detection Test

Verify diagnostics flag understanding changes that have no corresponding input evidence:

- [x] Constraint disappears without decision or context evidence.
- [x] Architecture changes without handoff or decision evidence.
- [x] Open question disappears without resolution evidence.
- [x] Decision rationale disappears while decision remains.

## Workspace Certification

Verify the workspace remains usable after multiple revisions:

- [x] Current understanding remains concise.
- [x] Open questions are visible.
- [x] Stable decisions are visible.
- [x] Active risks are visible.
- [x] Recent changes are visible.
- [x] Dashboard summary remains scannable.

## Certification Exit

Long-horizon continuity is certified when repeated cycles preserve understanding and avoid:

- [x] Knowledge erosion.
- [x] Historical accretion.
- [x] Decision amnesia.
- [x] Open question loss.
- [x] Understanding drift.
