# Milestone 13: Storage Compatibility and Verification Mode

## Objective

Expose executable verification that proves the database, filesystem exports, retained files, logical references, archive recovery, and storage-mode behavior are mutually consistent.

## Implementation

- [ ] Implement `IWorkspaceVerificationService`.
- [ ] Compose existing domain validators, sync service, logical resolver, archive recovery checks, and transaction integrity validator.
- [ ] Add temporary export/import round-trip verification in an isolated temp workspace.
- [ ] Add read-only database open and mutation guard checks.
- [ ] Expose `storage-verify`.
- [ ] Add optional `--full-roundtrip` for slower export/import/export checks.

## Verification Findings

Verification reports:

- [ ] success;
- [ ] stale export;
- [ ] missing exported file;
- [ ] unresolved logical path;
- [ ] nondeterministic round trip;
- [ ] unrecoverable archive;
- [ ] corrupt domain;
- [ ] unsupported version;
- [ ] mutation required;
- [ ] conflict.

Each finding includes domain, identity/path, rule, severity, current state, expected state, and recommended executable recovery action.

## Tests

- [ ] Valid SQLite-canonical workspace with fresh exports verifies successfully.
- [ ] Legacy filesystem workspace imports and verifies successfully.
- [ ] Stale export fixture fails.
- [ ] Missing export fixture fails.
- [ ] Unresolved path fixture fails.
- [ ] Nondeterministic serializer fixture fails.
- [ ] Broken completed epic archive fixture fails.
- [ ] Unsupported schema/export version fixture fails.
- [ ] Read-only verification does not change database bytes or export tree hashes.

## Exit Criteria

- [ ] Verification is read-only by default.
- [ ] Required consistency failures are detected.
- [ ] Full migrated persistence architecture is covered by executable checks.
