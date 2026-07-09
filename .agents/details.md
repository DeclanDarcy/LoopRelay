# SQLite Persistence Plan Details

This file is an implementation addendum for `.agents/plan.md`. It captures cross-cutting facts from `.agents/specs/roadmap.md`, `.agents/specs/audit.md`, and the M01-M13 deep-dives that are necessary to fill meaningful gaps in the condensed plan.

It should be treated as supporting detail, not a replacement for the plan. When this file and the plan differ, prefer the plan for settled roadmap decisions and use this file for implementation constraints, current behavior, diagnostics, tests, and unresolved questions.

Milestone-scoped constraints belong in `.agents/milestones/m*.md`. Duplicate shared requirements there when a milestone needs them.

## Gap 1: Current Filesystem Behavior Is a Contract

The plan says to introduce domain stores, but implementation must preserve several filesystem behaviors that are currently part of product semantics.

`IArtifactStore` is file-shaped. It exposes `ExistsAsync`, `ReadAsync`, `ReadAs<T>`, `WriteAsync`, `DeleteAsync`, `ListAsync`, and `ListDirectoriesAsync`. `FileSystemArtifactStore.WriteAsync` creates parent directories, writes through a temp file, then atomically replaces or moves into place. Reads are cached by path and file signature. Deletes evict cache entries. File and directory listings are deterministic, with `ListAsync` and `ListDirectoriesAsync` using `StringComparer.OrdinalIgnoreCase` after excluding temp files.

Repo-relative path strings are durable identities, not just locations. They are stored in lifecycle entries, split family child paths, decision ledger input/output paths, roadmap state active artifacts and transition intents, derived artifact manifest entries, transition journal output paths and input snapshots, and provenance causal inputs.

Filename-derived allocation is observable behavior. Existing sequence allocation scans filenames and takes max numeric suffix plus one:

- `.agents/decisions/decisions.NNNN.md` through `LoopArtifacts.PersistDecisionsAsync`.
- `.agents/handoffs/handoff.NNNN.md` through `LoopArtifacts.RotateLiveHandoffAsync`.
- `.agents/deltas/operational_delta.NNNN.md` through `LoopArtifacts.RotateOperationalDeltaAsync`.
- `.agents/evidence/execution/{stem}.NNNN.md` through `RoadmapArtifacts.WriteNumberedEvidenceAsync` and `CompletionArtifacts.WriteNumberedEvidenceAsync`.

Glob ordering affects hashes, prompt content, and archives. Roadmap source files are ordered with ordinal comparison before concatenation or hashing. `ProjectContextLoader` requires `.agents/ctx/01-purpose.md` through `.agents/ctx/08-vocabulary.md` and rejects unexpected numbered context files. Completion archive file moves are deterministic.

Live-plus-history behavior must remain split exactly:

- Live decisions: `.agents/decisions/decisions.md`; history: `.agents/decisions/decisions.NNNN.md`.
- Live handoff: `.agents/handoffs/handoff.md`; history: `.agents/handoffs/handoff.NNNN.md`.
- Live operational delta: `.agents/operational_delta.md`; history: `.agents/deltas/operational_delta.NNNN.md`.

Latest decision and handoff reads are live-first, then highest numeric historical record. Live decision existence is a loop control signal: if live decisions exist, execution can proceed without creating a new decision proposal.

`.agents` submodule publication is part of compatibility. `AgentsSubmodulePublisher` commits and pushes `.agents` after mutating planning/orchestration operations, `CommitGate` treats `.agents` as bookkeeping, and reviewers/tools currently inspect files. Migrated SQLite domains therefore still need deterministic filesystem exports for review, backup, restore, debugging, and older tools.

## Gap 2: Retained Path Clarifications

The plan lists retained artifacts, but implementation must preserve the current path facts and not infer migrations from ambiguous requested patterns.

Retained implemented inputs include:

- `.agents/specs/epic.md`: required planning/preflight input and execution preparation causal input.
- `.agents/specs/s{n}.md`: milestone specs, path/hash inputs for execution preparation.
- `.agents/epic.md`: active roadmap epic markdown.
- `.agents/plan.md`: planning/execution prompt document, mutated by planning and archived on completion.
- `.agents/details.md`: optional plan addendum; this file remains filesystem-backed.
- `.agents/operational_context.md`: rewritten by transfer/evolution and hashed for execution freshness.
- `.agents/operational_delta.md`: live transient delta written before context evolution and rotated afterward.
- `.agents/decisions/decisions.md`: live execution decision handoff.
- `.agents/handoffs/handoff.md`: live execution-to-decision handoff.
- `.agents/milestones/m*.md`: markdown checklists parsed for execution progress.
- `.agents/roadmap/*.md`: human-authored roadmap sources; empty set is a hard failure.
- `.agents/selection.md`: implemented selection artifact path used by selection provenance.
- `.agents/core/roadmap-completion-context.md`: implemented selection/completion input.
- `.agents/ctx/01-purpose.md` through `.agents/ctx/08-vocabulary.md`: implemented project context sources for projections.
- `.agents/projections/*.md`: projection body markdown; metadata migrates, bodies remain filesystem-backed.
- Non-execution evidence directories such as audits, blockers, evaluations, and orchestration remain filesystem-backed unless a later roadmap changes them.

Do not migrate `.agents/core/0*.md` by inference. The audit found no implemented producer or consumer for that pattern. The implemented numbered context files are under `.agents/ctx`.

Do not treat `.agents/evals/*.md` as implemented. The implemented evaluation-like path is `.agents/evidence/evaluations`.

## Gap 3: Domain-by-Domain Compatibility Facts

These facts should drive store contracts, import/export, schema mapping, failure behavior, and tests.

### Decision Ledger

Current file: `.agents/decision-ledger.json`; legacy import: `.agents/decision-ledger.md`.

Facts:

- Schema string is `decision-ledger.v1`.
- Entries are sorted by `DecisionId`.
- Validation rejects malformed decision IDs and duplicate IDs.
- `NextDecisionIdAsync` derives next ID as max `DNNNN` plus one.
- JSON has authority over legacy markdown when both exist.
- Legacy markdown is parsed and migrated only when JSON is absent.
- Execution preparation currently hashes this JSON as a causal input.

Implementation constraints:

- Preserve all imported `DNNNN` IDs exactly.
- Allocate inside transaction/unique constraint in SQLite mode.
- Define freshness equivalence for the decision-ledger hash before making SQLite canonical.
- Treat legacy markdown as explicit import compatibility only; never as authority over a migrated database.

### Execution Preparation Provenance

Current file: `.agents/execution-preparation-manifest.json`.

Facts:

- Model schema string is `execution-preparation.v1`.
- Tracks active epic path/hash, milestone spec paths/hashes, and trusted derived artifacts.
- Records causal inputs for milestone specs, operational context, execution prompt, execution plan, and milestones.
- Freshness detects active epic drift, spec drift, operational context drift, execution prompt drift, decision ledger drift, missing artifacts, and reduced milestone count.
- Missing, blank, or malformed JSON currently loads as `ExecutionPreparationManifest.Empty`.

Implementation constraints:

- Preserve empty-on-missing/blank/malformed behavior at the import/compatibility boundary unless the roadmap explicitly hardens it.
- Manifest rows still reference retained filesystem files and SQLite-backed decision ledger content by logical path/hash.
- Freshness diagnostics need path, stored hash, current hash, and stale reason.

### Selection Provenance

Current file: `.agents/selection-provenance-manifest.json`; retained selected artifact: `.agents/selection.md`.

Facts:

- Model schema string is `selection-provenance.v1`.
- Active trusted selection entries can supersede older active entries.
- Freshness compares selection cycle identity, prompt context hash, secondary input hash, retired epic state hash, roadmap source hashes, projection inputs, and roadmap completion context input.
- Missing, blank, or malformed JSON currently loads empty.

Implementation constraints:

- Preserve active/superseded semantics and stale reason mapping.
- Treat `.agents/selection.md` and `.agents/core/roadmap-completion-context.md` as retained filesystem inputs.
- Empty-on-malformed is compatibility behavior, not a reason to accept corrupt canonical SQLite rows.

### Roadmap State

Current file: `.agents/state.json`; legacy import: `.agents/state.md`.

Facts:

- Schema string is `roadmap-state.v1`.
- JSON has authority over legacy markdown.
- Legacy markdown migration parses current state, active artifacts, transition metadata, blockers, next valid transitions, decision ledger summary, projection counts, split counts, and retired epics.
- State is partly a projection of other stores and filesystem listings.

Implementation constraints:

- Preserve active artifact paths and transition intent evidence paths.
- Avoid creating a second authority for derived counts. Either recompute on save or validate snapshot fields against canonical stores.
- Database wins over stale exported JSON after migration; legacy markdown is only an explicit import source.

### Artifact Lifecycle

Current file: `.agents/artifacts/lifecycle.json`; legacy import: `.agents/artifacts/lifecycle.md`.

Facts:

- Schema string is `artifact-lifecycle.v1`.
- Entries are keyed by path and sorted by path.
- Duplicate paths are rejected case-insensitively.
- Upsert removes the existing path case-insensitively, appends a new timestamped entry, and saves.

Implementation constraints:

- Preserve path identity as repo-relative artifact identity.
- Enforce case-insensitive uniqueness in domain logic and SQLite constraints.
- Timestamp policy must be explicit for byte-stable export tests.

### Split Lineage

Current files: `.agents/splits/split-family-*.json`; legacy import: `.agents/splits/split-family-*.md`.

Facts:

- Schema string is `split-family.v1`.
- Filename embeds family ID as `split-family-{FamilyId}.json`.
- Split transition writes child epics, lifecycle entries, and split family record as one lineage event.
- `ExistsForChildAsync` lists JSON files first, then migrates matching legacy markdown only when JSON is absent.
- Roadmap state summaries count `split-family-*.json` files today.

Implementation constraints:

- Preserve family ID, child paths, selected child, and dependency order.
- Preserve child-path lookup behavior without scanning exported files in SQLite mode.
- JSON family file wins over same-family legacy markdown during import.

### Projection Metadata

Current file: `.agents/projections/manifest.json`; legacy import: `.agents/projections/manifest.md`; retained bodies: `.agents/projections/*.md`.

Facts:

- Schema string is `projection-manifest.v1`.
- Entries are keyed by runtime prompt name.
- Entries include projection path, prompt/source/context/projection hashes, generated time, validation status, stale status, provenance status, projection identity, prompt type, causal inputs, and stale reasons.
- Store/model logic is duplicated in `LoopRelay.Roadmap.Cli` and `LoopRelay.Projections`.

Implementation constraints:

- Runtime prompt name is the canonical key.
- Projection body markdown remains filesystem-backed and is referenced by path/hash.
- Consolidate or conformance-test the duplicate stores so both projects see the same semantics.
- Timestamp/generated-time byte-stability policy must be explicit.

### Transition Journal

Current file: `.agents/journal/transitions.jsonl`.

Facts:

- Append reads the existing JSONL file, trims trailing whitespace, appends one serialized record, and writes the full content back.
- Records use `JsonSerializerDefaults.Web`.
- Records include correlation ID, previous/attempted state, prompt, projection, prompt contract key, input artifact hashes, output paths, duration, result, parser decision, error message, and optional input snapshot.
- Tests cover started/completed pairs, legacy records without input snapshot, changed inputs during prompt execution, and failure paths.

Implementation constraints:

- Preserve append order independently of filesystem append behavior.
- Preserve correlation ID grouping.
- Legacy JSONL records without input snapshots must import.
- JSONL export is one record per line in canonical event order; external byte-compatibility beyond logical content is an open detail.

### Loop Histories

Current histories:

- `.agents/decisions/decisions.NNNN.md`
- `.agents/handoffs/handoff.NNNN.md`
- `.agents/deltas/operational_delta.NNNN.md`

Facts:

- Decision proposal writes both live `decisions.md` and numbered decision history; target collision throws.
- Execution consumes and retires only live `decisions.md`.
- Execution writes live `handoff.md`; the next decision loop rotates it to numbered history.
- Transfer writes live `operational_delta.md`; context evolution consumes it; rotation writes numbered delta history and deletes live delta.
- Completion archive currently moves decisions, handoffs, and deltas directories.

Implementation constraints:

- Live files stay filesystem-backed and live-first.
- History write must succeed before deleting a live file.
- Latest fallback queries must order by numeric sequence, not lexical path.
- Markdown bodies are opaque text; preserve exact body bytes unless a domain explicitly declares canonicalization.

### Execution Evidence

Current files: `.agents/evidence/execution/{stem}.NNNN.md`.

Facts:

- `RoadmapExecutionBridge` and `CompletionCertificationService` both write numbered execution evidence through artifact helpers.
- Evidence paths are stored in state, journal, prompt inputs, transition intent, unblock planning, and completion evaluation.
- Prompt context reads required evidence by path.
- Unblock planning searches execution evidence.

Implementation constraints:

- Preserve logical evidence paths, stems, suffixes, body text, and hashes.
- Writes allocate path and hash atomically with body storage.
- Read/search consumers must work after exported evidence files are deleted.
- Missing evidence maps to stale, invalid, or blocked according to the consuming workflow.
- Non-execution evidence directories remain filesystem-backed.

### Completed Epic Archives

Current archive behavior is filesystem-shaped.

Facts:

- `CompletedEpicArchiveService` moves decisions, deltas, handoffs, milestones, review files, details, operational context, and plan into `.agents/archive/completed-epics/{index}`.
- Archive index is currently `ListDirectoriesAsync(archiveRoot).Count + 1`.
- Directory contents are moved deterministically as files.

Implementation constraints:

- Archive association must deterministically select DB-backed histories/evidence belonging to the completed epic.
- Missing migrated records must fail archive completion, not be silently omitted.
- Materialize DB-backed histories/evidence into deterministic archive export form.
- Read DB-backed records and validate archive plan before destructive retained-file moves.
- Recovered archive state must remain archived state, not active workspace history, unless explicitly restored.

## Gap 4: Import, Export, and Byte Stability Rules

Filesystem export is a first-class interchange format for migrated domains.

General rules:

- Import never reallocates existing identities.
- Export never changes logical path identities, sequence suffixes, decision IDs, family IDs, runtime prompt names, or correlation IDs.
- Strict JSON domains export deterministic indented JSON with one trailing newline.
- JSONL export writes one record per line in event order.
- Markdown histories and evidence preserve body text as opaque content.
- Export/import/export stability is required unless a field is explicitly non-canonical.
- Validation errors include domain, path, identity, and reason.
- Partial export/import is valid only with explicit domain scope and dependency validation.

Ordering and identity rules:

- Decision ledger sorts by `DecisionId`.
- Lifecycle sorts by path with case-insensitive identity.
- Projection manifest sorts by runtime prompt name.
- Split exports sort family files by family ID and preserve child dependency order.
- Histories sort by numeric `NNNN`.
- Evidence sorts by directory/stem/numeric suffix and preserves stem/suffix.
- Journal preserves append/event order.

Non-canonical metadata must be declared before golden tests. Timestamps and generated times are the main candidates.

Partial export semantics must be explicit per domain. Missing optional provenance manifests may mean empty. Missing required state, referenced evidence, or lifecycle records should normally be invalid. Truncated journals or omitted split/evidence records require explicit markers if supported.

## Gap 5: Logical Artifact Resolution and Freshness

The plan names a resolver and canonical hasher; implementation needs these concrete contracts.

A logical artifact descriptor should include:

- repo-relative path;
- domain classification;
- storage classification: retained file, migrated canonical record, exported projection, missing, wrong domain, stale, invalid, or blocked;
- canonical content or an opaque content handle;
- canonical hash and hash algorithm;
- referring domain when available for diagnostics.

Provider responsibilities:

- Retained filesystem provider resolves retained prompt/workflow/source files through `IArtifactStore`.
- Structured migrated providers resolve exported JSON-equivalent content through domain serializers or canonical row snapshots.
- Loop history provider resolves numbered decisions, handoffs, and deltas.
- Evidence provider resolves `.agents/evidence/execution/{stem}.NNNN.md` by path.
- Projection body provider resolves retained `.agents/projections/*.md`.
- Archive provider resolves archived materialized records where verification/recovery needs them.

Hash policy:

- Retained files hash current file bytes, preserving existing behavior.
- Migrated records should hash canonical export-equivalent content unless a domain explicitly defines a freshness token.
- If canonical JSON hashes cannot be preserved during migration, freshness baselines must be intentionally invalidated with explicit diagnostics rather than silently drifting.

Consumers that must stop assuming physical files for migrated paths:

- `TransitionInputAccumulator`
- `TransitionInputResolver`
- `ExecutionPreparationProvenanceService`
- `SelectionProvenanceService`
- `RoadmapPromptContextBuilder`
- `RoadmapUnblockPlanner`
- `InvariantValidator`
- completion context/evaluation readers that consume evidence paths

Missing-path behavior must remain consumer-specific. A retained file missing should report missing retained artifact. A migrated record missing should become stale, invalid, or blocked according to the existing workflow behavior. No resolver fallback may fabricate content or read unrelated filesystem files.

## Gap 6: SQLite Substrate, Integrity, and Import Details

The database location is settled by the plan as `.LoopRelay/persistence/looprelay.sqlite3`. Implementation still needs these details:

- Centralize location in one workspace database locator.
- Validate the resolved database path stays inside workspace policy.
- Add `Microsoft.Data.Sqlite` through central package management.
- Open read-only mode for verification.
- Validate schema version before all domain reads and writes.
- Use parameterized SQL only.
- Do not hold SQLite transactions while Codex or any long-running agent/prompt process executes.
- Batch import inserts inside transactions.
- Failed imports roll back and must not leave the database classified as valid imported state.

Database lifecycle states:

- Missing
- Initialized
- ValidEmpty
- Imported
- ValidImported
- Corrupt
- UnsupportedSchema
- IncompatiblePartialState

Import behavior:

- Validate filesystem snapshot first.
- Begin transaction.
- Insert/update rows preserving all identities, timestamps, hashes, and path references.
- Compare database snapshot to source snapshot.
- Commit only after equality succeeds.
- Re-import of unchanged source is idempotent.

Diagnostics should include database path, schema version, domain counts, row counts, source snapshot hash/equality summary, failing table/domain, and rolled-back status.

## Gap 7: Storage Authority and Sync Policy

After a domain migrates, SQLite is canonical and filesystem files are exports/import sources only.

Authority rules:

- Normal `status`, `run`, and `unblock` must not silently import stale filesystem exports over database state.
- Database state wins over stale exported JSON after migration.
- Legacy markdown is import compatibility, not canonical runtime authority.
- Export can regenerate deleted filesystem equivalents without logical loss.
- Export timing for Git/submodule visibility remains a workspace synchronization policy, not a per-domain migration decision.

Workspace sync must support:

- full export from canonical SQLite to `.agents` filesystem equivalents;
- full import from generated exports to a clean equivalent database;
- domain-scoped import/export;
- stale export detection;
- divergent database/export edit conflict detection;
- unsupported mixed-version detection;
- `.agents` submodule publish integration for fresh exports.

Conflict detection should compare sync metadata and content hashes before overwriting in either direction. If both database and filesystem export changed, fail safely and require explicit reconciliation. Scoped sync must validate dependency closure and reject scopes that leave unresolved cross-domain references.

Sync diagnostics should include operation type, domain scope, changed file count, changed row count, conflict list, marker/hash values, changed paths, and suggested reconciliation action.

The SQLite file itself is process state unless a later roadmap explicitly changes publication policy. The required publish surface is the deterministic filesystem export state.

## Gap 8: Workflow Transactions and Filesystem Staging

The plan requires transaction safety; implementation must avoid promising impossible atomicity across SQLite and arbitrary files.

Covered workflows must define persistence units before their first mutating write:

- roadmap transition persistence;
- decision ledger append plus roadmap state update;
- split child artifact writes, lifecycle updates, and split family record;
- execution preparation artifact generation plus provenance update;
- selection artifact write plus provenance update;
- projection body generation/validation plus manifest update;
- transition journal started/completed/failed recording;
- loop history/evidence writes with state/journal references;
- completion archive association/materialization.

Coordinator responsibilities:

- open database transaction for covered SQLite writes;
- invoke domain stores through transaction-aware methods;
- stage retained filesystem writes/deletes when needed;
- commit/rollback database work;
- finalize or roll back staged file mutations;
- record journal outcomes accurately;
- create workflow transaction markers if needed for recovery.

Filesystem staging rules:

- Stage writes/deletes under paths that cannot escape the workspace.
- Do not delete a retained live file until its corresponding SQLite history write has succeeded.
- Do not overwrite retained files inconsistently with committed database state.
- On file finalization failure, classify state as retryable partial or corrupt depending on the commit point and staged state.

Recovery classifier inputs:

- journal records;
- workflow transaction markers;
- domain state;
- retained file presence/hash;
- sync metadata.

Recovery categories should include valid, retryable partial, corrupt, unsupported, and conflict. Default recovery should classify and guide retry; mutation or repair must be explicit.

## Gap 9: Verification Mode Details

Verification must be executable and read-only by default.

Verification result categories:

- success
- stale export
- missing export
- unresolved path
- nondeterministic sync
- unrecoverable archive
- corrupt domain
- unsupported version
- mutation required

Verification checks:

- open database read-only;
- validate database/schema/integrity;
- validate sync/export freshness;
- resolve all logical references in state, journal, provenance, lifecycle, split lineage, histories, evidence, and archives;
- verify retained-file and migrated-record freshness;
- verify archive recoverability;
- run export/import equivalence in isolated temp workspaces;
- aggregate findings with domain, identity/path, rule, severity, expected/current state, and recommended executable recovery action.

Required detections:

- stale exports relative to canonical database state;
- missing required exported files for migrated records;
- unresolved logical paths;
- nondeterministic export/import/export;
- broken completed epic archives;
- corrupt domains/rows;
- unsupported schema/export versions;
- accidental mutation during verification.

Mutation guard tests must compare pre/post hashes of the canonical database and export tree. Optional repair or re-export modes must be explicit and separate from default verification.

## Gap 10: Existing and New Test Surface

Existing tests directly covering the split boundary:

- `tests/LoopRelay.Core.Tests/Services/Artifacts/FileSystemArtifactStoreTests.cs`
- `tests/LoopRelay.Cli.Tests/Services/Execution/LoopArtifactsTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/Services/State/RoadmapStateStoreTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState/DecisionLedgerTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/Services/ArtifactManagement/ArtifactLifecycleTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/Services/Projections/ProjectionManifestTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/Services/Splits/SplitFamilyStoreTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/Services/Execution/ExecutionPreparationProvenanceTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/Services/Selection/SelectionProvenanceTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState/TransitionJournalTests.cs`
- `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState/TransitionInputResolverTests.cs`
- `tests/LoopRelay.Plan.Cli.Tests/Services/PlanArtifactOperations/PlanArtifactsTests.cs`
- `tests/LoopRelay.Plan.Cli.Tests/Services/Execution/PlanPipelineTests.cs`
- `tests/LoopRelay.Plan.Cli.Tests/Services/Execution/PreflightGateTests.cs`
- `tests/LoopRelay.Completion.Tests/Services/CompletionCertificationServiceTests.cs`

Behaviors to preserve or intentionally change with explicit tests:

- JSON authority over legacy markdown when both exist.
- Legacy markdown migration when JSON is absent.
- Malformed legacy markdown fails without writing JSON for strict stores.
- Malformed execution/selection provenance JSON currently becomes empty.
- Deterministic JSON formatting and ordering.
- Filename-derived next sequence for decisions, deltas, handoffs, and evidence.
- Live-first fallback for decisions and handoffs.
- Transition journal started/completed correlation and input snapshot reuse.
- Selection and execution freshness invalidation when retained filesystem inputs change.
- Completion archive movement of active and historical files.
- Planning preflight blocking when retained files already exist.

New test fixtures and scenarios needed:

- Valid empty workspace.
- Valid full filesystem `.agents` tree.
- Valid full SQLite-canonical workspace.
- Older filesystem-only workspace.
- Mixed legacy markdown and JSON source workspace.
- Malformed strict JSON.
- Malformed empty-on-error provenance JSON.
- Duplicate decision IDs.
- Duplicate case-variant lifecycle paths.
- Duplicate logical evidence paths.
- Duplicate `NNNN` histories and evidence.
- Invalid filename histories/evidence.
- Legacy JSONL without input snapshots.
- Stale export marker.
- Divergent database and export edits.
- Scoped sync with unresolved cross-domain references.
- Deleted export while SQLite rows exist.
- Corrupt database row/body/hash.
- Broken completed epic archive.
- Retryable partial workflow marker.
- Staged retained file mutation.
- Concurrent sequence allocation harness.
- Clarification fixtures for `.agents/core/0*.md` versus `.agents/ctx/0*.md`.
- Clarification fixtures for `.agents/evals/*.md` versus `.agents/evidence/evaluations/*.md`.

## Gap 11: Cross-Cutting Open Questions That Remain Implementation Blockers

The plan resolves some audit questions, such as the database path and retained status of several implemented artifacts. These questions still need explicit implementation decisions because they affect multiple milestones or future roadmap scope:

- Should migrated freshness preserve byte-identical hashes of current canonical JSON exports, or intentionally reset baselines with a migration diagnostic?
- What timestamp/generated-time fields are non-canonical for byte-stability tests?
- Is SQLite limited to the listed machine-managed domains, or will future roadmaps migrate projection bodies/project context?
