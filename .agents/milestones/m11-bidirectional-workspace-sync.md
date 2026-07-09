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

## Implementation Constraints

- Full export/import/export is stable by domain rules.
- Selective sync must leave unrelated domains unchanged.
- Stale/conflicting exports are blocked before overwrite.
- Older filesystem-only state imports without losing legacy-supported data.
- Publisher integration must verify fresh export before publishing.

## Conflict Rules

- [ ] If database changed and export changed since last sync, report conflict.
- [ ] If export is stale and database is newer, block import unless explicit reconciliation is requested.
- [ ] If scoped sync would leave unresolved cross-domain references, fail or require dependent domains.
- [ ] Unsupported schema or export versions fail safely.

## Open Questions

- What explicit conflict resolution commands should exist after sync detects divergent database/export edits?
- Will older CLIs need to operate directly against migrated workspaces, or only against generated filesystem exports?
- Are any external scripts or user workflows known to inspect `.agents` JSONL/JSON/markdown directly?

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
