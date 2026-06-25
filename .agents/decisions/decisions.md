# Decisions

## Newly Authorized

- Proceed next with identity-aware operational evolution and semantic diff for Milestone 7.
- `UnderstandingDiffService` and operational evolution reporting must distinguish modified understanding from remove/add pairs when supported identity signals indicate the same evolving concept.
- Modification detection must remain backend-owned and deterministic, using stable lineage, source reference, section identity, persistent identity, or explicitly backend-owned semantic heuristics.
- Structured change records must carry previous state, current state, modification reason, identity basis, and supporting evidence.
- The identity basis must be exposed as part of the structured projection instead of remaining an internal implementation detail.
- Genuine removals and genuine additions must still be emitted when no supported identity relationship exists.
- Backend regression tests must cover modification versus remove/add, lineage matches, source-reference matches, genuine removals, and genuine additions.
- This slice remains backend-only; defer UI rendering until the operational evolution projection is complete.
