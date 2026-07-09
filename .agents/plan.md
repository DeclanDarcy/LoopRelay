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

(See ./milestones/m1-file-backed-domain-persistence-surface.md)

## Milestone 2: Lossless Filesystem Serialization

(See ./milestones/m2-lossless-filesystem-serialization.md)

## Milestone 3: Logical Artifact Identity and Freshness Resolution

(See ./milestones/m3-logical-artifact-identity-freshness.md)

## Milestone 4: SQLite Workspace Store and Importable Database State

(See ./milestones/m4-sqlite-workspace-store-import.md)

## Milestone 5: Core Roadmap State Runs from SQLite

(See ./milestones/m5-core-roadmap-state-sqlite.md)

## Milestone 6: Provenance and Projection Metadata Run from SQLite

(See ./milestones/m6-provenance-projection-metadata-sqlite.md)

## Milestone 7: Transition Journal Runs from SQLite with JSONL Interchange

(See ./milestones/m7-transition-journal-sqlite-jsonl.md)

## Milestone 8: Loop Histories Move to SQLite While Live Files Stay on Filesystem

(See ./milestones/m8-loop-histories-sqlite-live-files.md)

## Milestone 9: Execution Evidence Moves to SQLite with Path-Compatible Access

(See ./milestones/m9-execution-evidence-sqlite.md)

## Milestone 10: Completed Epic Archives Preserve DB-Backed Historical State

(See ./milestones/m10-completed-epic-archives.md)

## Milestone 11: Bidirectional Workspace Synchronization

(See ./milestones/m11-bidirectional-workspace-sync.md)

## Milestone 12: Transactional Workflow Persistence and Recovery

(See ./milestones/m12-transactional-workflow-persistence.md)

## Milestone 13: Storage Compatibility and Verification Mode

(See ./milestones/m13-storage-verification-mode.md)

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
