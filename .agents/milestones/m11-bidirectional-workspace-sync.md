# Milestone 11: Bidirectional Workspace Synchronization

## Objective

Provide full and domain-scoped synchronization between canonical SQLite state and deterministic filesystem exports.

## Implementation

- [ ] Implement `IWorkspaceSyncService`.
- [ ] Add full export for every migrated domain.
- [ ] Add full import from export into clean or existing database with conflict detection.
- [ ] Add domain-scoped import/export with dependency validation.
- [ ] Add sync markers and canonical hashes per domain.
- [ ] Detect stale exports and external edit conflicts before overwrite.
- [ ] Integrate `.agents` submodule publishing with fresh export preflight.

## Conflict Rules

- [ ] If database changed and export changed since last sync, report conflict.
- [ ] If export is stale and database is newer, block import unless explicit reconciliation is requested.
- [ ] If scoped sync would leave unresolved cross-domain references, fail or require dependent domains.
- [ ] Unsupported schema or export versions fail safely.

## Code Impact

- [ ] Existing `AgentsSubmodulePublisher` calls in Main/Roadmap flows must ensure the migrated export surface is fresh before publishing.
- [ ] `storage-export`, `storage-import`, and `storage-sync` commands report changed rows, changed files, domain scope, marker hashes, and conflicts.

## Tests

- [ ] Full export regenerates all migrated files.
- [ ] Full import from generated export creates equivalent database.
- [ ] Export/import/export is stable.
- [ ] Scoped sync leaves unrelated domains unchanged.
- [ ] Stale export and divergent edit fixtures fail safely.
- [ ] Fake publisher publishes only after fresh export.
- [ ] Older filesystem-only state imports without losing legacy-supported data.

## Exit Criteria

- [ ] Workspace synchronization is intentional, safe, and test-covered.
- [ ] `.agents` submodule publishing can publish the intended export surface.
