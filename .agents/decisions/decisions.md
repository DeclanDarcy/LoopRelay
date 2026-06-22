# Decisions

## Newly Authorized

- M5 should now transition from backend lifecycle implementation into presentation work.
- The proposal lineage projection is accepted as the correct abstraction and should remain a first-class backend concept.
- React must consume lineage rather than reconstruct proposal history, revision ordering, parent relationships, summaries, priority adjustments, retired assumptions, or retired options.
- The artifact authority decomposition is:
  - proposal is authority
  - revision is history
  - comparison is explanation
  - lineage is navigation
- Lineage should become the canonical navigation surface for later M6 resolution integration, M7 governance, and M10 operational adoption questions about proposal evolution.
- The first M5 UI surface should be a revision history panel focused on understanding proposal evolution, current state, evolution sequence, and revision count.
- The second M5 UI surface should be a revision comparison viewer that renders backend-provided comparison states rather than introducing client-side diff interpretation.
- The third M5 UI surface should make current proposal versus historical revision visually explicit:
  - current proposal is active, authoritative, and carries the current recommendation
  - historical revision is read-only, historical, and not current
- Before adding refinement mutation UI, consider adding a lightweight lineage summary model for history navigation containing revision id, timestamp, revision type, change summary, priority adjustment count, and retired item count.
- The lineage history panel should not need to load every full comparison artifact just to render the revision list.
- Refinement mutation UI remains deferred until read-only lineage, revision history, and revision comparison surfaces preserve the existing authority boundaries.

## Next Slice Direction

- Start M5 read-only UI with the revision history panel over lineage.
- Then add the revision comparison viewer using backend comparison projection.
- Consider adding the lightweight lineage summary projection before or during the history panel work if the current lineage payload is too detailed for navigation.
