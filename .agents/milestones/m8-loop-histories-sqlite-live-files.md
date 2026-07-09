# Milestone 8: Loop Histories Move to SQLite While Live Files Stay on Filesystem

## Objective

Move historical decisions, handoffs, and operational deltas to SQLite while retaining live files and live-first behavior.

## Implementation

- [ ] Implement `ILoopHistoryStore` for decision, handoff, and operational delta histories.
- [ ] Adapt `LoopArtifacts` into a live/history facade:
  - [ ] live `decisions.md`, `handoff.md`, and `operational_delta.md` remain filesystem files;
  - [ ] numbered histories are SQLite rows;
  - [ ] latest reads check live file first, then highest SQLite sequence.
- [ ] Preserve rotation ordering:
  - [ ] write SQLite history before deleting live file;
  - [ ] keep live file when history write fails.
- [ ] Export/import numbered markdown histories.

## Implementation Constraints

- Live decisions, live handoff, and live operational delta stay on disk.
- History writes allocate sequence inside SQLite transaction/constraint behavior.
- Rotation must not delete live files until SQLite history write succeeds.
- Latest fallback is numeric sequence order.
- Archive integration is still pending until M10.

## Tests

- [ ] Decision proposal writes live decisions file and SQLite `decisions.NNNN.md` history.
- [ ] Execution handoff rotates into SQLite before next decision.
- [ ] Operational delta transfer writes live delta, evolves context, then rotates into SQLite.
- [ ] Latest read prefers live files.
- [ ] Export/import preserves sequences and markdown bodies.
- [ ] Injected history write failure keeps live file available.

## Exit Criteria

- [ ] Histories are SQLite-canonical.
- [ ] Live files remain filesystem-backed and live-first.
- [ ] Completion archive support is not claimed until the next milestone.
