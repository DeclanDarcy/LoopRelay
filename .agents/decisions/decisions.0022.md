# Decisions

## Newly Authorized

- The completed comparison/read-model slice is accepted as preserving the correct Decision Lifecycle sequencing.
- Proposal evolution should remain inspectable before it becomes mutable.
- Backend-owned comparison read models remain the approved boundary:
  - backend builds lifecycle comparisons
  - React renders comparison projections
  - React must not calculate lifecycle deltas
- `REV-NNNN.comparison.md` artifacts are accepted as first-class repository artifacts.
- The governing model remains:
  - proposal is authority
  - revision is history
  - comparison is explanation
- Source-fingerprint chain integrity is directionally correct and should continue to preserve traceability, stale-base rejection, and auditability without converting the proposal lifecycle into event sourcing.
- Priority-change refinement should be treated as explicit refinement input rather than inferred proposal mutation.
- Priority changes should use explicit domain language such as `DecisionPriorityAdjustment` or equivalent.
- Priority adjustment artifacts should record:
  - previous priority
  - new priority
  - reason
  - source
  - attribution
- Revision history and revision comparison UI should precede refinement mutation UI.
- Add a dedicated proposal lineage read model/projection before UI mutation controls if it can stay non-authoritative.
- Proposal lineage should summarize proposal evolution at a glance for human review, governance, operational adoption, and later M6/M7 work.

## Next Slice Direction

- Continue M5 with priority-change semantics and proposal lineage/read models.
- Keep UI work read-only first:
  - revision history browser
  - revision comparison viewer
  - current proposal versus historical revision distinction
- Defer refinement mutation UI until priority evolution and lineage are inspectable.
