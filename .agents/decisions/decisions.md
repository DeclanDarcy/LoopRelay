# Decisions

## Newly Authorized

- The M6 supersession boundary is accepted:
  - supersession means replacement lineage
  - archive means retirement state
- Supersession must remain distinct from archive:
  - `Decision A was replaced by Decision B`
  - is not equivalent to
  - `Decision A is no longer active`
- Recording `Supersedes` on the replacement decision is accepted as the right lineage direction because it preserves the forward authority chain from old resolved decision to new resolved decision.
- Replacement authority must already be `Resolved`.
- `UnderReview`, `Deferred`, and `Archived` decisions must not become replacement authority through supersession.
- Archive should remain a terminal cleanup/retirement action after `Superseded -> Archived`.
- Resolution UI remains deferred until assimilation recommendation boundaries are stable.
- Assimilation recommendation packages must be explicitly advisory:
  - `Resolved Decision`
  - to `Assimilation Recommendation Package`
  - to human or later continuity workflow
- Decision resolution must never directly mutate `.agents/operational_context.md`.
- The next M6 implementation slice should cover assimilation recommendation packages.
- Core next-slice tests should prove:
  - packages are generated only from `Resolved` decisions
  - packages include source decision and snapshot lineage
  - packages do not mutate `.agents/operational_context.md`
  - packages do not promote or merge continuity state
  - package projection is reproducible from repository artifacts
- Current M6 status is accepted as:
  - Resolution snapshot complete
  - Resolution service boundary complete
  - Outcome semantics complete
  - Supersede/archive complete
  - Assimilation recommendations next
  - Resolution UI deferred
