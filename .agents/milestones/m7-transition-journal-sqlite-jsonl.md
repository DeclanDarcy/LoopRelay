# Milestone 7: Transition Journal Runs from SQLite with JSONL Interchange

## Objective

Make transition chronology SQLite-canonical while preserving ordered JSONL import/export and legacy records without input snapshots.

## Implementation

- [x] Implement SQLite journal append with monotonic `event_order`.
- [x] Preserve correlation IDs, event kind, states, transition, projection, input hashes, output paths, result, decision, error, and optional input snapshot.
- [x] Import legacy JSONL lines, including records without input snapshots.
- [x] Export deterministic JSONL ordered by `event_order`.
- [x] Route transition runner and state-machine journal writes through the SQLite store.

## Implementation Constraints

- Do not add workflow-level transaction atomicity yet.
- Journal rows are append-only.
- Append assigns monotonic order independent of filesystem order.
- Started, completed, and failed records sharing a correlation ID remain queryable together.
- JSONL export is debugging/interchange, not runtime authority.

## Open Questions

- What JSONL byte-compatibility level is required for external transition journal tools beyond deterministic logical content?

## Tests

- [x] Started/completed/failed event order is stable.
- [x] Legacy no-snapshot records import.
- [x] JSONL export imports into a clean equivalent database.
- [x] Concurrent append smoke test.
- [x] Verification hook reports unresolved output paths without mutating journal rows.

## Exit Criteria

- [x] Journal runtime authority is SQLite.
- [x] JSONL remains an importable/exportable debugging surface.
