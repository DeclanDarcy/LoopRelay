# Decisions

## Newly Authorized

- Treat the completed Workspace Live Activity slice as architecturally correct: `WorkspaceLiveActivityPanel` is a display wrapper, `ExecutionEventFeed` remains the shared renderer, and `selectedExecutionEvents` remains the existing source of event truth.
- Preserve the invariant that Workspace activity must not introduce a second event store.
- Continue M3 with Workstream 3.5: Milestones Panel.
- Implement `WorkspaceMilestonesPanel` as an overview and navigation surface only.
- Render only known milestone inventory and selection state from existing frontend/backend projections.
- Safe milestone inputs are `workspace.artifactInventory.milestones`, selected milestone path, click/select milestone behavior, and navigation/open affordances.
- Do not invent milestone completion status, inferred progress, derived readiness, or synthetic workflow state unless those values are already provided by backend projections.
- `WorkspaceMilestonesPanel` may own milestone list rendering, empty state rendering, selected visual state, and navigation callbacks.
- `WorkspaceMilestonesPanel` must not own milestone parsing, progress inference, execution readiness, or workflow mutation.
