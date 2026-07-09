# Milestone 13: Storage Compatibility and Verification Mode

## Objective

Expose executable verification that proves the database, filesystem exports, retained files, logical references, archive recovery, and storage-mode behavior are mutually consistent.

## Implementation

- [x] Implement `IWorkspaceVerificationService`.
- [x] Compose existing domain validators, sync service, logical resolver, archive recovery checks, and transaction integrity validator.
- [x] Add temporary export/import round-trip verification in an isolated temp workspace.
- [x] Add read-only database open and mutation guard checks.
- [x] Expose `storage-verify`.
- [x] Add optional `--full-roundtrip` for slower export/import/export checks.

## Implementation Constraints

- Do not introduce new architecture or persistence domains.
- Verification composes existing validators and sync services, not duplicate parsers.
- Default verify is read-only; temp workspaces are used for round-trip checks.
- CLI/API results map deterministically to verification categories.
- Failure fixtures cover every required detection class.

## Verification Findings

Verification reports:

- [x] success;
- [x] stale export;
- [x] missing exported file;
- [x] unresolved logical path;
- [x] nondeterministic round trip;
- [x] unrecoverable archive;
- [x] corrupt domain;
- [x] unsupported version;
- [x] mutation required;
- [x] conflict.

Each finding includes domain, identity/path, rule, severity, current state, expected state, and recommended executable recovery action.

## Tests

- [x] Valid SQLite-canonical workspace with fresh exports verifies successfully.
- [x] Legacy filesystem workspace imports and verifies successfully.
- [x] Stale export fixture fails.
- [x] Missing export fixture fails.
- [x] Unresolved path fixture fails.
- [x] Nondeterministic serializer fixture fails.
- [x] Broken completed epic archive fixture fails.
- [x] Unsupported schema/export version fixture fails.
- [x] Read-only verification does not change database bytes or export tree hashes.

## Exit Criteria

- [x] Verification is read-only by default.
- [x] Required consistency failures are detected.
- [x] Full migrated persistence architecture is covered by executable checks.
