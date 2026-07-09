# Milestone 11: Bidirectional Workspace Synchronization

## Objective

Provide full and domain-scoped synchronization between canonical SQLite state and deterministic filesystem exports.

## Implementation

- [x] Implement `IWorkspaceSyncService`.
- [x] Add full export for every migrated domain.
- [x] Add full import from export into clean or existing database with conflict detection.
- [x] Add domain-scoped import/export.
- [x] Add dependency validation for scoped import/export.
- [x] Add sync markers and canonical hashes per domain.
- [x] Detect stale exports and external edit conflicts before overwrite.
- [x] Integrate `.agents` submodule publishing with fresh export preflight.

## Implementation Constraints

- Full export/import/export is stable by domain rules.
- Selective sync must leave unrelated domains unchanged.
- Stale/conflicting exports are blocked before overwrite.
- Older filesystem-only state imports without losing legacy-supported data.
- Publisher integration must verify fresh export before publishing.

## Conflict Rules

- [x] If database changed and export changed since last sync, report conflict.
- [x] If export is stale and database is newer, block import unless explicit reconciliation is requested.
- [x] If scoped sync would leave unresolved cross-domain references, fail or require dependent domains.
- [x] Unsupported schema or export versions fail safely.

## Open Questions

- What explicit conflict resolution commands should exist after sync detects divergent database/export edits?
- Will older CLIs need to operate directly against migrated workspaces, or only against generated filesystem exports?
- Are any external scripts or user workflows known to inspect `.agents` JSONL/JSON/markdown directly?

## Code Impact

- [x] Existing `AgentsSubmodulePublisher` calls in Main/Roadmap flows must ensure the migrated export surface is fresh before publishing.
- [x] `storage-export`, `storage-import`, and `storage-sync` commands report domain scope, marker hashes, and conflicts.
- [x] `storage-export`, `storage-import`, and `storage-sync` commands report changed rows and changed files.

## Tests

- [x] Full export regenerates all migrated files.
- [x] Full import from generated export creates equivalent database.
- [x] Export/import/export is stable.
- [x] Scoped sync leaves unrelated domains unchanged.
- [x] Stale export and divergent edit fixtures fail safely.
- [x] Fake publisher publishes only after fresh export.
- [x] Older filesystem-only state imports without losing legacy-supported data.

## Exit Criteria

- [x] Workspace synchronization is intentional, safe, and test-covered.
- [x] `.agents` submodule publishing can publish the intended export surface.
