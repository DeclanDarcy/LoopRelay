# Milestone 4: SQLite Workspace Store and Importable Database State

## Objective

Introduce SQLite initialization, versioning, integrity validation, transactions, and snapshot import without changing runtime workflow authority.

## Implementation

- [ ] Add SQLite package and workspace database locator.
- [ ] Create schema metadata and initial tables.
- [ ] Implement schema migrator and integrity validator.
- [ ] Implement filesystem snapshot import into SQLite in one transaction.
- [ ] Compare imported database snapshot to filesystem snapshot before classifying as valid.
- [ ] Add `storage-init` and `storage-import` command behavior.

## Tests

- [ ] Missing database initializes to valid empty state.
- [ ] Full filesystem snapshot imports to logically equivalent database.
- [ ] Re-import with unchanged source is idempotent.
- [ ] Import failure rolls back.
- [ ] Unsupported schema version blocks access without mutation.
- [ ] Corrupt database and invalid row fixtures classify correctly.
- [ ] Existing workflows still run file-backed after database import.

## Exit Criteria

- [ ] SQLite database can be initialized, imported, and validated.
- [ ] Workflows still use file-backed stores.
- [ ] No exported files are deleted or treated as projections yet.
