# Milestone 5: Core Roadmap State Runs from SQLite

## Objective

Make decision ledger, roadmap state, artifact lifecycle, and split lineage SQLite-canonical.

## Implementation

- [ ] Implement SQLite-backed stores:
  - [ ] decision ledger;
  - [ ] roadmap state;
  - [ ] artifact lifecycle;
  - [ ] split lineage.
- [ ] Route Roadmap CLI composition to SQLite stores when database mode is active.
- [ ] Make decision append and next `DNNNN` allocation transaction-safe.
- [ ] Enforce case-insensitive lifecycle path uniqueness.
- [ ] Make split lookup by child path read SQLite, not filesystem globs.
- [ ] Export deterministic equivalents:
  - [ ] `.agents/decision-ledger.json`
  - [ ] `.agents/state.json`
  - [ ] `.agents/artifacts/lifecycle.json`
  - [ ] `.agents/splits/split-family-*.json`

## Implementation Constraints

- Migrate decision ledger, roadmap state, lifecycle, and split lineage only.
- Decision append and ID allocation occur inside a database transaction.
- Lifecycle path uniqueness remains case-insensitive.
- Split lookup by child path must not scan stale files in SQLite mode.
- Delete/regenerate export tests prove database authority.

## Code Impact

- [ ] `RoadmapTransitionPersistence.CaptureSummaryAsync` must use canonical stores for last decision ID and split family count.
- [ ] Legacy markdown import is allowed only during explicit import, not normal SQLite runtime.
- [ ] Stale filesystem JSON must not override database state.

## Tests

- [ ] Delete exported core JSON files, load state from SQLite, regenerate exports.
- [ ] Append decisions after imported `D0003` and verify next ID is `D0004`.
- [ ] Lifecycle upsert rejects duplicate case variants.
- [ ] Split child lookup works with only SQLite rows.
- [ ] Exported core files import into a clean equivalent database.

## Exit Criteria

- [ ] Core structured machine state is SQLite-canonical.
- [ ] Regenerated exports can be deleted and restored without logical loss.
