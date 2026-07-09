# Milestone 4 — SQLite Workspace Store and Importable Database State Deep Dive

<!-- GENERATED:START -->
Generated from `.agents/specs/roadmap.md` and `.agents/specs/audit.md`.

## 1. Milestone Summary

- Milestone Identifier: Milestone 4
- Milestone Name: SQLite Workspace Store and Importable Database State
- Short Description: Add a SQLite-backed workspace store that can initialize, validate, version, and import migrated domains from filesystem state without changing workflow semantics.
- Implementation Role: Introduces the database substrate as importable state while keeping file-backed workflows operational.
- Roadmap Position: Fourth milestone; follows domain behavior, serialization, and logical identity; precedes SQLite-canonical domains.
- Primary Outcomes:
- A workspace can create a canonical SQLite database from existing `.agents` machine-managed files.
- Database integrity validation distinguishes empty, imported, corrupt, and unsupported-version states.
- Existing workflows can still run against file-backed persistence after import.

## 2. Normative Basis

**Roadmap Authority**
- Roadmap authority: `.agents/specs/roadmap.md` `# Project Goal`, `# Guiding Principles`, and this milestone's `## Milestone` section.
- `.agents/specs/roadmap.md` `## Milestone 4 — SQLite Workspace Store and Importable Database State`.
**Architectural Authority**
- Architectural authority: `.agents/specs/audit.md` sections `2. Persistence Authority`, `4. Persistence Semantics`, `5. Filesystem-Coupled Behaviors`, `8. Cross-Boundary Dependencies`, `9. Compatibility and Migration Constraints`, `10. Round-Trip Import/Export Requirements`, `14. Persistence Domain Inventory`, `16. Persistence Dependency Graph`, `17. Synchronization Model`, `19. Persistence Platform Capabilities`, and `20. Roadmap-Relevant Findings`.
**Implementation Authority**
- Implementation authority: existing LoopRelay persistence code under `src/LoopRelay.Roadmap.Cli/Services`, `src/LoopRelay.Cli/Services/Execution`, `src/LoopRelay.Completion/Services/ArtifactStorage`, `src/LoopRelay.Projections/Services`, `src/LoopRelay.Core/Abstractions/Artifacts/IArtifactStore.cs`, and `src/LoopRelay.Infrastructure/Services/Artifacts`.
**Supporting Context**
- Supporting context: current tests under `tests/LoopRelay.Roadmap.Cli.Tests`, `tests/LoopRelay.Cli.Tests`, `tests/LoopRelay.Completion.Tests`, `tests/LoopRelay.Projections.Tests`, and `tests/LoopRelay.Infrastructure.Tests`.
- No undocumented authority is introduced here; unresolved audit mismatches remain open questions rather than implementation permission.

## 3. Objective

Implement SQLite workspace initialization, schema versioning, integrity validation, transaction support, and filesystem snapshot import for migrated domains, with database state validated against the imported filesystem snapshot.

## 4. Non-Goals

- Do not route production workflows to SQLite as canonical persistence.
- Do not delete or stop writing existing filesystem canonical files.
- Do not implement full workspace synchronization, conflict resolution, or export commands.
- Do not make multi-domain workflow updates transactional yet.
- Do not resolve future mixed-version CLI rollout policy beyond safe import validation.

## 5. Runtime / System State Before

- Milestones 1-3 provide domain behavior, import/export snapshots, and logical path resolution.
- No SQLite database, schema version table, integrity checker, or importer exists for migrated domains.
- Filesystem state remains canonical and workflow-safe.

## 6. Runtime / System State After

- Workspace database can be initialized and versioned.
- Filesystem migrated-domain snapshots can be imported idempotently into SQLite.
- Integrity validation can identify valid empty state, valid imported state, corrupt state, and unsupported schema versions.
- Database snapshot comparison proves imported logical state equals filesystem source.
- Workflows remain file-backed until later milestones intentionally switch domains.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| SQLite workspace initialization | SQLite workspace store | Create database file, schema metadata, and empty domain tables. | Workspace root and schema version. | Initialized SQLite database. | SQLite runtime package and schema definitions. | Valid empty database passes integrity checks. | Domain SQLite stores, import, verification. |
| Filesystem snapshot import | Workspace importer | Load Milestone 2 snapshots into database rows. | Validated domain snapshots. | SQLite state preserving identities, timestamps, hashes, and path references. | Milestone 2 serializers and Milestone 3 identity model. | Existing `.agents` state imports with logical equality. | Milestones 5-9 canonical stores, synchronization. |
| Schema version and integrity validation | SQLite workspace store | Track schema versions and classify database health. | Database metadata, domain tables, integrity checks. | Validation result with diagnostics. | Database schema. | Corrupt, partial, duplicate, and unsupported versions fail safely. | CLI startup, import, verification. |
| Transaction support | SQLite substrate | Provide transaction primitive for import and future workflow updates. | Domain write functions. | Committed or rolled-back database changes. | SQLite connection management. | Failed import leaves no partial imported state claimed as valid. | Importer, later canonical domain stores, Milestone 12. |

## 8. Architectural Responsibilities

- SQLite workspace store owns database file lifecycle, schema version, connection management, and transaction boundary primitives.
- Domain importers own mapping from snapshots to rows but do not bypass workspace transactions.
- Filesystem remains runtime authority for workflows until later milestones.
- Integrity validator owns database health classification, not domain business decisions.

**Cross-Milestone Constraints**
- SQLite becomes canonical only for machine-managed migrated domains.
- Retained markdown prompt/workflow artifacts remain filesystem-backed.
- Filesystem exports are deterministic, importable serializations of migrated domain state.
- Repo-relative path strings remain durable logical identities.
- `DNNNN`, `NNNN`, family IDs, runtime prompt names, and correlation IDs preserve imported identity.
- Behavior moves behind semantic domain operations before callers stop using filesystem representations.
- Freshness semantics remain trustworthy across retained files and migrated records.

## 9. Components and Modules

**SQLite workspace store**
- Purpose: Initialize and open workspace database safely.
- Responsibilities: Path selection, connection creation, schema metadata, transaction wrapper, integrity checks.
- Owned State: SQLite database file and schema metadata.
- Consumed State: Workspace root and schema definitions.
- Public Contracts: Open/init/validate/import transaction APIs.
- Internal Contracts: No domain write outside transaction context during import.
- Dependencies: SQLite provider package.
- Tests Required: Empty, versioned, corrupt, unsupported version tests.
**Schema migrator**
- Purpose: Create versioned schema for migrated domain imports.
- Responsibilities: Apply initial schema, record schema version, reject unsupported versions.
- Owned State: Schema tables and metadata rows.
- Consumed State: Domain table definitions.
- Public Contracts: Initialize/validate schema version.
- Internal Contracts: Migrations are idempotent for same version.
- Dependencies: Workspace store.
- Tests Required: Re-init and incompatible version tests.
**Filesystem-to-SQLite importer**
- Purpose: Populate database from validated filesystem snapshots.
- Responsibilities: Map snapshots to rows, preserve identities, detect duplicates and partial state.
- Owned State: Imported domain rows.
- Consumed State: Milestone 2 workspace snapshot.
- Public Contracts: Import workspace snapshot.
- Internal Contracts: All imported domains commit atomically or roll back.
- Dependencies: Snapshot serializers and transactions.
- Tests Required: Idempotent import, duplicate, partial, and equality tests.

## 10. Repository and File Impact

- `src/LoopRelay.Roadmap.Cli/Services/Artifacts` and `RoadmapArtifactPaths` remain the source of existing roadmap artifact path constants until a shared path layer replaces them.
- `src/LoopRelay.Roadmap.Cli/Services/State`, `TransitionState`, `TransitionCoordination`, `Decisions`, `ExecutionPreparation`, `Projections`, `Splits`, and `ArtifactManagement` contain the current roadmap domain stores.
- `src/LoopRelay.Cli/Services/Execution/LoopArtifacts.cs` owns live decision, handoff, and operational delta loop-file behavior today.
- `src/LoopRelay.Completion/Services/ArtifactStorage` owns completed-epic archive and evidence access used by completion workflows.
- `src/LoopRelay.Infrastructure/Services/Artifacts` contains repository-relative path bridging and numbered artifact sequence helpers.
- Expected SQLite substrate can live in a shared infrastructure namespace if multiple CLIs need it, with roadmap domain mappings beside roadmap domain stores.
- Add tests under roadmap/infrastructure persistence-focused test directories.
- No exported filesystem files should be removed or treated as projections yet.

## 11. Public Contracts

- Workspace database initialization and validation APIs.
- Filesystem snapshot import API.
- Database integrity result categories: valid empty, valid imported, corrupt, unsupported schema, incompatible partial state.
- Transaction wrapper for future domain stores.

## 12. Internal Contracts

- Import is idempotent when source snapshot has not changed.
- Imported identities exactly match snapshot identities; no reallocation during import.
- Schema version is checked before domain access.
- Failed import does not leave a database classified as valid imported state.

## 13. Data and State Model

**SQLite workspace database**
- Owner: SQLite workspace store
- Lifecycle: Initialized, validated, imported, later used by canonical domain stores.
- Durability: SQLite file in workspace-defined location.
- Mutability: Mutable only through versioned store APIs.
- Identity: Workspace root plus schema version.
- Validation: SQLite integrity, schema metadata, domain constraints.
- Recovery: Recreate from filesystem import while filesystem remains canonical.
- Consumers: Future SQLite domain stores and verification.
**Schema metadata**
- Owner: Schema migrator
- Lifecycle: Created at initialization and updated only by supported migrations.
- Durability: SQLite table.
- Mutability: Controlled by migrator.
- Identity: Schema version value.
- Validation: Supported version check.
- Recovery: Reinitialize or migrate through supported path.
- Consumers: Workspace open and integrity validation.
**Imported domain rows**
- Owner: Domain importers
- Lifecycle: Inserted from filesystem snapshots and later read by SQLite stores.
- Durability: SQLite tables.
- Mutability: Import idempotently replaces or verifies matching source.
- Identity: Domain natural keys.
- Validation: Domain constraints and snapshot equality.
- Recovery: Re-import from filesystem source.
- Consumers: Milestones 5-9.

## 14. Lifecycle and State Transitions

- Database: Missing -> Initialized -> Valid Empty -> Imported -> Valid Imported.
- Failure transitions: Initialized -> Corrupt if schema/constraints fail; Imported -> Unsupported if schema version is newer than runtime.
- Import: validate filesystem snapshot -> begin transaction -> insert/update domain rows -> compare database snapshot -> commit; failure rolls back.

## 15. Execution Flow

- Startup/open checks database existence, schema metadata, SQLite integrity, and supported version.
- Import flow reads filesystem snapshot through Milestone 2, opens transaction, writes rows, validates equality, commits.
- Failure flow rolls back transaction and reports domain/path/identity diagnostics.
- Recovery flow deletes/reinitializes only through explicit user action or re-imports while filesystem remains canonical.
- Normal workflows continue using file-backed stores.

## 16. Dependency Closure

- Hard prerequisite: Milestone 1 domain surface.
- Hard prerequisite: Milestone 2 import snapshots.
- Hard prerequisite: Milestone 3 logical path identity and hash policy.
- Supporting infrastructure: SQLite provider, transaction wrapper, schema metadata.
- Explicitly unavailable dependency: SQLite-canonical workflow writes.
- Enables Milestones 5, 6, 7, 8, 9, 11, 12, and 13.

## 17. Failure Modes

**Unsupported schema version**
- Description: Runtime opens a database created by a newer or incompatible schema.
- Detection: Schema metadata check.
- Behavior: Fail open/import safely without modifying database.
- Recovery: Use compatible runtime or explicit migration path.
- Diagnostics: Found version, supported version range.
- Tests: Manually seeded unsupported metadata.
**Duplicate imported identity**
- Description: Filesystem snapshot contains or maps to duplicate domain keys.
- Detection: Snapshot validation and database constraints.
- Behavior: Abort import and roll back.
- Recovery: Fix source export or importer mapping.
- Diagnostics: Domain and duplicate key.
- Tests: Duplicate source and constraint violation tests.
**Corrupt database**
- Description: Database file or rows fail SQLite/domain integrity checks.
- Detection: SQLite integrity check and domain validation.
- Behavior: Classify as corrupt and block canonical use.
- Recovery: Restore backup or recreate from filesystem source.
- Diagnostics: Integrity category and failing table/domain.
- Tests: Corrupt file and invalid row fixtures.

## 18. Validation and Invariants

**Import preserves all logical identities, sequence numbers, timestamps, hashes, and path references.**
- Source Authority: Milestone 4 acceptance criteria.
- Enforcement Point: Database snapshot equality after import.
- Failure Behavior: Import rolls back or validation fails.
- Test Strategy: Full fixture import comparison.
**Re-import is idempotent when source state has not changed.**
- Source Authority: Milestone 4 acceptance criteria.
- Enforcement Point: Import conflict/equality check.
- Failure Behavior: Second import modifies rows or reports conflict.
- Test Strategy: Run import twice and compare database snapshot/hash.
**Existing workflows still run against filesystem-backed persistence after database import.**
- Source Authority: Milestone 4 acceptance criteria.
- Enforcement Point: Workflow regression tests post-import.
- Failure Behavior: Workflow tests fail or use database accidentally.
- Test Strategy: Import database then run existing file-backed workflow tests.

## 19. Testing Strategy

- Unit tests for schema initialization, version checks, transaction rollback, and integrity categories.
- Integration tests importing valid full and empty filesystem snapshots.
- Contract tests comparing imported database snapshot to source filesystem snapshot.
- Failure-path tests for duplicate identities, corrupt database, unsupported schema, invalid partial state, and failed transaction rollback.
- Regression tests proving workflows remain file-backed after import.
- Performance smoke tests for importing large evidence/history fixtures.

## 20. Fixtures and Test Data

- Valid empty workspace.
- Valid full `.agents` filesystem snapshot.
- Duplicate identity snapshot.
- Unsupported schema database.
- Corrupt database file or invalid row set.
- Large history/evidence workspace.
- Mixed legacy markdown and JSON source workspace.

## 21. Acceptance Demonstration

- Create a temporary workspace with existing `.agents` machine-managed files.
- Run database initialization.
- Import filesystem snapshot into SQLite.
- Run integrity validation and snapshot equality comparison.
- Run an existing file-backed workflow test/command against the same workspace and confirm it still uses filesystem persistence.

## 22. Certification Evidence

- Database initialization and integrity test output.
- Import equality report for representative workspace.
- Idempotent re-import test output.
- Rollback evidence from induced import failure.
- Workflow regression output after database import.

## 23. Implementation Plan

**Add SQLite substrate**
- Purpose: Provide database file, connection, schema metadata, and transaction primitives.
- Deliverables: Workspace store and schema initializer.
- Dependencies: SQLite provider selection.
- Completion: Empty database validates.
**Define initial schema**
- Purpose: Represent imported migrated domain snapshots.
- Deliverables: Versioned tables and constraints.
- Dependencies: Domain snapshot identities.
- Completion: Schema enforces natural keys and referential basics.
**Implement importer**
- Purpose: Create database state from filesystem snapshots.
- Deliverables: Import pipeline and row mappers.
- Dependencies: Milestone 2 snapshots.
- Completion: Full fixture imports with equality.
**Implement integrity validation**
- Purpose: Classify database health.
- Deliverables: Validation categories and diagnostics.
- Dependencies: Schema and importer.
- Completion: Corrupt/unsupported/partial fixtures classified.

## 24. Parallel Work Opportunities

**SQLite substrate and versioning**
- Owner: Infrastructure engineer
- Dependencies: Provider choice.
- Sync: Schema table naming and transaction API.
- Risk: Provider options leak into domain stores.
**Domain row mapping**
- Owner: Domain persistence engineer
- Dependencies: Snapshot models.
- Sync: Schema natural keys.
- Risk: Identity-preservation bugs.
**Integrity test fixtures**
- Owner: Test engineer
- Dependencies: Schema draft.
- Sync: Validation categories.
- Risk: Fixtures depend on unstable schema internals.

## 25. Risks and Mitigations

**Database path/location ambiguity**
- Class: architectural
- Impact: CLIs disagree on database location or publishing behavior.
- Likelihood: medium
- Detection: Open question and integration tests.
- Mitigation: Centralize path selection in workspace store and do not publish policy yet.
- Fallback: Keep database optional/import-only until Milestone 11 policy.
**Import writes partial valid-looking state**
- Class: data
- Impact: Later milestones trust incomplete database.
- Likelihood: medium
- Detection: Rollback and integrity tests.
- Mitigation: Single transaction plus post-import equality check.
- Fallback: Mark database invalid and require re-import.
**SQLite schema overfits current files**
- Class: maintainability
- Impact: Later domain stores become hard to implement.
- Likelihood: medium
- Detection: Domain store design review and conformance tests.
- Mitigation: Schema follows domain identities, not file layout alone.
- Fallback: Add compatibility views/export mappings without changing domain contracts.

## 26. Observability and Diagnostics

- Integrity validation report includes database path, schema version, domain counts, and failing constraints.
- Import diagnostics include source snapshot hash/equality summary and per-domain row counts.
- Transaction failure diagnostics include the domain write phase and rolled-back status.

## 27. Performance and Scalability Considerations

- Baseline import should scale linearly with exported record count.
- Likely bottlenecks are bulk evidence bodies and transition JSONL import.
- Use batched inserts within a transaction and measure import duration/counts.
- Deferred optimization: incremental import and row-level change tracking.

## 28. Security and Safety Considerations

- Validate database path stays within workspace policy.
- Use parameterized SQLite writes; never compose SQL with imported artifact content.
- Treat imported files as untrusted data and validate before insertion.
- Do not delete or overwrite existing filesystem state during import.

## 29. Documentation Updates

- No documentation-only deliverable is required.
- If initialization or import is exposed through an executable command, command help must reflect actual safe-import behavior.

## 30. Exit Criteria

- SQLite workspace store initializes and validates empty database state.
- Filesystem snapshots import into SQLite with identity preservation.
- Re-import is idempotent.
- Integrity validation distinguishes valid empty, valid imported, corrupt, and unsupported-version states.
- Existing workflows continue to pass using file-backed persistence after import.

## 31. Transition to Next Milestone

- Milestone 5 receives a populated database substrate for core roadmap domains.
- Milestone 11 receives the initial import side of workspace synchronization.
- Remaining limitation: database rows are imported but not yet runtime authority.

## Open Implementation Questions

- The authoritative location of the SQLite database relative to `.agents` is unresolved by the roadmap and should be centralized in the workspace store without defining publication policy yet.
- Mixed-version CLI behavior is only fail-safe detection in this milestone; full compatibility policy belongs to Milestone 11.

<!-- GENERATED:END -->

<!-- MANUAL:START -->
<!-- Add manually maintained notes here. This block is preserved by the generator. -->
<!-- MANUAL:END -->
