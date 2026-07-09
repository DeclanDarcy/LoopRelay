# Milestone 8: Loop Histories Move to SQLite While Live Files Stay on Filesystem

## Objective

Move historical decisions, handoffs, and operational deltas to SQLite while retaining live files and live-first behavior.

## Implementation

- [x] Implement `ILoopHistoryStore` for decision, handoff, and operational delta histories.
- [x] Adapt `LoopArtifacts` into a live/history facade:
  - [x] live `decisions.md`, `handoff.md`, and `operational_delta.md` remain filesystem files;
  - [x] numbered histories are SQLite rows;
  - [x] latest reads check live file first, then highest SQLite sequence.
- [x] Preserve rotation ordering:
  - [x] write SQLite history before deleting live file;
  - [x] keep live file when history write fails.
- [x] Export/import numbered markdown histories.

## Implementation Constraints

- Live decisions, live handoff, and live operational delta stay on disk.
- History writes allocate sequence inside SQLite transaction/constraint behavior.
- Rotation must not delete live files until SQLite history write succeeds.
- Latest fallback is numeric sequence order.
- Archive integration is still pending until M10.

## Tests

- [x] Decision proposal writes live decisions file and SQLite `decisions.NNNN.md` history.
- [x] Execution handoff rotates into SQLite before next decision.
- [x] Operational delta transfer writes live delta, evolves context, then rotates into SQLite.
- [x] Latest read prefers live files.
- [x] Export/import preserves sequences and markdown bodies.
- [x] Injected history write failure keeps live file available.

## Exit Criteria

- [x] Histories are SQLite-canonical.
- [x] Live files remain filesystem-backed and live-first.
- [x] Completion archive support is not claimed until the next milestone.
