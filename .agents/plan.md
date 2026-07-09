# SQLite-Backed `.agents` Persistence Implementation Plan

## Goal

Build a persistence architecture where human-facing `.agents` artifacts remain ordinary repository files, while machine-managed persistence domains use SQLite as canonical storage. Every migrated domain must still have deterministic, importable filesystem exports so Git review, backup, restore, debugging, legacy workspaces, and `.agents` submodule publishing continue to work.

The completed system must preserve:

- repo-relative path identity for every persisted reference;
- visible `DNNNN` and `NNNN` sequence identities;
- freshness behavior based on trustworthy canonical content hashes;
- deterministic filesystem export and import for every migrated domain;
- compatibility with existing legacy markdown import behavior where the code already supports it;
- live workflow files as live files;
- completed epic recoverability after histories and evidence move to SQLite.

Canonical database state lives at:

```text
.LoopRelay/persistence/looprelay.sqlite3
```

`.LoopRelay` is already workspace-local and self-ignored. The SQLite file is process state, not the Git-reviewable publication surface. The reviewable and portable surface remains the retained `.agents` files plus deterministic exports regenerated from SQLite.

## Storage Split

### Retained Filesystem Artifacts

These paths remain filesystem-backed. They are live prompt inputs, human/agent authored documents, retained workflow files, or implemented artifacts whose current behavior depends on direct markdown files:

| Path or pattern | Owner today | Required behavior |
|---|---|---|
| `.agents/specs/epic.md` | Plan/spec workflows | Retain as a file and hash through logical artifact resolution. |
| `.agents/specs/s{n}.md` | Plan/spec workflows | Retain as files; milestone spec freshness continues to use path and content hash. |
| `.agents/epic.md` | Roadmap active epic flow | Retain as active epic markdown. |
| `.agents/plan.md` | Plan/Main execution | Retain as execution plan markdown. |
| `.agents/details.md` | Main execution | Retain as optional plan addendum. |
| `.agents/operational_context.md` | Main loop transfer flow | Retain as live operational context. |
| `.agents/operational_delta.md` | Main loop transfer flow | Retain as live delta until rotated to history. |
| `.agents/decisions/decisions.md` | Main loop decision flow | Retain as live execution prompt decisions. |
| `.agents/handoffs/handoff.md` | Main loop execution flow | Retain as live handoff. |
| `.agents/milestones/m*.md` | Plan/Main execution | Retain as markdown checklist files. |
| `.agents/roadmap/*.md` | Roadmap source | Retain as roadmap source markdown. |
| `.agents/selection.md` | Roadmap selection output | Retain as markdown artifact; only selection provenance metadata migrates. |
| `.agents/core/roadmap-completion-context.md` | Completion context | Retain as markdown. |
| `.agents/ctx/0*.md` | Project Context | Retain as markdown source files. |
| `.agents/projections/*.md` | Projection bodies | Retain as projection body markdown; only manifest metadata migrates. |
| `.agents/evidence/audits/*` | Roadmap audit evidence | Retain unless a later change explicitly migrates it. |
| `.agents/evidence/blockers/*` | Roadmap blocker evidence | Retain unless a later change explicitly migrates it. |
| `.agents/evidence/evaluations/*` | Completion/evaluation evidence | Retain unless a later change explicitly migrates it. |
| `.agents/evidence/orchestration/*` | Orchestration evidence | Retain unless a later change explicitly migrates it. |
| `.agents/review/*` | Non-implementation review | Retain unless a later change explicitly migrates it. |

Do not migrate ambiguous or currently unimplemented patterns by inference. In particular, `.agents/core/0*.md` has no implemented producer or consumer in the current codebase, and `.agents/evals/*.md` is not the implemented evaluation evidence path.

### SQLite-Canonical Migrated Domains

These domains become SQLite-canonical and must export back to their existing filesystem-equivalent shapes:

| Domain | Current filesystem shape | Existing code owners | Canonical identity |
|---|---|---|---|
| Decision ledger | `.agents/decision-ledger.json`, legacy `.agents/decision-ledger.md` import | `DecisionLedgerStore` | `DNNNN` decision ID |
| Roadmap state | `.agents/state.json`, legacy `.agents/state.md` import | `RoadmapStateStore` | singleton current workspace state |
| Artifact lifecycle | `.agents/artifacts/lifecycle.json`, legacy markdown import | `ArtifactLifecycleStore` | case-insensitive artifact path |
| Split lineage | `.agents/splits/split-family-*.json`, legacy markdown import | `SplitFamilyStore` | family ID plus child path |
| Execution preparation provenance | `.agents/execution-preparation-manifest.json` | `ExecutionPreparationManifestStore` | artifact kind plus artifact identity |
| Selection provenance | `.agents/selection-provenance-manifest.json` | `SelectionProvenanceManifestStore` | selection cycle identity |
| Projection metadata | `.agents/projections/manifest.json`, legacy `.agents/projections/manifest.md` import | roadmap and projections `ProjectionManifestStore` | runtime prompt name |
| Transition journal | `.agents/journal/transitions.jsonl` | `TransitionJournalStore` | monotonic event order plus correlation ID |
| Decision history | `.agents/decisions/decisions.NNNN.md` | `LoopArtifacts` | sequence and logical path |
| Handoff history | `.agents/handoffs/handoff.NNNN.md` | `LoopArtifacts` | sequence and logical path |
| Operational delta history | `.agents/deltas/operational_delta.NNNN.md` | `LoopArtifacts` | sequence and logical path |
| Execution evidence | `.agents/evidence/execution/{stem}.NNNN.md` | `RoadmapArtifacts`, `CompletionArtifacts` | stem, sequence, logical path |
| Completed epic migrated state | materialized inside `.agents/archive/epics/{index}` | `CompletedEpicArchiveService` | archive index plus logical record paths |

## Current Code Seams

Important implementation locations in the current codebase:

- `src/LoopRelay.Core/Abstractions/Artifacts/IArtifactStore.cs` is file-shaped: `ExistsAsync`, `ReadAsync`, `WriteAsync`, `DeleteAsync`, `ListAsync`, and `ListDirectoriesAsync`.
- `src/LoopRelay.Core/Services/Artifacts/FileSystemArtifactStore.cs` provides atomic single-file writes, read caching, deserialization caching, and deterministic directory listing.
- `src/LoopRelay.Infrastructure/Services/Artifacts/RepositoryArtifactStore.cs` bridges repository-relative paths to absolute filesystem paths.
- `src/LoopRelay.Infrastructure/Services/Artifacts/NumberedArtifactSequence.cs` centralizes simple max-plus-one sequence allocation, but several domains still implement their own filename scan logic.
- `src/LoopRelay.Roadmap.Cli/Services/Artifacts/RoadmapArtifactPaths.cs` and `src/LoopRelay.Orchestration.Primitives/Services/OrchestrationArtifactPaths.cs` define most durable paths.
- `src/LoopRelay.Roadmap.Cli/Services/State/StructuredPersistence.cs` is the strict JSON store used by state, ledger, lifecycle, projection manifest, and split family persistence.
- `src/LoopRelay.Roadmap.Cli/Services/ExecutionPreparation/ExecutionPreparationManifestStore.cs` and `src/LoopRelay.Roadmap.Cli/Services/Decisions/SelectionProvenanceManifestStore.cs` intentionally load empty on malformed JSON.
- `src/LoopRelay.Roadmap.Cli/Services/TransitionState/TransitionJournalStore.cs` appends by reading, trimming, and rewriting the JSONL file.
- `src/LoopRelay.Cli/Services/Execution/LoopArtifacts.cs` owns live/history behavior for decisions, handoffs, and operational deltas.
- `src/LoopRelay.Completion/Services/ArtifactStorage/CompletionArtifacts.cs` has its own numbered evidence writer.
- `src/LoopRelay.Completion/Services/ArtifactStorage/CompletedEpicArchiveService.cs` archives by moving files and directories directly.
- `src/LoopRelay.Roadmap.Cli/Services/Projections/ProjectionManifestStore.cs` and `src/LoopRelay.Projections/Services/Manifests/ProjectionManifestStore.cs` duplicate projection manifest persistence behavior.
- `src/LoopRelay.Roadmap.Cli/Services/Cli/RoadmapCliComposition.cs` manually wires concrete stores, so storage mode changes must be composition-explicit.
- `src/LoopRelay.Roadmap.Cli/Primitives/State/RoadmapCliCommand.cs` currently supports only `Status`, `Run`, and `Unblock`; storage operations need new command/API surfaces.

## Global Implementation Rules

1. Workflow code must operate on domain stores and logical artifact services, not raw SQL rows or exported files.
2. SQLite writes must use parameterized commands only.
3. Database access must validate schema version before domain reads or writes.
4. Import never reallocates identities that already exist in filesystem exports.
5. Export never mutates retained live files.
6. Exported files are deterministic import sources, not runtime authority after a domain becomes SQLite-canonical.
7. Missing or malformed persisted state keeps the domain-specific behavior that exists today:
   - strict structured stores throw on malformed canonical JSON;
   - execution preparation and selection provenance manifests load empty in compatibility/import mode.
8. Markdown history and evidence bodies are opaque text and must not be reformatted during import/export.
9. Normal verification is read-only. Repair, re-export, or force reconciliation must be explicit.
10. Do not hold SQLite transactions open while Codex or any long-running agent process is executing.

## New Shared Capabilities

### SQLite Substrate

Add a SQLite substrate under infrastructure/persistence code and wire it through CLI compositions:

- Add `Microsoft.Data.Sqlite` through central package management.
- Add a workspace database locator that resolves `.LoopRelay/persistence/looprelay.sqlite3`.
- Add schema metadata and validation.
- Add transaction wrapper APIs.
- Add read-only open mode for verification.
- Add integrity result categories:
  - `ValidEmpty`
  - `ValidImported`
  - `ValidCanonical`
  - `Corrupt`
  - `UnsupportedSchema`
  - `IncompatiblePartialState`

Initial schema tables:

```text
schema_metadata(key text primary key, value text not null)
workspace_metadata(key text primary key, value text not null)
sync_markers(domain text primary key, canonical_hash text not null, export_hash text, generation integer not null, updated_at text not null)
decision_ledger(decision_id text primary key, timestamp text not null, state text not null, transition text not null, prompt text not null, projection_path text not null, input_paths_json text not null, output_paths_json text not null, decision text not null, confidence text not null, rationale_excerpt text not null)
roadmap_state(id integer primary key check (id = 1), document_json text not null, updated_at text not null)
artifact_lifecycle(path_key text primary key, path text not null, state text not null, updated_at text not null, notes text not null)
split_families(family_id text primary key, proposal text not null, selected_child text not null, selected_child_rationale text not null, created_at text not null)
split_family_children(family_id text not null, ordinal integer not null, child_path text not null, primary key(family_id, ordinal), unique(family_id, child_path))
split_family_dependency_order(family_id text not null, ordinal integer not null, child_path text not null, primary key(family_id, ordinal))
execution_preparation_manifest(id integer primary key check (id = 1), document_json text not null, updated_at text not null)
selection_provenance_manifest(id integer primary key check (id = 1), document_json text not null, updated_at text not null)
projection_manifest_entries(runtime_prompt text primary key, document_json text not null, updated_at text not null)
transition_journal(event_order integer primary key autoincrement, correlation_id text not null, event_name text not null, recorded_at text not null, from_state text not null, to_state text not null, transition text not null, projection_path text not null, prompt_contract text not null, input_hashes_json text not null, output_paths_json text not null, retry_count integer not null, result text not null, decision text not null, error text, input_snapshot_json text)
loop_history(kind text not null, sequence integer not null, logical_path text not null unique, body text not null, content_hash text not null, created_at text not null, primary key(kind, sequence))
execution_evidence(logical_path text primary key, stem text not null, sequence integer not null, body text not null, content_hash text not null, created_at text not null, writer text, metadata_json text not null, unique(stem, sequence))
completed_epic_archives(archive_index integer primary key, archive_directory text not null unique, synthesis_path text not null unique, created_at text not null, metadata_json text not null)
completed_epic_records(archive_index integer not null, domain text not null, logical_path text not null, export_path text not null, content_hash text not null, primary key(archive_index, domain, logical_path))
workflow_transactions(transaction_id text primary key, workflow_name text not null, correlation_id text not null, status text not null, started_at text not null, completed_at text, marker_json text not null)
```

Store JSON blobs where current domain models are already well validated and where normalization would create risk. Add indexes for common lookup paths:

- `artifact_lifecycle(path_key)`
- `split_family_children(child_path)`
- `transition_journal(correlation_id)`
- `loop_history(kind, sequence desc)`
- `execution_evidence(stem, sequence desc)`

### Domain Contracts

Introduce concrete contracts before storage migration. Use interfaces that express domain behavior, not file operations:

- `IDecisionLedgerStore`
- `IRoadmapStateStore`
- `IArtifactLifecycleStore`
- `ISplitFamilyStore`
- `IExecutionPreparationManifestStore`
- `ISelectionProvenanceManifestStore`
- `IProjectionManifestStore`
- `ITransitionJournalStore`
- `ILoopHistoryStore`
- `IExecutionEvidenceStore`
- `IWorkspaceSnapshotImporter`
- `IWorkspaceSnapshotExporter`
- `ILogicalArtifactResolver`
- `ICanonicalArtifactHasher`
- `IWorkspaceSyncService`
- `IWorkspaceVerificationService`
- `IWorkflowPersistenceCoordinator`

Keep file-backed implementations for every contract first. Then add SQLite implementations domain by domain.

### Snapshot and Export Model

Each migrated domain needs:

- typed snapshot model;
- logical equality rules;
- filesystem importer;
- filesystem exporter;
- canonical hash calculation;
- validation errors with domain, path, identity, and reason.

Export rules:

- Strict JSON domains use the current indented JSON options and end with one newline.
- JSONL journal export writes one record per line in event order.
- Markdown histories and execution evidence preserve exact body text.
- Per-family split export preserves `split-family-{familyId}.json`.
- Sequence filenames preserve imported numeric suffixes.
- Export/import/export stability is required unless a domain explicitly marks a metadata field non-canonical.

### Logical Artifact Resolution

Add a resolver that maps repo-relative paths to content regardless of backing store:

- retained files resolve through `IArtifactStore`;
- migrated structured exports resolve through domain canonical serialization;
- loop histories resolve through `ILoopHistoryStore`;
- execution evidence resolves through `IExecutionEvidenceStore`;
- projection bodies remain filesystem-backed and resolve through `IArtifactStore`;
- unresolved paths return typed statuses such as missing retained file, missing migrated record, wrong domain, stale, invalid, or blocked.

Update these consumers to use the resolver or canonical hasher before any content domain migrates:

- `TransitionInputAccumulator`
- `TransitionInputResolver`
- `ExecutionPreparationProvenanceService`
- `SelectionProvenanceService`
- `RoadmapPromptContextBuilder`
- `RoadmapUnblockPlanner`
- `InvariantValidator`
- completion context/evaluation readers that consume evidence paths

## CLI and API Surface

Add explicit storage operations to Roadmap CLI. Extend `RoadmapCliInvocation` with storage options and update `CliArguments`.

Required command shapes:

```text
LoopRelay.Roadmap.Cli storage-init <REPO_DIR>
LoopRelay.Roadmap.Cli storage-import <REPO_DIR> [--domain DOMAIN]
LoopRelay.Roadmap.Cli storage-export <REPO_DIR> [--domain DOMAIN] [--force]
LoopRelay.Roadmap.Cli storage-sync <REPO_DIR> [--domain DOMAIN] [--force-import|--force-export]
LoopRelay.Roadmap.Cli storage-verify <REPO_DIR> [--domain DOMAIN] [--full-roundtrip]
```

Normal `status`, `run`, and `unblock` must not silently import stale filesystem exports over canonical database state.

Storage result categories:

- `Initialized`
- `Imported`
- `Exported`
- `Unchanged`
- `StaleExport`
- `Conflict`
- `UnsupportedVersion`
- `ValidationFailure`
- `VerificationFailed`

## Milestone 1: File-Backed Domain Persistence Surface

### Objective

Move current behavior behind semantic domain contracts while files remain canonical.

### Implementation

1. Add contracts for all migrated domains.
2. Implement file-backed adapters that delegate to current stores/helpers.
3. Replace direct persistence semantics in callers with domain operations where the behavior belongs to a migrated domain.
4. Keep retained live file reads/writes as file operations.
5. Add conformance tests that freeze current behavior.

### Code Impact

- Wrap `DecisionLedgerStore`, `RoadmapStateStore`, `ArtifactLifecycleStore`, `SplitFamilyStore`, `ExecutionPreparationManifestStore`, `SelectionProvenanceManifestStore`, `ProjectionManifestStore`, and `TransitionJournalStore` behind interfaces.
- Extract loop history behavior out of `LoopArtifacts` into a history store/facade while preserving live-file methods.
- Extract numbered execution evidence behavior out of `RoadmapArtifacts.WriteNumberedEvidenceAsync` and `CompletionArtifacts.WriteNumberedEvidenceAsync`.
- Update `RoadmapCliComposition` and Main CLI composition to construct contract-based services.

### Tests

- Sequence allocation for decisions, handoffs, deltas, and evidence.
- Live-first read for decisions and handoffs.
- Strict JSON malformed behavior.
- Empty-on-malformed execution/selection manifest behavior.
- Split family legacy markdown migration.
- Journal started/completed/failed append compatibility.

### Exit Criteria

- Existing workflows pass with file-backed persistence.
- Migrated-domain behavior is available through semantic contracts.
- No SQLite schema or canonical database behavior is introduced.

## Milestone 2: Lossless Filesystem Serialization

### Objective

Make filesystem import/export a first-class capability for every migrated domain while files are still canonical.

### Implementation

1. Add immutable snapshot models for every migrated domain.
2. Add domain importers from current filesystem shapes.
3. Add deterministic domain exporters to current filesystem shapes.
4. Add workspace snapshot aggregate for all domains.
5. Add validation for duplicate, malformed, missing, partial, and invalid sequence state.

### Tests

- Full `.agents` tree import to workspace snapshot.
- Snapshot export to clean `.agents` tree.
- Export/import/export stability for stable domains.
- Duplicate `DNNNN`, duplicate `NNNN`, duplicate lifecycle path, duplicate runtime prompt, duplicate family ID.
- Optional missing execution/selection manifests load empty.
- Legacy markdown-only fixtures for stores that currently support legacy migration.

### Exit Criteria

- Every migrated domain supports import and export.
- Stable domains are byte-stable after filesystem to snapshot to filesystem.
- Identity-preserving markdown histories and evidence preserve path, sequence, and body.

## Milestone 3: Logical Artifact Identity and Freshness Resolution

### Objective

Resolve content and hashes by logical repo-relative path independent of physical storage.

### Implementation

1. Add `LogicalArtifactDescriptor`, `LogicalArtifactContent`, and `LogicalArtifactResolutionResult`.
2. Add resolver providers for retained filesystem files and file-backed migrated domains.
3. Add canonical hash service using retained file bytes or canonical export-equivalent migrated content.
4. Update freshness and prompt consumers to use logical resolution for any path that can become SQLite-backed.
5. Keep missing-path behavior domain-specific.

### Code Impact

- Replace direct `RoadmapArtifacts.ReadAsync(path)` hashing in `TransitionInputAccumulator`.
- Replace `ExecutionPreparationProvenanceService.CaptureDecisionLedgerInputAsync` file hash of `decision-ledger.json` with canonical decision ledger hash.
- Update completion evaluation context construction to resolve execution evidence through the resolver.
- Update unblock evidence hashing for execution evidence and migrated histories.

### Tests

- Retained spec, active epic, plan, operational context, live decision, and live handoff resolve from disk.
- Historical decision/handoff/delta paths resolve after import.
- Execution evidence paths resolve from file-backed evidence store.
- Hash drift in retained and migrated domains reports stale.
- Missing migrated evidence reports stale, invalid, or blocked according to consumer behavior.

### Exit Criteria

- File-backed freshness results match current tests.
- All path references that may later point to SQLite-backed records resolve through the logical resolver.

## Milestone 4: SQLite Workspace Store and Importable Database State

### Objective

Introduce SQLite initialization, versioning, integrity validation, transactions, and snapshot import without changing runtime workflow authority.

### Implementation

1. Add SQLite package and workspace database locator.
2. Create schema metadata and initial tables.
3. Implement schema migrator and integrity validator.
4. Implement filesystem snapshot import into SQLite in one transaction.
5. Compare imported database snapshot to filesystem snapshot before classifying as valid.
6. Add `storage-init` and `storage-import` command behavior.

### Tests

- Missing database initializes to valid empty state.
- Full filesystem snapshot imports to logically equivalent database.
- Re-import with unchanged source is idempotent.
- Import failure rolls back.
- Unsupported schema version blocks access without mutation.
- Corrupt database and invalid row fixtures classify correctly.
- Existing workflows still run file-backed after database import.

### Exit Criteria

- SQLite database can be initialized, imported, and validated.
- Workflows still use file-backed stores.
- No exported files are deleted or treated as projections yet.

## Milestone 5: Core Roadmap State Runs from SQLite

### Objective

Make decision ledger, roadmap state, artifact lifecycle, and split lineage SQLite-canonical.

### Implementation

1. Implement SQLite-backed stores for:
   - decision ledger;
   - roadmap state;
   - artifact lifecycle;
   - split lineage.
2. Route Roadmap CLI composition to SQLite stores when database mode is active.
3. Make decision append and next `DNNNN` allocation transaction-safe.
4. Enforce case-insensitive lifecycle path uniqueness.
5. Make split lookup by child path read SQLite, not filesystem globs.
6. Export deterministic equivalents:
   - `.agents/decision-ledger.json`
   - `.agents/state.json`
   - `.agents/artifacts/lifecycle.json`
   - `.agents/splits/split-family-*.json`

### Code Impact

- `RoadmapTransitionPersistence.CaptureSummaryAsync` must use canonical stores for last decision ID and split family count.
- Legacy markdown import is allowed only during explicit import, not normal SQLite runtime.
- Stale filesystem JSON must not override database state.

### Tests

- Delete exported core JSON files, load state from SQLite, regenerate exports.
- Append decisions after imported `D0003` and verify next ID is `D0004`.
- Lifecycle upsert rejects duplicate case variants.
- Split child lookup works with only SQLite rows.
- Exported core files import into a clean equivalent database.

### Exit Criteria

- Core structured machine state is SQLite-canonical.
- Regenerated exports can be deleted and restored without logical loss.

## Milestone 6: Provenance and Projection Metadata Run from SQLite

### Objective

Make execution preparation provenance, selection provenance, and projection manifest metadata SQLite-canonical while preserving freshness.

### Implementation

1. Implement SQLite-backed execution preparation manifest store.
2. Implement SQLite-backed selection provenance manifest store.
3. Implement SQLite-backed projection manifest store keyed by runtime prompt name.
4. Consolidate duplicated projection manifest behavior so Roadmap and Projections use one canonical contract or conformance suite.
5. Keep projection body markdown files on disk.
6. Preserve empty-on-malformed compatibility only at import/export boundaries for execution and selection manifests.

### Code Impact

- `ExecutionPreparationProvenanceService` reads/writes SQLite metadata and hashes retained inputs through the logical hasher.
- `SelectionProvenanceService` evaluates drift from SQLite metadata and logical input snapshots.
- `ProjectionCache` and `ProjectContextProjectionService` observe the same manifest semantics.

### Tests

- Freshness parity for retained-file drift, decision ledger drift, retired epic drift, projection body drift, and missing projection bodies.
- Malformed exported execution/selection manifests load empty during compatibility import.
- Projection manifest upsert by runtime prompt replaces existing metadata.
- Roadmap and Projections project tests pass against the same behavior.
- Metadata export/import into clean database preserves logical equality.

### Exit Criteria

- Provenance and projection metadata are SQLite-canonical.
- Projection body content remains filesystem-backed.
- Freshness decisions match file-backed behavior.

## Milestone 7: Transition Journal Runs from SQLite with JSONL Interchange

### Objective

Make transition chronology SQLite-canonical while preserving ordered JSONL import/export and legacy records without input snapshots.

### Implementation

1. Implement SQLite journal append with monotonic `event_order`.
2. Preserve correlation IDs, event kind, states, transition, projection, input hashes, output paths, result, decision, error, and optional input snapshot.
3. Import legacy JSONL lines, including records without input snapshots.
4. Export deterministic JSONL ordered by `event_order`.
5. Route transition runner and state-machine journal writes through the SQLite store.

### Tests

- Started/completed/failed event order is stable.
- Legacy no-snapshot records import.
- JSONL export imports into a clean equivalent database.
- Concurrent append smoke test.
- Verification hook reports unresolved output paths without mutating journal rows.

### Exit Criteria

- Journal runtime authority is SQLite.
- JSONL remains an importable/exportable debugging surface.

## Milestone 8: Loop Histories Move to SQLite While Live Files Stay on Filesystem

### Objective

Move historical decisions, handoffs, and operational deltas to SQLite while retaining live files and live-first behavior.

### Implementation

1. Implement `ILoopHistoryStore` for decision, handoff, and operational delta histories.
2. Adapt `LoopArtifacts` into a live/history facade:
   - live `decisions.md`, `handoff.md`, and `operational_delta.md` remain filesystem files;
   - numbered histories are SQLite rows;
   - latest reads check live file first, then highest SQLite sequence.
3. Preserve rotation ordering:
   - write SQLite history before deleting live file;
   - keep live file when history write fails.
4. Export/import numbered markdown histories.

### Tests

- Decision proposal writes live decisions file and SQLite `decisions.NNNN.md` history.
- Execution handoff rotates into SQLite before next decision.
- Operational delta transfer writes live delta, evolves context, then rotates into SQLite.
- Latest read prefers live files.
- Export/import preserves sequences and markdown bodies.
- Injected history write failure keeps live file available.

### Exit Criteria

- Histories are SQLite-canonical.
- Live files remain filesystem-backed and live-first.
- Completion archive support is not claimed until the next milestone.

## Milestone 9: Execution Evidence Moves to SQLite with Path-Compatible Access

### Objective

Move `.agents/evidence/execution/*` to SQLite while preserving path-compatible evidence reads, sequence allocation, search, prompt consumption, completion evaluation, and export/import.

### Implementation

1. Implement `IExecutionEvidenceStore` with write, read by logical path, search, allocation, import, export, and hash validation.
2. Route `RoadmapArtifacts.WriteNumberedEvidenceAsync` and `CompletionArtifacts.WriteNumberedEvidenceAsync` through the evidence store only for `.agents/evidence/execution`.
3. Keep non-execution evidence directories filesystem-backed.
4. Update consumers:
   - `RoadmapExecutionBridge`
   - `CompletionCertificationService`
   - `TransitionInputResolver`
   - `RoadmapPromptContextBuilder`
   - `RoadmapUnblockPlanner`
   - completion context builders
5. Ensure consumers pass when exported evidence files are deleted but SQLite rows exist.

### Tests

- Existing stem `execution-trust-posture.0003.md` imports and next write allocates `0004`.
- Prompt context reads SQLite-backed execution evidence.
- Unblock planner searches or hashes SQLite-backed evidence.
- Completion evaluation consumes SQLite-backed claim evidence.
- Missing referenced evidence maps to existing stale/invalid/blocked behavior.
- Export/import preserves body, path, stem, sequence, and hash.

### Exit Criteria

- Execution evidence is SQLite-canonical.
- All path-compatible consumers work without physical evidence export files.

## Milestone 10: Completed Epic Archives Preserve DB-Backed Historical State

### Objective

Make completed epic archives recover histories and execution evidence after those records move to SQLite.

### Implementation

1. Add archive association logic that selects DB-backed decisions, handoffs, deltas, and execution evidence for the completed epic.
2. Use persisted state, journal output paths, transition intents, and completion context to determine associations.
3. Materialize associated DB-backed records into deterministic archive filesystem form.
4. Preserve retained file archive behavior exactly where files remain filesystem-backed.
5. Add archive import/recovery that reconstructs logical archived state without promoting it to active workspace state.
6. Use staging before destructive retained-file moves.

### Code Impact

- Refactor `CompletedEpicArchiveService` so it no longer assumes history/evidence directories are canonical file directories.
- Add storage-neutral archive provider interfaces in `LoopRelay.Completion.Abstractions` and wire SQLite implementations in CLI compositions.
- Update completed epic evidence loaders to understand archive metadata when present.

### Tests

- Archive includes DB-backed decisions, handoffs, deltas, and execution evidence.
- Retained plan/context/milestones/review artifacts archive as before.
- Missing migrated record fails archive instead of silently dropping it.
- Archive path collisions abort before overwrite.
- Exported archive imports into a clean recovery context with equivalent archived state.

### Exit Criteria

- Completed epics remain recoverable with DB-backed histories/evidence.
- Active workspace state and archived state remain distinct.

## Milestone 11: Bidirectional Workspace Synchronization

### Objective

Provide full and domain-scoped synchronization between canonical SQLite state and deterministic filesystem exports.

### Implementation

1. Implement `IWorkspaceSyncService`.
2. Add full export for every migrated domain.
3. Add full import from export into clean or existing database with conflict detection.
4. Add domain-scoped import/export with dependency validation.
5. Add sync markers and canonical hashes per domain.
6. Detect stale exports and external edit conflicts before overwrite.
7. Integrate `.agents` submodule publishing with fresh export preflight.

### Conflict Rules

- If database changed and export changed since last sync, report conflict.
- If export is stale and database is newer, block import unless explicit reconciliation is requested.
- If scoped sync would leave unresolved cross-domain references, fail or require dependent domains.
- Unsupported schema or export versions fail safely.

### Code Impact

- Existing `AgentsSubmodulePublisher` calls in Main/Roadmap flows must ensure the migrated export surface is fresh before publishing.
- `storage-export`, `storage-import`, and `storage-sync` commands report changed rows, changed files, domain scope, marker hashes, and conflicts.

### Tests

- Full export regenerates all migrated files.
- Full import from generated export creates equivalent database.
- Export/import/export is stable.
- Scoped sync leaves unrelated domains unchanged.
- Stale export and divergent edit fixtures fail safely.
- Fake publisher publishes only after fresh export.
- Older filesystem-only state imports without losing legacy-supported data.

### Exit Criteria

- Workspace synchronization is intentional, safe, and test-covered.
- `.agents` submodule publishing can publish the intended export surface.

## Milestone 12: Transactional Workflow Persistence and Recovery

### Objective

Coordinate multi-domain workflow writes across SQLite stores and retained filesystem artifacts so failures are deterministic and recoverable.

### Implementation

1. Define persistence units for covered workflows:
   - roadmap transition save;
   - decision recording plus state update;
   - split lineage plus child artifacts plus lifecycle;
   - execution preparation/provenance updates;
   - journal event emission;
   - loop history/evidence writes;
   - completed epic archive.
2. Implement `IWorkflowPersistenceCoordinator` around SQLite transactions.
3. Implement retained file staging/commit/rollback adapter for filesystem artifacts.
4. Add workflow transaction markers and recovery classification.
5. Add cross-domain integrity validator.
6. Ensure journal started/completed/failed records reflect actual outcomes.
7. Keep transactions out of agent execution time; only persistence phases are transactional.

### Integrity Rules

Detect:

- state or journal references to missing logical paths;
- orphaned execution evidence;
- orphaned loop histories;
- duplicate identities;
- invalid archive references;
- stale sync metadata;
- incomplete workflow transaction markers;
- invalid split child references;
- lifecycle rows pointing to invalid paths.

### Tests

- Injected failure after decision append rolls back state/journal claims.
- Injected split failure does not leave incomplete split family state.
- Evidence write plus state/journal update either commits coherently or classifies retryable partial state.
- Retained file finalization failure classifies retryable versus corrupt based on commit point.
- Concurrent sequence allocation remains unique.
- Integrity validator reports valid, retryable partial, corrupt, unsupported, and conflict categories.

### Exit Criteria

- Covered workflows use the coordinator.
- Failure paths are deterministic.
- No stronger cross-store atomicity guarantee is claimed than staging and recovery can enforce.

## Milestone 13: Storage Compatibility and Verification Mode

### Objective

Expose executable verification that proves the database, filesystem exports, retained files, logical references, archive recovery, and storage-mode behavior are mutually consistent.

### Implementation

1. Implement `IWorkspaceVerificationService`.
2. Compose existing domain validators, sync service, logical resolver, archive recovery checks, and transaction integrity validator.
3. Add temporary export/import round-trip verification in an isolated temp workspace.
4. Add read-only database open and mutation guard checks.
5. Expose `storage-verify`.
6. Add optional `--full-roundtrip` for slower export/import/export checks.

### Verification Findings

Verification reports:

- success;
- stale export;
- missing exported file;
- unresolved logical path;
- nondeterministic round trip;
- unrecoverable archive;
- corrupt domain;
- unsupported version;
- mutation required;
- conflict.

Each finding includes domain, identity/path, rule, severity, current state, expected state, and recommended executable recovery action.

### Tests

- Valid SQLite-canonical workspace with fresh exports verifies successfully.
- Legacy filesystem workspace imports and verifies successfully.
- Stale export fixture fails.
- Missing export fixture fails.
- Unresolved path fixture fails.
- Nondeterministic serializer fixture fails.
- Broken completed epic archive fixture fails.
- Unsupported schema/export version fixture fails.
- Read-only verification does not change database bytes or export tree hashes.

### Exit Criteria

- Verification is read-only by default.
- Required consistency failures are detected.
- Full migrated persistence architecture is covered by executable checks.

## Cross-Cutting Test Plan

Run targeted suites as milestones land:

```powershell
dotnet test tests\LoopRelay.Infrastructure.Tests\LoopRelay.Infrastructure.Tests.csproj
dotnet test tests\LoopRelay.Roadmap.Cli.Tests\LoopRelay.Roadmap.Cli.Tests.csproj
dotnet test tests\LoopRelay.Cli.Tests\LoopRelay.Cli.Tests.csproj
dotnet test tests\LoopRelay.Completion.Tests\LoopRelay.Completion.Tests.csproj
dotnet test tests\LoopRelay.Projections.Tests\LoopRelay.Projections.Tests.csproj
```

Run the full solution before milestone certification:

```powershell
dotnet test LoopRelay.slnx
```

Add new fixtures under existing test projects rather than creating an unrelated fixture hierarchy:

- valid full filesystem `.agents` tree;
- valid empty workspace;
- valid full SQLite-canonical workspace;
- older filesystem-only workspace;
- malformed strict JSON;
- malformed empty-on-error provenance JSON;
- legacy markdown-only state/ledger/lifecycle/projection/split inputs;
- duplicate decision IDs;
- duplicate path identities;
- duplicate `NNNN` histories and evidence;
- invalid filename histories;
- stale export marker;
- divergent database/export edit;
- unresolved path reference;
- corrupt database row;
- broken archive;
- retryable partial workflow marker.

## Completion Criteria

The implementation is complete when all of the following are true:

- Machine-managed migrated domains run canonically from SQLite.
- Retained markdown artifacts remain normal filesystem files.
- Live decisions, live handoff, and live operational delta remain live filesystem workflow files.
- Every migrated domain has deterministic import and export.
- Full export/import/export is stable by domain rules.
- Logical artifact resolution works across retained files, migrated SQLite rows, and exports.
- Freshness behavior remains equivalent to current file-backed behavior.
- Completed epic archives include DB-backed histories and execution evidence.
- Workspace sync detects stale/conflicting exports before overwrite.
- Covered workflow persistence units are transaction-coordinated with deterministic recovery classification.
- Verification succeeds for valid SQLite and imported legacy workspaces.
- Verification detects stale exports, missing exports, unresolved paths, nondeterministic round trips, unrecoverable archives, corrupt domains, and unsupported versions.
- Default verification is read-only and mutation guard tests pass.
