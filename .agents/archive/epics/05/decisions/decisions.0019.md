# Decisions

## Newly Authorized

- The M4 option comparison slice is accepted as aligned with the roadmap.
- Option comparison must remain a projection of backend-owned read models.
- React must not recompute tradeoff summaries or create an alternative lifecycle interpretation.
- Evidence/source navigation is accepted as completing the explainability chain from recommendation to evidence to source attribution.
- Source attribution and evidence navigation remain read-only and presentation-only in React.
- React ownership remains limited to selection, filtering, navigation, and presentation.
- Backend ownership remains authoritative for lifecycle state, review state, and read models.
- No lifecycle authority should move into React.
- No operational-context coupling should be introduced by the M4 review workspace.
- No execution coupling should be introduced by the M4 review workspace.

## M4 Closure Guidance

- Perform a final M4 closure review before marking M4 complete.
- Evaluate information density, evidence discoverability, source attribution discoverability, review note visibility, diagnostics visibility, and navigation friction.
- A dedicated review diagnostics panel is not required if diagnostics are already discoverable where users need them in the proposal viewer, review workspace, and evidence surfaces.
- Avoid adding a diagnostics panel merely because it appeared in the earlier concept; the goal is explainability, not panel count.
- If the closure review finds no substantive gaps, close M4 and begin M5.

## Newly Authorized Next Slice

- Run the final M4 closure review.
- If no substantive gaps are found, mark M4 complete.
- Begin M5 refinement workflow planning after M4 closure.
- M5 should focus on revision UX, refinement requests, revision comparison, and revision history navigation rather than foundational lifecycle work.
