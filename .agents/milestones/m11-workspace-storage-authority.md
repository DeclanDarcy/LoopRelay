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

### Export/rehydration boundary

M11 owns the versioned canonical export codec, canonical decoder, semantic fingerprint, and a
test-only/fresh-target canonical rehydration path. Acceptance performs export -> fresh canonical
rehydration -> owner-projection comparison without exposing a public legacy `storage import`
implementation. Public `storage import` remains unavailable/delegated until M12 wires source
detection, mapping, approval, and one-way import. M12 may consume the codec but may not redefine
its schema or storage semantics.

### Recoverable initialization and strict verification

Because `storage init` cannot journal in a target database that does not exist, use a deterministic
staging database/artifact under the canonical persistence directory, keyed by operation ID. It
contains the intended workspace identity, target schema manifest, operation plan, and creation
evidence. Validate it completely, then use one absence-guarded atomic filesystem promotion effect
to install the database. Restart inspects staging and target independently and completes the same
promotion, records an existing matching receipt, or fails ambiguous; it never overwrites an
existing authority.

Migration/sync plans for an existing recognizable database live in that database's operation
journal. Database-internal transactional changes use the Storage Authority's versioned migration
executor; outward file creation, replacement, export, and projection work are M8 effects. Do not
model every SQL statement as a separate external effect.

Verification/status opens SQLite in an OS/driver-enforced read-only mode and does not create or
alter the database, `-wal`, `-shm`, journal, metadata, migration receipt, observable access time, or
workspace projection. Hash and inventory the persistence tree before and after two repeated
queries. Report `Healthy`, `ActionRequired`, `Unsupported`, or `Corrupt` with
identity/family/version/shape/fingerprint evidence. A v8 or recognized partial-v9 source reports
the complete migration chain to the then-current version.

### Bounded sync and export semantics

`storage sync` may reconcile only rebuildable projections and already-journaled effect work from
canonical facts. It cannot import legacy facts, rewrite authoritative history, merge another
store, or invent defaults. Its plan enumerates each projection/effect, source watermark,
postcondition, and expected no-op cases.

The export manifest includes schema/codec version, workspace identity, domain row counts, canonical
ordering rules, explicit null/unknown fields, per-domain hashes, whole-package SHA-256, and a
logical fingerprint independent of SQLite bytes and insertion order where domain order is not
semantic. Round-trip comparison reports a typed per-domain semantic diff.
