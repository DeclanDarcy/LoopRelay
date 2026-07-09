# Milestone 10: Completed Epic Archives Preserve DB-Backed Historical State

## Objective

Make completed epic archives recover histories and execution evidence after those records move to SQLite.

## Implementation

- [x] Add archive association logic that selects DB-backed decisions, handoffs, deltas, and execution evidence for the completed epic.
- [x] Use persisted state, journal output paths, transition intents, and completion context to determine associations.
- [x] Materialize associated DB-backed records into deterministic archive filesystem form.
- [x] Preserve retained file archive behavior exactly where files remain filesystem-backed.
- [x] Add archive import/recovery that reconstructs logical archived state without promoting it to active workspace state.
- [x] Use staging before destructive retained-file moves.

## Implementation Constraints

- Association must select migrated records from persisted state, journal, and path references deterministically.
- Missing migrated records fail archive completion; do not silently complete.
- Materialize archives into deterministic filesystem form.
- Validate and stage before destructive retained-file moves.
- Archive import/recovery must not pollute active workspace state.

## Code Impact

- [x] Refactor `CompletedEpicArchiveService` so it no longer assumes history/evidence directories are canonical file directories.
- [x] Add storage-neutral archive provider interfaces in `LoopRelay.Completion.Abstractions` and wire SQLite implementations in CLI compositions.
- [x] Update completed epic evidence loaders to understand archive metadata when present.

## Open Questions

- What exact archive export layout should materialize DB-backed histories and evidence while preserving existing archive discovery?

## Tests

- [x] Archive includes DB-backed decisions, handoffs, deltas, and execution evidence.
- [x] Retained plan/context/milestones/review artifacts archive as before.
- [x] Missing migrated record fails archive instead of silently dropping it.
- [x] Archive path collisions abort before overwrite.
- [x] Exported archive imports into a clean recovery context with equivalent archived state.

## Exit Criteria

- [x] Completed epics remain recoverable with DB-backed histories/evidence.
- [x] Active workspace state and archived state remain distinct.
