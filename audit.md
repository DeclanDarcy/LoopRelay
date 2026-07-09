# `.LoopRelay` Runtime Persistence Architecture Audit

## Scope Finding

The corrected runtime root is `.LoopRelay`. Source code uses the casing `.LoopRelay`; on this Windows workspace that is equivalent to `.looprelay` for path resolution, but the source-defined path casing is `.LoopRelay`.

Current on-disk state at audit time:

- `.LoopRelay/` is absent.
- Root `.gitignore` ignores `.LoopRelay/`.

This audit is source-defined rather than sample-data-derived. It inventories only runtime-managed persistent data under `.LoopRelay` and intentionally omits other artifact roots.

## Evidence Base

Primary source files audited:

- `src/LoopRelay.Orchestration.Primitives/Services/DecisionSessionResumeStore.cs`
- `src/LoopRelay.Orchestration.Primitives/Models/DecisionSessionResumeState.cs`
- `src/LoopRelay.Cli/Services/Telemetry/SessionTelemetryComposition.cs`
- `src/LoopRelay.Cli/Services/Telemetry/SessionTelemetryRecorder.cs`
- `src/LoopRelay.Cli/Services/Telemetry/RotatingJsonlTelemetrySink.cs`
- `src/LoopRelay.Cli/Models/SessionTelemetryRecord.cs`
- `src/LoopRelay.Cli/Models/SessionTelemetryJson.cs`
- `src/LoopRelay.Cli/Services/Telemetry/InputWaitObservationStore.cs`
- `src/LoopRelay.Cli/Services/Cli/LoopCliComposition.cs`
- `src/LoopRelay.Cli/Services/Decisions/DecisionSession.cs`
- `src/LoopRelay.Cli/Services/Decisions/DecisionResumeComposition.cs`
- `src/LoopRelay.Cli/Services/Execution/SqliteLoopHistoryStore.cs`
- `src/LoopRelay.Core/Services/Persistence/SqliteExecutionEvidenceStore.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Persistence/WorkspaceSqlitePersistence.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Persistence/WorkspaceSyncService.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Persistence/WorkspaceVerificationService.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Persistence/WorkflowPersistenceCoordinator.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Cli/RoadmapCliComposition.cs`
- `src/LoopRelay.Completion/Services/ArtifactStorage/SqliteCompletedEpicArchiveMaterializer.cs`
- `src/LoopRelay.Completion/Services/ArtifactStorage/CompletionLogicalArtifactServices.cs`

## Complete Persistence Inventory

### `.LoopRelay/`

- Purpose: local runtime persistence root.
- Classification: runtime workspace, local-only persistence container, coordination boundary.
- Producer: loop CLI startup, decision-session resume store, telemetry sink, SQLite workspace store.
- Consumer: runtime persistence components and SQLite-backed services.
- Lifecycle: created lazily when runtime state, telemetry, or database storage is initialized.
- Creation trigger: `FileDecisionSessionResumeStore.EnsureDirectoryProtection`, decision resume write, telemetry append, or database initialization/import/write.
- Update trigger: child artifact updates.
- Deletion trigger: none in application code.
- Expected durability: local process-restart durability; ignored by git.
- Expected size: sum of child state, logs, and database files.
- Write frequency: depends on child artifacts.
- Read frequency: startup and runtime operations.
- Append vs overwrite: mixed by child artifact.
- Ordering: only child artifact ordering matters.
- Historical versions: only child artifacts carry history.
- Concurrency expectations: no root-level lock; child components own their own behavior.
- Crash recovery expectations: missing root is tolerated and recreated by producers.
- SQLite fit: not itself database data; remains a filesystem directory.

### `.LoopRelay/.gitignore`

- Purpose: self-ignore the local runtime root even if the target repository root ignore file is absent or incomplete.
- Classification: coordination data, filesystem guardrail, developer convenience.
- Producer: `FileDecisionSessionResumeStore.EnsureSelfIgnore`.
- Consumer: git and working-tree change detection behavior.
- Lifecycle: create-only.
- Creation trigger: loop CLI startup through `EnsureDirectoryProtection`, or before writing decision-session resume state.
- Update trigger: none in code.
- Deletion trigger: none in code.
- Expected durability: durable local configuration.
- Expected size: one line, `*`.
- Write frequency: at most once unless manually deleted.
- Read frequency: not read by application logic after the existence check; interpreted by git tooling.
- Append vs overwrite: create-only; an existing file is never overwritten.
- Ordering: none.
- Historical versions: not meaningful.
- Concurrency expectations: benign create race; no explicit lock.
- Crash recovery expectations: if absent, startup attempts to recreate it.
- SQLite fit: not database data. It remains filesystem-backed because its behavior is defined by filesystem/git semantics.

### `.LoopRelay/decision-session.json`

- Purpose: persist decision-session resume state across process restarts.
- Classification: resumability data, operational metadata, machine-required runtime state.
- Producer: `DecisionSession.PersistResumeStateAsync` through `FileDecisionSessionResumeStore.WriteAsync`.
- Consumer: `DecisionSession.OpenOrResumeSessionAsync` through `FileDecisionSessionResumeStore.ReadAsync`.
- Lifecycle: singleton latest-state file.
- Creation trigger: after a successful decision proposal.
- Update trigger: after each successful proposal, with `SavedAtUtc` updated to UTC now.
- Deletion trigger: transfer recycle, failed turn cleanup, stale or missing decision projection, failed resume, invalid content, corrupt content, or explicit clear.
- Expected durability: local restart durability only; not required for correctness because the store is fail-open.
- Expected size: tiny JSON document.
- Write frequency: once per successful decision proposal.
- Read frequency: first decision-session open when resume is enabled.
- Append vs overwrite: overwrite.
- Ordering: latest value only.
- Historical versions: not used.
- Concurrency expectations: single runtime writer is assumed; no file lock or transaction.
- Crash recovery expectations: missing file returns null. Schema mismatch, null state, blank thread id, read failure, or invalid content emits a warning, deletes the file, and resumes cold.
- SQLite fit: strong. This is structured singleton runtime state with clear lifecycle, overwrite semantics, and recovery rules. It is not currently stored in SQLite.

Persisted fields:

- `schemaVersion`
- `threadId`
- `occupancyTokens`
- `reuseCost`
- `reuseCycles`
- `lastCycleCost`
- `prevCycleCost`
- `transferCost`
- `transferCount`
- `previousOperationalContextSize`
- `operationalContextGrowthStreak`
- `savedAtUtc`

Behavioral switches:

- `LoopRelay_DECISION_RESUME=0` disables resume attempts.
- `LoopRelay_DECISION_RESUME=false` disables resume attempts.
- Disabling resume does not change write/clear behavior elsewhere.

### `.LoopRelay/telemetry/`

- Purpose: telemetry log directory.
- Classification: telemetry container, diagnostics container.
- Producer: `SessionTelemetryComposition` creates a `RotatingJsonlTelemetrySink` rooted at this directory when telemetry is enabled.
- Consumer: telemetry sink and external diagnostics/visualizer tooling implied by source comments.
- Lifecycle: created lazily on first append.
- Creation trigger: first telemetry append in a telemetry-enabled run.
- Update trigger: child JSONL file append/rotation.
- Deletion trigger: none in telemetry sink.
- Expected durability: local diagnostic retention.
- Expected size: unbounded by the sink; pruning is delegated outside the sink.
- Write frequency: per recorded turn.
- Read frequency: not read by core runtime.
- Append vs overwrite: child files are append-only.
- Ordering: child filenames and line order matter.
- Historical versions: telemetry history is diagnostic.
- Concurrency expectations: sink uses in-process locking, not cross-process locking.
- Crash recovery expectations: telemetry is fail-open; telemetry faults warn and do not fail a turn.
- SQLite fit: directory remains filesystem if JSONL compatibility is retained; contained events are database-suitable telemetry records.

### `.LoopRelay/telemetry/sessions.YYYY-MM-DD.NNNN.jsonl`

- Purpose: compact per-turn session telemetry records.
- Classification: telemetry, diagnostics, event log.
- Producer: `SessionTelemetryRecorder` builds `SessionTelemetryRecord`; `RotatingJsonlTelemetrySink.Append` serializes and appends each record.
- Consumer: external diagnostics/visualizer tooling; tests exercise the sink and record shape.
- Lifecycle: append-only file per UTC date and sequence, rotated by size and date.
- Creation trigger: first append for a UTC date/sequence.
- Update trigger: one append per successful telemetry recording.
- Deletion trigger: none in sink. Root `.gitignore` comment states a visualizer manages pruning.
- Expected durability: local diagnostic history.
- Expected size: each file rotates at 5 MiB by default.
- Write frequency: hot path; at most once per recorded turn.
- Read frequency: external diagnostics; not read by runtime control flow.
- Append vs overwrite: append-only.
- Ordering: line order is temporal within a file; filename date and sequence order matter across files.
- Historical versions: useful for diagnostics and trends; not required for workflow recovery.
- Concurrency expectations: in-process `lock` around directory creation, active-file resolution, and append; no inter-process mutex.
- Crash recovery expectations: telemetry failures are swallowed after warning. A truncated or malformed final line is diagnostic corruption, not control-plane corruption.
- SQLite fit: strong for queryability, indexing, retention policies, and partial-line avoidance. The current filesystem contract remains JSONL files with external pruning.

Persisted telemetry fields include:

- Timestamp and repository name.
- Codex log path.
- Session id and session type.
- Turn index.
- Prompt, output, cached, and effective token counts.
- Capacity percentages.
- Input-wait status, timing, attempt counts, model, and retry-after data.

Behavioral switches:

- `LoopRelay_SESSION_LOG=0` disables telemetry.
- `LoopRelay_SESSION_LOG=false` disables telemetry.

### `.LoopRelay/persistence/`

- Purpose: SQLite database directory.
- Classification: database container.
- Producer: workspace database locator/store, loop history database checks, execution evidence database store.
- Consumer: SQLite-backed runtime stores and verification/sync components.
- Lifecycle: created when the database is initialized/imported or when SQLite-backed stores create/open the database.
- Creation trigger: storage initialization/import or SQLite domain-store writes.
- Update trigger: child database and possible SQLite sidecar files.
- Deletion trigger: none in application code.
- Expected durability: local durable database storage.
- Expected size: database-dependent.
- Write frequency: depends on SQLite-backed runtime flows.
- Read frequency: startup validation and runtime SQLite reads.
- Append vs overwrite: database-managed.
- Ordering: database-managed.
- Historical versions: database rows carry history.
- Concurrency expectations: SQLite owns file locking below this directory.
- Crash recovery expectations: database validation classifies missing, corrupt, incompatible, and valid states.
- SQLite fit: remains filesystem directory containing the database file.

### `.LoopRelay/persistence/looprelay.sqlite3`

- Purpose: project SQLite database.
- Classification: canonical/imported database, runtime persistence store, workflow recovery store.
- Producer: `WorkspaceSqliteStore`, SQLite domain stores, `SqliteLoopHistoryStore`, `SqliteExecutionEvidenceStore`, workflow persistence coordinator.
- Consumer: runtime composition, SQLite stores, storage sync/export/verify, logical artifact providers, completion archive materialization.
- Lifecycle: created by storage initialization/import or SQLite-backed writes.
- Creation trigger: `storage-init`, `storage-import`, or a write path that opens the database in read-write-create mode.
- Update trigger: domain state updates, manifest updates, journal inserts, loop history inserts, execution evidence inserts, workflow transaction marker writes, sync marker updates.
- Deletion trigger: none in application code.
- Expected durability: durable local database.
- Expected size: grows with retained workflow state, journals, histories, evidence, archive metadata, and markers.
- Write frequency: medium to hot depending on runtime activity.
- Read frequency: high at startup and during runtime operations that use SQLite-backed stores.
- Append vs overwrite: mixed row insert, upsert, delete, and singleton replacement behavior.
- Ordering: explicit row columns encode ordering where needed.
- Historical versions: journals, histories, evidence, workflow transactions, and archive records preserve history; singleton rows preserve latest state.
- Concurrency expectations: short-lived SQLite connections with pooling disabled; SQLite file locking.
- Crash recovery expectations: validation checks schema version, persistence state, expected tables, readable rows, and JSON deserialization.
- SQLite fit: already SQLite.

### SQLite sidecar files under `.LoopRelay/persistence/`

- Purpose: possible SQLite engine sidecars depending on journal mode and platform behavior.
- Classification: database engine files, temporary/runtime database coordination.
- Producer: SQLite engine, not application code directly.
- Consumer: SQLite engine.
- Lifecycle: engine-managed.
- Creation trigger: SQLite write behavior if journal/WAL mode requires sidecar files.
- Update trigger: SQLite transactions.
- Deletion trigger: SQLite engine cleanup.
- Expected durability: engine-managed; not domain data independently.
- Expected size: workload-dependent.
- Write frequency: transaction-dependent.
- Read frequency: transaction-dependent.
- Append vs overwrite: engine-managed.
- Ordering: engine-managed.
- Historical versions: not separately meaningful.
- Concurrency expectations: SQLite engine locking.
- Crash recovery expectations: SQLite engine recovery.
- SQLite fit: remain filesystem sidecars because they are part of the database engine.

## SQLite Layer Audit

Database location:

- `.LoopRelay/persistence/looprelay.sqlite3`

Versioning:

- `WorkspaceSqliteStore.CurrentSchemaVersion = 1`.
- `schema_metadata` stores `schema_version`.
- `workspace_metadata` stores `persistence_state`.
- Observed persistence states are `empty`, `imported`, and `canonical`.

Connection model:

- Connections are short-lived.
- Read-write-create and read-only connection builders set `Pooling = false`.
- Schema creation enables foreign keys through `PRAGMA foreign_keys=ON`.
- SQLite provides file-level locking.

Current schema objects:

- `schema_metadata`
- `workspace_metadata`
- `sync_markers`
- `decision_ledger`
- `roadmap_state`
- `artifact_lifecycle`
- `split_families`
- `split_family_children`
- `split_family_dependency_order`
- `execution_preparation_manifest`
- `selection_provenance_manifest`
- `projection_manifest_entries`
- `transition_journal`
- `loop_history`
- `execution_evidence`
- `completed_epic_archives`
- `completed_epic_records`
- `workflow_transactions`

Current indexes:

- `idx_artifact_lifecycle_path_key`
- `idx_split_family_children_child_path`
- `idx_transition_journal_correlation_id`
- `idx_loop_history_kind_sequence_desc`
- `idx_execution_evidence_stem_sequence_desc`

Current transaction model:

- Storage import opens one transaction, clears selected domain tables, inserts rows, re-reads a snapshot, compares hashes, and commits only after validation.
- Sync marker writes run in one transaction.
- Loop history append computes next sequence and inserts inside one transaction.
- Execution evidence append computes next sequence and inserts inside one transaction.
- Many domain stores use transactions for singleton upserts, delete-insert replacement, and full saves.
- Workflow persistence coordinator records `Started`, `Completed`, and `Failed` markers in `workflow_transactions`, but marker writes are separate from the persistence work they surround.

Current validation model:

- Missing database returns a missing status.
- Unsupported schema version returns an incompatible/unsupported status.
- Valid empty database requires `persistence_state=empty` and no domain rows.
- Valid imported/canonical databases require known persistence states and readable tables.
- SQLite exceptions, JSON exceptions, and invalid operations classify as corrupt.
- Verification can check sync markers, missing exports, stale exports, unresolved output references, duplicate identities, archive recoverability, workflow transaction recovery, and optional full round trip.

Serialization approach:

- Structured documents are stored as JSON in text columns for singleton/domain documents.
- Some table columns store JSON arrays or snapshots as JSON text.
- Loop history and execution evidence store body text plus SHA-256 content hash.
- Workflow transaction markers store marker JSON.

## Data Relationships Within `.LoopRelay`

Observed database relationships:

- `schema_metadata` defines database schema compatibility.
- `workspace_metadata` defines database state classification.
- `sync_markers` relates logical domains to canonical/export hashes and generation.
- `decision_ledger` records decisions with prompt, transition, projection path, input paths, and output paths.
- `roadmap_state` stores a singleton state document.
- `artifact_lifecycle` indexes artifact status by normalized path key.
- `split_families`, `split_family_children`, and `split_family_dependency_order` encode parent-child and ordered dependency relationships.
- `projection_manifest_entries` is keyed by runtime prompt.
- `transition_journal` is ordered by `event_order` and indexed by `correlation_id`.
- `loop_history` is ordered by `(kind, sequence)` and uniquely maps each record to a logical path.
- `execution_evidence` is ordered by `(stem, sequence)` and uniquely maps each record to a logical path.
- `completed_epic_archives` and `completed_epic_records` relate archive ids to materialized records and content hashes.
- `workflow_transactions` records workflow unit, correlation id, status, timestamps, and marker payload.

Natural normalization already present:

- Ordered child lists are separated from split family records.
- Ordered transition events are separated from singleton state.
- Loop histories and execution evidence use sequence columns rather than filename parsing inside the database.
- Content hashes allow database read validation independent of filesystem metadata.
- Workflow transactions provide recovery markers separate from domain data.

## Filesystem Coupling Under `.LoopRelay`

Direct path assumptions:

- `.LoopRelay` directory name is hardcoded in `FileDecisionSessionResumeStore`.
- `decision-session.json` filename is hardcoded.
- `.LoopRelay/.gitignore` contents are hardcoded as `*`.
- Telemetry directory is hardcoded as `.LoopRelay/telemetry`.
- Telemetry file pattern is hardcoded as `sessions.{yyyy-MM-dd}.{sequence:D4}.jsonl`.
- SQLite database relative path is hardcoded as `.LoopRelay/persistence/looprelay.sqlite3` in multiple SQLite consumers.

File existence assumptions:

- Missing `.LoopRelay/decision-session.json` means no resumable session.
- Existing `.LoopRelay/.gitignore` is respected and not overwritten.
- Missing database means SQLite-backed workspace persistence is unavailable.
- A usable loop-history database requires the database file, supported schema version, accepted persistence state, and expected table.

Timestamp assumptions:

- No `.LoopRelay` component audited depends on file timestamps for correctness.

Append semantics:

- Telemetry appends one compact JSON line per record.
- SQLite appends/updates through database transactions.

Cleanup logic:

- Decision resume can delete its singleton file on invalid content or explicit clear.
- Telemetry sink never deletes.
- SQLite database cleanup is not performed by application code.

## Behavioral Contracts

Contracts observed for `.LoopRelay`:

- The root is local-only and ignored.
- Startup attempts to protect the root before telemetry or decision resume writes.
- Runtime state under the root must not affect normal working-tree change detection.
- Decision resume is fail-open.
- Telemetry is fail-open.
- SQLite validation decides whether SQLite-backed stores are usable.
- SQLite database path is stable and shared across roadmap, loop, evidence, verification, sync, and completion components.
- Telemetry record ordering is filename order plus JSONL line order.
- Decision resume stores only latest state.
- Database histories use explicit sequence/order columns.

Contracts to preserve as externally observable behavior:

- `.LoopRelay` path remains local runtime state.
- Telemetry disable values `0` and `false` remain honored.
- Decision resume disable values `0` and `false` remain honored.
- Telemetry failures do not fail turns.
- Decision resume read/write/clear failures do not fail turns.
- Invalid decision resume is deleted and ignored.
- SQLite validation statuses remain distinguishable.
- Existing database file remains readable by all current SQLite consumers.

## Transaction Boundaries

Decision resume:

- A write serializes the full state document and overwrites `decision-session.json`.
- Clear deletes only that file.
- No transaction spans decision resume and other persistence.

Telemetry:

- A record is serialized and appended as a single line.
- The sink locks in process around active-file selection and append.
- No transaction spans telemetry and workflow state.

SQLite storage import:

- Domain import is transactional.
- Tables for selected domains are cleared and repopulated.
- A post-write snapshot is compared against the source hash before commit.

SQLite append histories:

- Loop history append computes next sequence and inserts in one transaction.
- Execution evidence append computes next sequence and inserts in one transaction.

Workflow persistence:

- Workflow marker writes bracket logical persistence phases.
- `Started`, `Completed`, and `Failed` states are recorded in `workflow_transactions`.
- Started-without-completed is classified as retryable partial.
- Failed markers are classified as corrupt for recovery reporting.

## Runtime Characteristics

Hot write paths:

- Telemetry JSONL append per recorded turn.
- Decision resume overwrite per successful decision proposal.
- SQLite journal/history/evidence inserts during runtime transitions.
- SQLite workflow marker writes around coordinated persistence.

Hot read paths:

- Startup database validation.
- First decision-session resume read.
- SQLite-backed store selection.
- Latest history reads.
- Verification/sync reads.

Latency-sensitive behavior:

- Telemetry recording is designed not to block turn success.
- Decision resume is designed not to block turn success.
- SQLite validation happens during composition/startup and influences store selection.

Startup behavior:

- Loop CLI creates/protects `.LoopRelay`.
- Loop CLI chooses SQLite loop history/evidence stores only if the database is usable.
- Roadmap CLI validates the database before selecting SQLite-backed stores.

Shutdown behavior:

- No explicit telemetry flush beyond synchronous append was found.
- Decision-session disposal can preserve resume state for the next run.

Background persistence:

- No independent background persistence task was found.

## Recovery Semantics

Missing root:

- Recreated by producers.

Missing decision resume:

- Resume read returns null.

Invalid decision resume:

- Warning is emitted.
- File is deleted.
- Runtime continues without resume.

Decision resume write failure:

- Warning is emitted.
- Runtime continues.

Telemetry failure:

- Warning is emitted.
- Runtime continues, except caller cancellation propagates.

Telemetry partial write:

- Only diagnostics are affected. Runtime control state does not depend on reading the JSONL files.

Missing database:

- Validation reports missing.
- Store composition falls back where applicable.

Unsupported database:

- Validation reports unsupported/incompatible schema.
- SQLite stores are not selected for normal runtime composition.

Corrupt database:

- Validation reports corrupt on SQLite/JSON/invalid-operation failures.

Interrupted SQLite operations:

- SQLite transactions provide atomicity for the transactional operations listed above.
- Workflow markers can reveal incomplete higher-level persistence phases.

## Historical Retention

History currently retained under `.LoopRelay`:

- Telemetry records in rotated JSONL files.
- Database row histories for decision ledger, transition journal, loop history, execution evidence, completed archive records, sync markers, and workflow transactions.

History not retained:

- Decision resume keeps only latest state.
- `.LoopRelay/.gitignore` has no useful history.
- SQLite sidecar files do not represent independent history.

Diagnostic-only retention:

- Telemetry JSONL is diagnostic and pruning is external to the sink.

Runtime/query retention:

- Database journals, histories, evidence, archive records, and workflow transactions are queryable runtime persistence.

## Database Fitness

### Already database-backed

- `.LoopRelay/persistence/looprelay.sqlite3` and its schema.
- Workflow transaction markers.
- Sync markers.
- Transition journal rows.
- Loop history rows.
- Execution evidence rows.
- Domain singleton and manifest rows.
- Archive metadata rows.

### Strong database fit, currently filesystem-backed

- `decision-session.json`: structured singleton state with clear invalidation and overwrite semantics.
- Telemetry JSONL records: structured event records with ordering, retention, and query requirements.

### Filesystem-backed by nature

- `.LoopRelay/.gitignore`: git/filesystem coordination artifact.
- SQLite database file and possible SQLite sidecars: database engine files.
- Telemetry JSONL files if compatibility with existing visualizer/pruning workflows is required.

## Migration Complexity

| Artifact | Difficulty | Coupling | Implementation risk | Validation complexity | Rollback complexity | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| `.LoopRelay/.gitignore` | Low | Git ignore behavior | Low | Low | Low | Create-only one-line guard |
| `.LoopRelay/decision-session.json` | Low to medium | Decision resume open/write/clear paths | Medium | Low | Low | Tiny singleton with fail-open semantics |
| `.LoopRelay/telemetry/*.jsonl` | Medium | Telemetry sink, external visualizer/pruning | Medium | Medium | Medium | Hot append path, rotation, ordering |
| `.LoopRelay/persistence/looprelay.sqlite3` | Existing | All SQLite-backed runtime stores | Existing behavior | Existing validation | Existing export/sync mechanisms | Already database-backed |
| SQLite sidecars | Low | SQLite engine | Low | Low | Low | Engine-managed files |

## Remaining Filesystem Needs

Filesystem-backed artifacts that remain justified by observed behavior:

- `.LoopRelay/`: directory container.
- `.LoopRelay/.gitignore`: git ignore coordination.
- `.LoopRelay/persistence/looprelay.sqlite3`: the database file.
- SQLite sidecar files if created by the engine.
- `.LoopRelay/telemetry/*.jsonl` while external tooling expects files with this name/order/rotation contract.

## External Surface Area

Potentially observable `.LoopRelay` surfaces:

- `.LoopRelay/.gitignore`
- `.LoopRelay/decision-session.json`
- `.LoopRelay/telemetry/sessions.YYYY-MM-DD.NNNN.jsonl`
- `.LoopRelay/persistence/looprelay.sqlite3`
- Environment variable `LoopRelay_SESSION_LOG`
- Environment variable `LoopRelay_DECISION_RESUME`
- CLI storage validation/import/export/sync behavior
- Diagnostic visualizer/pruning workflow implied by comments

Compatibility considerations:

- Path casing in source is `.LoopRelay`.
- Telemetry filename format and line-delimited JSON shape are externally inspectable.
- Decision resume invalidation behavior is observable through warnings and cold-session fallback.
- Database schema version and validation statuses are observable through storage commands.

## Validation Inventory

Observable behavior to validate after any future change:

- `.LoopRelay` is created when runtime persistence needs it.
- `.LoopRelay/.gitignore` is created with `*` when absent.
- Existing `.LoopRelay/.gitignore` is not overwritten.
- Root runtime state remains ignored by git.
- Missing `decision-session.json` returns no resume state.
- Invalid `decision-session.json` is deleted and ignored.
- Decision resume write failure warns but does not fail the turn.
- Decision resume clear failure warns but does not fail the turn.
- `LoopRelay_DECISION_RESUME=0` disables resume attempts.
- `LoopRelay_DECISION_RESUME=false` disables resume attempts.
- Telemetry is enabled by default.
- `LoopRelay_SESSION_LOG=0` disables telemetry.
- `LoopRelay_SESSION_LOG=false` disables telemetry.
- Telemetry append writes one compact JSON object per line.
- Telemetry file rotation preserves UTC date and four-digit sequence naming.
- Telemetry failures warn but do not fail the turn.
- SQLite database path remains `.LoopRelay/persistence/looprelay.sqlite3`.
- SQLite validation recognizes missing, valid empty, valid imported, valid canonical, incompatible partial, unsupported schema, and corrupt states.
- SQLite-backed store selection still depends on database usability.
- Database sequence ordering remains stable for ordered histories.
- Database content-hash validation remains enforced where currently used.
- Workflow transaction recovery classifications remain observable.

## Audit Conclusion

The `.LoopRelay` runtime persistence root contains three application-managed persistence surfaces: decision-session resumability, telemetry JSONL logs, and the SQLite workspace database. The SQLite database already provides structured persistence, ordering, validation, transaction handling, and recovery markers for multiple runtime domains. The two filesystem-backed data surfaces with clear database fit are `decision-session.json` and telemetry JSONL records. The root directory, `.gitignore`, database file, SQLite sidecars, and any JSONL compatibility export remain filesystem-shaped by current behavior.
