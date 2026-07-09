# Milestone 7: Transition Journal Runs from SQLite with JSONL Interchange

## Objective

Make transition chronology SQLite-canonical while preserving ordered JSONL import/export and legacy records without input snapshots.

## Implementation

- [ ] Implement SQLite journal append with monotonic `event_order`.
- [ ] Preserve correlation IDs, event kind, states, transition, projection, input hashes, output paths, result, decision, error, and optional input snapshot.
- [ ] Import legacy JSONL lines, including records without input snapshots.
- [ ] Export deterministic JSONL ordered by `event_order`.
- [ ] Route transition runner and state-machine journal writes through the SQLite store.

## Tests

- [ ] Started/completed/failed event order is stable.
- [ ] Legacy no-snapshot records import.
- [ ] JSONL export imports into a clean equivalent database.
- [ ] Concurrent append smoke test.
- [ ] Verification hook reports unresolved output paths without mutating journal rows.

## Exit Criteria

- [ ] Journal runtime authority is SQLite.
- [ ] JSONL remains an importable/exportable debugging surface.
