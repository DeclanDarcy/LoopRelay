# Milestone 4: SQLite Workspace Store and Importable Database State

## Objective

Introduce SQLite initialization, versioning, integrity validation, transactions, and snapshot import without changing runtime workflow authority.

## Implementation

- [x] Add SQLite package and workspace database locator.
- [x] Create schema metadata and initial tables.
- [x] Implement schema migrator and integrity validator.
- [x] Implement filesystem snapshot import into SQLite in one transaction.
- [x] Compare imported database snapshot to filesystem snapshot before classifying as valid.
- [x] Add `storage-init` and `storage-import` command behavior.

## Implementation Constraints

- Workflows remain file-backed after import.
- Import is all-or-nothing and idempotent.
- Integrity validation distinguishes valid empty/imported state, corrupt state, unsupported schema, and incompatible partial state.
- Existing file-backed workflow tests must still pass after database import.

## Tests

- [x] Missing database initializes to valid empty state.
- [x] Full filesystem snapshot imports to logically equivalent database.
- [x] Re-import with unchanged source is idempotent.
- [x] Import failure rolls back.
- [x] Unsupported schema version blocks access without mutation.
- [x] Corrupt database and invalid row fixtures classify correctly.
- [x] Existing workflows still run file-backed after database import.

## Exit Criteria

- [x] SQLite database can be initialized, imported, and validated.
- [x] Workflows still use file-backed stores.
- [x] No exported files are deleted or treated as projections yet.
