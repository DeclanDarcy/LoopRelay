# Milestone 5: Core Roadmap State Runs from SQLite

## Objective

Make decision ledger, roadmap state, artifact lifecycle, and split lineage SQLite-canonical.

## Implementation

- [x] Implement SQLite-backed stores:
  - [x] decision ledger;
  - [x] roadmap state;
  - [x] artifact lifecycle;
  - [x] split lineage.
- [x] Route Roadmap CLI composition to SQLite stores when database mode is active.
- [x] Make decision append and next `DNNNN` allocation transaction-safe.
- [x] Enforce case-insensitive lifecycle path uniqueness.
- [x] Make split lookup by child path read SQLite, not filesystem globs.
- [x] Export deterministic equivalents:
  - [x] `.agents/decision-ledger.json`
  - [x] `.agents/state.json`
  - [x] `.agents/artifacts/lifecycle.json`
  - [x] `.agents/splits/split-family-*.json`

## Implementation Constraints

- Migrate decision ledger, roadmap state, lifecycle, and split lineage only.
- Decision append and ID allocation occur inside a database transaction.
- Lifecycle path uniqueness remains case-insensitive.
- Split lookup by child path must not scan stale files in SQLite mode.
- Delete/regenerate export tests prove database authority.

## Code Impact

- [x] `RoadmapTransitionPersistence.CaptureSummaryAsync` must use canonical stores for last decision ID and split family count.
- [x] Legacy markdown import is allowed only during explicit import, not normal SQLite runtime.
- [x] Stale filesystem JSON must not override database state.

## Tests

- [x] Delete exported core JSON files, load state from SQLite, regenerate exports.
- [x] Append decisions after imported `D0003` and verify next ID is `D0004`.
- [x] Lifecycle upsert rejects duplicate case variants.
- [x] Split child lookup works with only SQLite rows.
- [x] Exported core files import into a clean equivalent database.

## Exit Criteria

- [x] Core structured machine state is SQLite-canonical.
- [x] Regenerated exports can be deleted and restored without logical loss.
