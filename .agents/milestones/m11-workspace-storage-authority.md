# M11 — Workspace Storage Authority


### Contracts

- [ ] Place logical storage operation contracts under `LoopRelay.Orchestration.Primitives/Storage/`; keep raw SQLite inspection/migration primitives in `LoopRelay.Core/Services/Persistence/`.

- [ ] `storage verify`: strictly read-only inspection of existence, bytes, identity, family, version, physical shape, corruption, unsupported version, unresolved references, and interrupted operations.
- [ ] `storage init`: create a fresh canonical workspace only when no authority exists; ambiguous/existing state is a typed refusal.
- [ ] `storage migrate`: explicitly plan and execute a supported schema upgrade/convergence through M8/M9.
- [ ] `storage export`: emit a versioned semantic package, manifest, hashes, and logical fingerprint through effects.
- [ ] `storage sync`: reconcile rebuildable projections/effect work with canonical facts; it is never bidirectional legacy import.
- [ ] `storage import`: delegate source interpretation and one-way import to M12.

### Implementation

- [ ] Split `LoopRelayWorkspaceDatabase` into read-only inspector, version manifest/migration catalog, connection factory, and migration executors while preserving one schema authority.
- [ ] Make inspection APIs impossible to open a read-write connection. Byte-hash the workspace before and after verify/status tests.
- [ ] Add durable storage-operation plan/event/receipt records. Persist the plan before database/filesystem mutation; execute through typed effects; recover through M9.
- [ ] Define a versioned export DTO for every logical domain, explicit null/unknown historical fields, and stable semantic fingerprint independent of row order and SQLite bytes.
- [ ] Implement export -> fresh import -> canonical projection comparison. A file-format match alone is insufficient.
- [ ] Remove migration and direct SQL from `CanonicalCliApplicationService`/`UnifiedCliRunner`. Remove the current storage commands that merely ensure schema or write `workspace_metadata` while claiming import/export/sync.
- [ ] Change startup/status to inspect and return `MigrationRequired`; only `storage migrate` may mutate.

### Verification and exit gate

- [ ] Test healthy v9+, v8 migration required, recognized partial-v9 convergence, unknown/stamped-incomplete schema, corruption, interrupted init/migrate/sync, repeated operations, and semantic export round-trip. Verification and status must preserve every byte. No application or observer code may issue SQL. Command labels and typed results must exactly describe performed work.

