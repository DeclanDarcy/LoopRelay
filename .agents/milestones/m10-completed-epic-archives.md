# Milestone 10: Completed Epic Archives Preserve DB-Backed Historical State

## Objective

Make completed epic archives recover histories and execution evidence after those records move to SQLite.

## Implementation

- [ ] Add archive association logic that selects DB-backed decisions, handoffs, deltas, and execution evidence for the completed epic.
- [ ] Use persisted state, journal output paths, transition intents, and completion context to determine associations.
- [ ] Materialize associated DB-backed records into deterministic archive filesystem form.
- [ ] Preserve retained file archive behavior exactly where files remain filesystem-backed.
- [ ] Add archive import/recovery that reconstructs logical archived state without promoting it to active workspace state.
- [ ] Use staging before destructive retained-file moves.

## Code Impact

- [ ] Refactor `CompletedEpicArchiveService` so it no longer assumes history/evidence directories are canonical file directories.
- [ ] Add storage-neutral archive provider interfaces in `LoopRelay.Completion.Abstractions` and wire SQLite implementations in CLI compositions.
- [ ] Update completed epic evidence loaders to understand archive metadata when present.

## Tests

- [ ] Archive includes DB-backed decisions, handoffs, deltas, and execution evidence.
- [ ] Retained plan/context/milestones/review artifacts archive as before.
- [ ] Missing migrated record fails archive instead of silently dropping it.
- [ ] Archive path collisions abort before overwrite.
- [ ] Exported archive imports into a clean recovery context with equivalent archived state.

## Exit Criteria

- [ ] Completed epics remain recoverable with DB-backed histories/evidence.
- [ ] Active workspace state and archived state remain distinct.
