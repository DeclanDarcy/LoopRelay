# Milestone 9: Slim `RoadmapStateMachine`

## Work Items

- [ ] Remove extracted private methods from `RoadmapStateMachine` after handlers are wired.
- [ ] Keep command dispatch, status reporting, Project Context preflight,
  prompt-contract snapshot emission, startup planning, resume planning, unblock
  planning, terminal selection route persistence, cancellation persistence, and
  generic error reporting.
- [ ] Keep simple selection terminal routes in `ContinueAfterSelectionAsync` unless a later refactor extracts them; they are not required for this change.
- [ ] Ensure `RoadmapStateMachine` constructor only receives handlers and high-level planners for paths it still owns.

## Expected Shape

- [ ] `RunFromCoreReady` checks for missing completion context, calls the bootstrap handler when needed, then continues to selection-and-following.
- [ ] `RunSelectionAndFollowing` calls the select-next handler, then continues after selection.
- [ ] `ContinueAfterSelection` routes existing epics to the epic-preparation handler.
- [ ] `ContinueAfterSelection` routes new epics to the create-new handler.
- [ ] `ContinueAfterSelection` routes split epics to the split handler.
- [ ] `ContinueAfterSelection` keeps terminal selection outcomes on existing state and decision persistence.
- [ ] `ContinueAfterSelection` falls through to the milestone handler when the active epic is ready.

```text
RunFromCoreReady
-> if completion context missing: bootstrap handler
-> selection-and-following

RunSelectionAndFollowing
-> select-next handler
-> continue-after-selection

ContinueAfterSelection
-> existing epic: epic-preparation handler
-> new epic: create-new handler
-> split epic: split handler
-> terminal selection outcomes: existing state/decision persistence
-> if active epic ready: milestone handler
```
