# Decisions

## Newly Authorized

- Accept the M0 closure audit conclusion that remaining `App.tsx` ownership is not automatically an extraction requirement.
- Treat the major M0 architectural milestone as separating extracted projection concerns from intentionally retained workflow concerns.
- Continue deferring commit preparation extraction because it is workflow review state coupled to selected paths, draft message, readiness, and review.
- Continue deferring operational-context proposal extraction because proposal loading is coupled to proposal draft, review note, comparison content, and review workflow.
- Proceed with Workstream 0.5.
- Define Workstream 0.5 as structural decomposition, not authority migration.
- Treat Workstream 0.5 as complexity reduction after authority boundaries have stabilized.
- Begin Workstream 0.5 with characterization around repository selection, workspace loading, and refresh reconciliation.
- Prefer pure helper extraction first, including selection reconciliation, artifact path utilities, milestone path utilities, view-model formatting, display-only helpers, and sorting/grouping helpers.
- Avoid extracting anything that starts owning workflow readiness, proposal review, commit review, execution meaning, or promotion meaning.
- Treat future M0 completion as requiring explicit projection, navigation, draft, and transport authority; projection, navigation, and draft-boundary certification; documented workflow deferrals; and completed structural decomposition.

## Next Authorized Slice

Start Workstream 0.5 with characterization-first structural decomposition:

- Add tests for repository selection, workspace loading, and refresh reconciliation.
- Extract pure helpers from `App.tsx` where ownership does not move.
- Preserve workflow authority, draft ownership, and backend-owned state transitions in their current boundaries.
