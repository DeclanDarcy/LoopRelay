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

- [ ] Architecture remains present.
- [ ] Constraints remain present.
- [ ] Stable decisions remain present.
- [ ] Decision rationale remains present.
- [ ] Unresolved questions remain visible.
- [ ] Resolved questions compress appropriately.
- [ ] Active risks remain visible.
- [ ] Retired risks do not accumulate indefinitely.
- [ ] Context size remains bounded relative to inputs.
- [ ] Semantic changes correspond to input changes.
- [ ] Restart and service recreation preserve proposals, reviews, current context, and history.

## Context Reconstruction Test

Verify a reviewer can reconstruct project mental model from:

```text
Plan
Current Milestone
Operational Context
```

without reading handoff archives, decision archives, execution events, or session history.

## Drift Detection Test

Verify diagnostics flag understanding changes that have no corresponding input evidence:

- [ ] Constraint disappears without decision or context evidence.
- [ ] Architecture changes without handoff or decision evidence.
- [ ] Open question disappears without resolution evidence.
- [ ] Decision rationale disappears while decision remains.

## Workspace Certification

Verify the workspace remains usable after multiple revisions:

- [ ] Current understanding remains concise.
- [ ] Open questions are visible.
- [ ] Stable decisions are visible.
- [ ] Active risks are visible.
- [ ] Recent changes are visible.
- [ ] Dashboard summary remains scannable.

## Certification Exit

Long-horizon continuity is certified when repeated cycles preserve understanding and avoid:

- [ ] Knowledge erosion.
- [ ] Historical accretion.
- [ ] Decision amnesia.
- [ ] Open question loss.
- [ ] Understanding drift.
