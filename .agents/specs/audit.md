# SQLite Persistence Split Audit

## 1. Scope

This audit evaluates the proposed split between `.agents` filesystem artifacts and SQLite-backed persistence in the current LoopRelay implementation. It is intentionally limited to implementation facts needed to inform a later roadmap.

The requested retained filesystem artifacts are:

- `.agents/specs/epic.md`
- `.agents/specs/s{n}.md`
- `.agents/plan.md`
- `.agents/operational_context.md`
- `.agents/operational_delta.md`
- `.agents/decisions/decisions.md`
- `.agents/evals/*.md`
- `.agents/handoffs/handoff.md`
- `.agents/milestones/m*.md`
- `.agents/roadmap/*.md`

The requested SQLite migration candidates are:

- `.agents/decision-ledger.json`
- `.agents/execution-preparation-manifest.json`
- `.agents/selection-provenance-manifest.json`
- `.agents/state.json`
- `.agents/artifacts/lifecycle.json`
- `.agents/core/0*.md`
- `.agents/decisions/decisions.NNNN.md`
- `.agents/deltas/operational_delta.NNNN.md`
- `.agents/evidence/execution/*`
- `.agents/handoffs/handoff.NNNN.md`
- `.agents/journal/transitions.jsonl`
- `.agents/projections/manifest.json`
- `.agents/splits/split-family-*.json`

Important scope mismatch found in code:

- `.agents/core/0*.md` is not implemented as a known path pattern. The implemented numbered project context files are `.agents/ctx/01-purpose.md` through `.agents/ctx/08-vocabulary.md` in `RoadmapArtifactPaths.ProjectContextSourceFiles`, `ProjectionArtifactPaths.ProjectContextSourceFiles`, and `ProjectContextLoader`.
- `.agents/core/roadmap-completion-context.md` is implemented and heavily used, but it is not listed in the proposed split.
- `.agents/evals/*.md` was not found as an implemented path. The implemented evaluation-like directory is `.agents/evidence/evaluations`.

Primary persistence chokepoints reviewed:

- `src/LoopRelay.Core/Abstractions/Artifacts/IArtifactStore.cs`
- `src/LoopRelay.Core/Services/Artifacts/FileSystemArtifactStore.cs`
- `src/LoopRelay.Infrastructure/Services/Artifacts/RepositoryArtifactStore.cs`
- `src/LoopRelay.Orchestration.Primitives/Services/OrchestrationArtifactPaths.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Artifacts/RoadmapArtifactPaths.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Artifacts/RoadmapArtifacts.cs`
- `src/LoopRelay.Completion/Services/ArtifactStorage/CompletionArtifacts.cs`
- `src/LoopRelay.Cli/Services/Execution/LoopArtifacts.cs`

## 2. Persistence Authority

This audit treats persistence authority, compatibility projections, and migration/import as separate concerns.

SQLite is the proposed canonical persistence store for machine-managed persistence domains. Filesystem representations of those same domains should be treated as deterministic external serializations, not as disposable compatibility shims.

The migration frame therefore has three representations:

| Representation | Purpose | Audit implication |
|---|---|---|
| In-memory domain model | Runtime operations and workflow decisions | Workflow code should operate on persistence domains, not on SQL rows or exported files directly. |
| SQLite | Canonical storage for machine-managed domains | Database state is authoritative for migrated artifacts once imported. |
| Filesystem export | Interchange, Git review, debugging, backup, portability, open-source consumption, deterministic tests | Exported files must be capable of full round-trip import/export for migrated domains. |

The implementation must support these directional operations as first-class behavior:

- import filesystem representation to SQLite
- export SQLite representation to filesystem
- deterministic serialization
- deterministic deserialization
- lossless round-trip fidelity

A filesystem export followed by import must reproduce the same logical persistence state. For stable domains, export -> import -> export should be byte-for-byte stable except for explicitly ignored metadata such as timestamps if a domain marks those as non-canonical.

This framing changes how compatibility facts should be read:

- Canonical persistence answers where authoritative data lives.
- Filesystem projection answers what paths still exist for Git, users, tests, archives, and older integrations.
- Migration/import answers how existing repositories become new repositories.

## 3. Current Persistence Inventory

| Artifact | Keep FS / Move SQLite | Current Producers | Current Consumers | Format / Schema | Git-Tracked Assumption | Human Editability Assumption | Notes |
|---|---:|---|---|---|---|---|---|
| `.agents/specs/epic.md` | Keep FS | Planning/spec workflows; roadmap execution preparation reads active epic sources; constants in `OrchestrationArtifactPaths.SpecsEpic` | `PlanArtifacts.ListSpecsRelativeAsync`; `PreflightGate`; `ExecutionPreparationProvenanceService`; roadmap prompt context and transition input resolution | Markdown | Under `.agents` submodule, published by planning/orchestration flows | Human and agent authored | Required by `PreflightGate`; copied/hashed as causal input for execution preparation freshness. |
| `.agents/specs/s{n}.md` | Keep FS | Milestone/spec generation paths; execution preparation records milestone specs | `ExecutionPreparationProvenanceService.RequireFreshMilestoneSpecPathsAsync`; roadmap prompt context; transition input snapshots | Markdown | Under `.agents` submodule | Human and agent reviewable | Freshness depends on stored path identity plus hash. Unexpected active specs make milestone spec freshness stale. |
| `.agents/plan.md` | Keep FS | `PlanSession`; `PlanPipeline`; `OneShotSteps.ExtractMilestones`; execution plan compatibility generation | `ExecutionStep`; `LoopArtifacts.EnsureOperationalContextAsync`; completion archive; plan/details optimization | Markdown | Under `.agents` submodule; publish happens after mutating planning steps and before/after execution | Human and agent authored | Retained file is central prompt input. Completion archive moves it into completed epic archive. |
| `.agents/operational_context.md` | Keep FS | `PlanPipeline` seeds it from plan; `LoopArtifacts.EnsureOperationalContextAsync`; `DecisionSession.EvolveOperationalContextAsync` | Decision proposal prompts; execution preparation provenance; execution prompt provenance; completion archive | Markdown | Under `.agents` submodule | Human and agent reviewable, but often rewritten by agent operations | Its hash is a causal input for execution prompt freshness. |
| `.agents/operational_delta.md` | Keep FS | `DecisionSession.TransferAsync` writes live delta before evolving context | `DecisionSession.EvolveOperationalContextAsync`; `LoopArtifacts.RotateOperationalDeltaAsync` | Markdown | Under `.agents` submodule while live | Agent generated but inspectable | Live delta is transient; after transfer it is rotated to `.agents/deltas/operational_delta.NNNN.md` and live file is deleted. |
| `.agents/decisions/decisions.md` | Keep FS | `LoopArtifacts.PersistDecisionsAsync`; decision session proposal path | `LoopRunner`; `ExecutionStep`; `LoopArtifacts.ReadLatestDecisionsAsync`; completion archive | Markdown | Under `.agents` submodule | Human and agent inspectable; HITL capture can read it | Live decision file is consumed by execution and retired with `RetireLiveDecisionsAsync`. Historical numbered files are separate. |
| `.agents/evals/*.md` | Keep FS | No implementation found | No implementation found | Unknown | Unknown | Unknown | Current code uses `.agents/evidence/evaluations/*.md`, not `.agents/evals/*.md`. |
| `.agents/handoffs/handoff.md` | Keep FS | `ExecutionStep` requires agent to write live handoff | `LoopRunner`; `DecisionSession.BuildProposalPromptAsync`; `LoopArtifacts.ReadLatestHandoffAsync`; completion archive | Markdown | Under `.agents` submodule | Human and agent inspectable | Live handoff is rotated to `.agents/handoffs/handoff.NNNN.md` before the next decision session. |
| `.agents/milestones/m*.md` | Keep FS | `OneShotSteps.ExtractMilestones`; execution preparation compatibility artifacts | `PlanArtifacts.ListMilestonesRelativeAsync`; `ExecutionStep`; `CommitGate` milestone progress check; completion archive | Markdown checklists | Under `.agents` submodule | Human and agent edited checklist state | Checkbox parsing in `MilestoneChecklist` and progress detection in execution flow depend on markdown structure. |
| `.agents/roadmap/*.md` | Keep FS | Roadmap source authoring outside reviewed persistence stores | `RoadmapArtifacts.RequireRoadmapSourcePathsAsync`; `TransitionInputResolver`; `SelectionProvenanceService` | Markdown | Under `.agents` submodule | Human-authored roadmap source | Listed by glob and ordered before concatenation/hashing. Empty set is a hard failure. |
| `.agents/decision-ledger.json` | Move SQLite | `DecisionLedgerStore.AppendAsync` via `DecisionRecorder` | `RoadmapTransitionPersistence`; `ExecutionPreparationProvenanceService`; state summaries; tests | JSON schema `decision-ledger.v1` | Currently under `.agents` submodule | Machine-managed; legacy `.md` was human-readable | Strict structured store validates IDs, uniqueness, and schema. Legacy `.agents/decision-ledger.md` migrates if JSON is absent. |
| `.agents/execution-preparation-manifest.json` | Move SQLite | `ExecutionPreparationProvenanceService` records authoritative inputs and derived artifacts | Execution preparation freshness checks; roadmap artifact snapshot; execution prompt and milestone readiness checks | JSON schema string `execution-preparation.v1` on model | Currently under `.agents` submodule | Machine-managed | Store silently returns empty on malformed JSON, unlike strict structured stores. |
| `.agents/selection-provenance-manifest.json` | Move SQLite | `SelectionProvenanceService.RecordSelectionAsync` | Selection freshness evaluation; retired epic and roadmap source drift checks | JSON schema string `selection-provenance.v1` on model | Currently under `.agents` submodule | Machine-managed | Store silently returns empty on malformed JSON. Active trusted selection entries drive freshness. |
| `.agents/state.json` | Move SQLite | `RoadmapStateStore.SaveAsync`; `RoadmapTransitionPersistence` | Roadmap state machine and transition persistence; tests | JSON schema `roadmap-state.v1` | Currently under `.agents` submodule | Machine-managed; legacy `.md` was human-readable | JSON has authority over legacy markdown. Legacy `.agents/state.md` migrates only when JSON is absent. |
| `.agents/artifacts/lifecycle.json` | Move SQLite | `ArtifactLifecycleStore.UpsertAsync`; promotion/split/bootstrap flows | Artifact lifecycle validation and snapshots | JSON schema `artifact-lifecycle.v1` | Currently under `.agents` submodule | Machine-managed | Keyed by artifact path, duplicate paths are rejected case-insensitively. Legacy lifecycle markdown migrates. |
| `.agents/core/0*.md` | Move SQLite | No implemented producer found for this pattern | No implemented consumer found for this pattern | Unknown | Unknown | Unknown | The implemented numbered context files are `.agents/ctx/0*.md`. `.agents/core/roadmap-completion-context.md` exists and is separate. |
| `.agents/decisions/decisions.NNNN.md` | Move SQLite | `LoopArtifacts.PersistDecisionsAsync`; `LoopArtifacts.RotateLiveDecisionsAsync` | `LoopArtifacts.ReadLatestDecisionsAsync`; completion archive moves decisions directory | Markdown history file | Currently under `.agents` submodule | Reviewable historical record | Sequence is computed from filenames by scanning `.agents/decisions/decisions.*.md`. Target collision throws. |
| `.agents/deltas/operational_delta.NNNN.md` | Move SQLite | `LoopArtifacts.RotateOperationalDeltaAsync` | Completion archive moves deltas directory | Markdown history file | Currently under `.agents` submodule | Reviewable historical record | Sequence is computed from filenames by scanning `.agents/deltas/operational_delta.*.md`. |
| `.agents/evidence/execution/*` | Move SQLite | `RoadmapExecutionBridge`; `CompletionCertificationService` through numbered evidence helpers | `TransitionInputResolver`; `RoadmapPromptContextBuilder`; `RoadmapUnblockPlanner`; transition intent/state records | Mostly markdown evidence with numbered stems | Currently under `.agents` submodule | Evidence is inspectable and often used for review/debugging | Consumers pass concrete evidence paths and hash file contents. Moving this requires a path or evidence identity compatibility story. |
| `.agents/handoffs/handoff.NNNN.md` | Move SQLite | `LoopArtifacts.RotateLiveHandoffAsync` | `LoopArtifacts.ReadLatestHandoffAsync`; decision session; completion archive | Markdown history file | Currently under `.agents` submodule | Reviewable historical record | Sequence is computed from filenames by scanning `.agents/handoffs/handoff.*.md`. |
| `.agents/journal/transitions.jsonl` | Move SQLite | `TransitionJournalStore.AppendAsync`; prompt runner, state machine, promotion, split, completion, failure paths | Transition journal tests; operational debugging; possible future replay | JSON Lines; records use `TransitionJournalRecord` shape | Currently under `.agents` submodule | Machine log; readable for debugging | Append is implemented as read whole file, trim, rewrite with added line. Legacy records without input snapshot are supported. |
| `.agents/projections/manifest.json` | Move SQLite | `ProjectionManifestStore` in roadmap CLI and projections project | Projection freshness, context projection service, snapshots | JSON schema `projection-manifest.v1` | Currently under `.agents` submodule | Machine-managed | Store implementation is duplicated in `LoopRelay.Roadmap.Cli` and `LoopRelay.Projections`. Projection markdown files remain separate from this manifest. |
| `.agents/splits/split-family-*.json` | Move SQLite | `SplitFamilyStore.WriteAsync`; `SplitEpicTransition` | `SplitFamilyStore.ExistsForChildAsync`; state summaries | JSON schema `split-family.v1` | Currently under `.agents` submodule | Machine-managed; legacy markdown was readable | Filename embeds family ID. Legacy `.md` family files migrate on lookup when JSON is absent. |

### Migrated Artifact Ownership

| Artifact | Authoritative owner/service today | Read-only projections | Cached copies | Derived views |
|---|---|---|---|---|
| `.agents/decision-ledger.json` | `DecisionLedgerStore`; appended through `DecisionRecorder` | Legacy `.agents/decision-ledger.md` import when JSON is absent | `FileSystemArtifactStore` read cache only | Last decision ID in `RoadmapTransitionPersistence`; decision ledger hash in execution preparation provenance |
| `.agents/execution-preparation-manifest.json` | `ExecutionPreparationManifestStore`; written by `ExecutionPreparationProvenanceService` | None implemented | `FileSystemArtifactStore` read cache only | Freshness status for specs, operational context, execution prompt, plan, and milestones |
| `.agents/selection-provenance-manifest.json` | `SelectionProvenanceManifestStore`; written by `SelectionProvenanceService` | None implemented | `FileSystemArtifactStore` read cache only | Selection freshness status and stale reasons |
| `.agents/state.json` | `RoadmapStateStore`; updated by `RoadmapTransitionPersistence` | Legacy `.agents/state.md` import when JSON is absent | `FileSystemArtifactStore` read cache only | Current state summaries, active artifact statuses, split counts, projection counts |
| `.agents/artifacts/lifecycle.json` | `ArtifactLifecycleStore` | Legacy `.agents/artifacts/lifecycle.md` import when JSON is absent | `FileSystemArtifactStore` read cache only | Lifecycle validation and roadmap artifact snapshot state |
| `.agents/core/0*.md` | No implemented owner found | None found | None found | No implemented derived view found |
| `.agents/decisions/decisions.NNNN.md` | `LoopArtifacts` | Live `.agents/decisions/decisions.md` is separate retained working file, not a projection of history | `FileSystemArtifactStore` read cache only | Latest decision fallback; completion archive material |
| `.agents/deltas/operational_delta.NNNN.md` | `LoopArtifacts` | Live `.agents/operational_delta.md` is separate retained working file before rotation | `FileSystemArtifactStore` read cache only | Completion archive material |
| `.agents/evidence/execution/*` | `RoadmapArtifacts` and `CompletionArtifacts` numbered evidence helpers | Evidence paths recorded in state/journal are references, not copies | `FileSystemArtifactStore` read cache only | Prompt input snapshots, unblock planning input, completion evaluation context |
| `.agents/handoffs/handoff.NNNN.md` | `LoopArtifacts` | Live `.agents/handoffs/handoff.md` is separate retained working file before rotation | `FileSystemArtifactStore` read cache only | Latest handoff fallback; completion archive material |
| `.agents/journal/transitions.jsonl` | `TransitionJournalStore` | None implemented | `FileSystemArtifactStore` read cache only | Transition history and debugging chronology |
| `.agents/projections/manifest.json` | `ProjectionManifestStore` in two projects | Legacy `.agents/projections/manifest.md` import when JSON is absent | `FileSystemArtifactStore` read cache only | Projection freshness, validation status, stale status |
| `.agents/splits/split-family-*.json` | `SplitFamilyStore` | Legacy `.agents/splits/split-family-*.md` import when JSON is absent | `FileSystemArtifactStore` read cache only | Split family existence by child path; split family count in state |

## 4. Persistence Semantics

The table below classifies current behavior for migrated artifacts. It does not propose database tables or schemas.

| Artifact | Current persistence semantics | Current lifecycle facts |
|---|---|---|
| `.agents/decision-ledger.json` | Append to logical ledger; overwrite canonical JSON document; derived last-ID view | `AppendAsync` loads, appends, sorts by `DecisionId`, saves; next ID is max `DNNNN` plus one. |
| `.agents/execution-preparation-manifest.json` | Snapshot; overwrite; derived freshness cache | Records authoritative inputs and active derived artifacts; stale when retained inputs or decision ledger hash drift. |
| `.agents/selection-provenance-manifest.json` | Snapshot; overwrite; supersede active entries; derived freshness cache | Tracks trusted active selection decisions; freshness depends on current selection cycle inputs. |
| `.agents/state.json` | Snapshot; overwrite; derived workflow state view | Saved from transition persistence using active paths, ledger summaries, projection counts, split counts, and transition intent. |
| `.agents/artifacts/lifecycle.json` | Upsert by path; overwrite canonical document; derived lifecycle view | Removes existing path case-insensitively, appends new timestamped entry, sorts by path. |
| `.agents/core/0*.md` | Unknown | No current producer or consumer found; implemented numbered context files are under `.agents/ctx`. |
| `.agents/decisions/decisions.NNNN.md` | Immutable historical write by sequence; rotate/copy from live decision flow | `PersistDecisionsAsync` writes numbered history and live decision; target collision throws. |
| `.agents/deltas/operational_delta.NNNN.md` | Rotate live file into immutable historical sequence | Transfer writes live delta, context evolution consumes it, rotation writes numbered history and deletes live file. |
| `.agents/evidence/execution/*` | Append numbered evidence; immutable evidence snapshots by stem and sequence | Number allocated from `stem.NNNN.md`; concrete paths are stored in state/journal/prompt inputs. |
| `.agents/handoffs/handoff.NNNN.md` | Rotate live file into immutable historical sequence | Execution writes live handoff; next decision loop rotates it; latest read falls back to highest historical file. |
| `.agents/journal/transitions.jsonl` | Append logical event; physically rewrite JSONL file | Started/completed/failed records preserve ordering and correlation IDs; legacy records without input snapshots deserialize. |
| `.agents/projections/manifest.json` | Upsert by runtime prompt; overwrite canonical document; derived freshness/validation cache | Rows describe projection files, hashes, validation status, stale status, and causal inputs. |
| `.agents/splits/split-family-*.json` | Immutable-ish lineage record by family ID; lookup by child path; per-family document | Split transition writes family record after child epics; lookup scans family files and can migrate legacy markdown. |

## 5. Filesystem-Coupled Behaviors

### Artifact store semantics

`IArtifactStore` is the shared abstraction for path-based persistence. Its operations are path-shaped: `ExistsAsync`, `ReadAsync`, `ReadAs<T>`, `WriteAsync`, `DeleteAsync`, `ListAsync`, and `ListDirectoriesAsync`.

`FileSystemArtifactStore` implements behavior that current callers rely on:

- `WriteAsync` creates parent directories, writes through a temp file, then atomically replaces or moves the temp file into place.
- `ReadAsync` and `ReadAs<T>` cache by path and file signature.
- `DeleteAsync` evicts cached entries.
- `ListAsync` maps to `Directory.GetFiles`, excludes temp files, and orders with `StringComparer.OrdinalIgnoreCase`.
- `ListDirectoriesAsync` maps to `Directory.GetDirectories` and orders with `StringComparer.OrdinalIgnoreCase`.

`RepositoryArtifactStore` converts between repository-relative paths and absolute filesystem paths. Many higher-level services store repo-relative path strings as identities, not only as locations.

### Sequence numbers from filenames

Several histories use filename scans as their allocation mechanism:

- `LoopArtifacts.PersistDecisionsAsync` computes the next decision history number by scanning `.agents/decisions/decisions.*.md`.
- `LoopArtifacts.RotateLiveHandoffAsync` computes the next handoff history number by scanning `.agents/handoffs/handoff.*.md`.
- `LoopArtifacts.RotateOperationalDeltaAsync` computes the next delta history number by scanning `.agents/deltas/operational_delta.*.md`.
- `RoadmapArtifacts.WriteNumberedEvidenceAsync` computes `stem.NNNN.md` by listing `{stem}.*.md` and taking the max numeric suffix.
- `CompletionArtifacts.WriteNumberedEvidenceAsync` has the same numbered evidence pattern.

These behaviors are not abstract "append" operations today. They are filename-derived allocation contracts. A SQLite migration has to preserve visible sequence identity for any remaining path references or exported artifacts.

### Glob ordering as behavior

Ordering of filesystem listings affects deterministic hashes, prompt content, and archive behavior:

- `RoadmapArtifacts.ListRoadmapSourcePathsAsync` lists `.agents/roadmap/*.md` and orders with `StringComparer.Ordinal`.
- `TransitionInputResolver` orders roadmap source paths, completed epic paths, and other prompt inputs before hashing snapshots.
- `ProjectContextLoader` lists `.agents/ctx/*.md`, requires the expected numbered context files, and rejects unexpected numbered context files.
- `CompletionArtifacts.MoveDirectoryContentsAsync` lists source directory files and moves them in deterministic order.
- `FileSystemArtifactStore.ListAsync` itself uses case-insensitive ordering, which callers sometimes override with ordinal ordering.

### Path strings as durable identities

Multiple structured records persist paths as durable identifiers:

- `ArtifactLifecycleEntryDto.Path` is the lifecycle key.
- `SplitFamilyDto.ChildEpicPaths` and `SelectedChildPath` identify child epics.
- `DecisionLedgerEntryDto.InputArtifactPaths` and `OutputArtifactPaths` record transition effects.
- `RoadmapStatePersistenceDocument.ActiveArtifacts` and transition intent fields store active paths and evidence paths.
- `DerivedArtifactManifestEntry.ArtifactPath` stores the path for generated artifacts.
- `TransitionJournalRecord.OutputPaths` and input snapshots store path/hash pairs.
- Execution and selection provenance causal inputs contain artifact identities, many of which are paths or path-derived identities.

Moving a file to SQLite without preserving path identity will break freshness, transition replay, state summaries, and human-readable provenance.

### Git submodule publishing

`.agents` is treated as a repository-owned submodule by implementation and docs:

- `OrchestrationArtifactPaths` documents `.agents` as a git submodule and notes that parent repositories see it as a gitlink.
- `AgentsSubmodulePublisher` commits and pushes `.agents` state after mutating planning/orchestration operations.
- `CommitGate` excludes `.agents` from main repository commits and treats lone `.agents` changes as bookkeeping rather than application progress.
- `PlanPipeline`, `LoopRunner`, and related workflows publish the `.agents` submodule at specific lifecycle points.

Moving canonical state into SQLite changes the review and propagation model. The filesystem export policy determines what reviewers and external tools can still observe through the `.agents` submodule.

### Live-plus-history conventions

Live artifacts and historical artifacts are coupled:

- Live decisions: `.agents/decisions/decisions.md`
- Historical decisions: `.agents/decisions/decisions.NNNN.md`
- Live handoff: `.agents/handoffs/handoff.md`
- Historical handoffs: `.agents/handoffs/handoff.NNNN.md`
- Live operational delta: `.agents/operational_delta.md`
- Historical deltas: `.agents/deltas/operational_delta.NNNN.md`

Current read behavior often prefers live files and falls back to the highest historical file. For example, `LoopArtifacts.ReadLatestHandoffAsync` and `ReadLatestDecisionsAsync` check the live file first.

### Completion archive behavior

Completion archival is filesystem-shaped:

- `CompletedEpicArchiveService` moves decisions, deltas, handoffs, milestones, review files, details, operational context, and plan into `.agents/archive/completed-epics/{index}`.
- The archive index is currently computed as `ListDirectoriesAsync(archiveRoot).Count + 1`.
- Directory contents are moved as files into archive subdirectories.

If historical decisions, deltas, handoffs, or execution evidence move to SQLite, the completed epic archive contract still has to keep that historical state recoverable with the completed epic.

## 6. SQLite Migration Candidates

### `.agents/decision-ledger.json`

Current implementation is already structured JSON through `DecisionLedgerStore` and `DecisionLedgerPersistenceDocument`.

Facts:

- Schema string: `decision-ledger.v1`.
- Entries are sorted by `DecisionId`.
- Validation rejects malformed decision IDs and duplicate IDs.
- `NextDecisionIdAsync` derives the next `DNNNN` ID from existing entries.
- JSON has authority over legacy `.agents/decision-ledger.md`.
- Legacy markdown is parsed and migrated only when JSON is absent.
- `ExecutionPreparationProvenanceService` hashes this JSON file as a causal input for operational context freshness.

Migration constraints:

- Preserve `DNNNN` IDs exactly.
- Preserve stable ordering by `DecisionId`.
- Define decision-ledger freshness equivalence for the current file-hash input used by execution preparation.
- Treat legacy `.agents/decision-ledger.md` migration as an import compatibility behavior, not as canonical persistence.

### `.agents/execution-preparation-manifest.json`

Current implementation is machine-managed JSON but does not use the strict structured store.

Facts:

- Model schema string: `execution-preparation.v1`.
- Tracks active epic path/hash, milestone spec paths/hashes, and trusted derived artifacts.
- Records causal inputs for milestone specs, operational context, execution prompt, execution plan, and execution milestones.
- `RequireFreshMilestoneSpecPathsAsync` returns manifest milestone spec identities.
- Freshness checks detect active epic drift, spec drift, operational context drift, execution prompt drift, decision ledger drift, missing artifacts, and reduced milestone count.
- Store returns `ExecutionPreparationManifest.Empty` when the JSON is missing, blank, or malformed.

Migration constraints:

- The current malformed JSON behavior is "empty manifest", not hard failure. SQLite migration must make that domain behavior explicit.
- Active milestone spec identities are paths. Moving only the manifest does not remove filesystem coupling.
- The manifest hashes retained filesystem files and the decision ledger. Its freshness model crosses the proposed FS/SQLite boundary.

### `.agents/selection-provenance-manifest.json`

Current implementation is machine-managed JSON with trusted active selection entries.

Facts:

- Model schema string: `selection-provenance.v1`.
- `SelectionProvenanceService` records `.agents/selection.md` as a selection decision artifact.
- Freshness compares current selection cycle, prompt context hash, secondary input hash, retired epic state hash, roadmap source hashes, projection inputs, and roadmap completion context input.
- Store returns empty when JSON is missing, blank, or malformed.

Migration constraints:

- Preserve active/superseded semantics from `DerivedArtifactManifestEntry`.
- Preserve causal input identity and stale reason mapping.
- The current malformed persisted state behavior is silent empty load. SQLite migration must make that domain behavior explicit.
- The selected artifact `.agents/selection.md` is outside the proposed split but is still an implemented active artifact path.

### `.agents/state.json`

Current implementation is strict structured JSON through `RoadmapStateStore`.

Facts:

- Schema string: `roadmap-state.v1`.
- JSON has authority over legacy `.agents/state.md`.
- Legacy markdown migration parses current state, active artifacts, transition metadata, blockers, next valid transitions, decision ledger summary, projection counts, split counts, and retired epics.
- `RoadmapTransitionPersistence` saves state summaries derived from other stores and filesystem listings.

Migration constraints:

- Preserve active artifact path strings and transition intent evidence path strings.
- JSON-over-legacy authority is current behavior. SQLite import precedence must define how legacy and exported filesystem state are reconciled.
- State is partly a projection from other files/manifests. SQLite migration should avoid creating two authorities for derived counts.

### `.agents/artifacts/lifecycle.json`

Current implementation is strict structured JSON through `ArtifactLifecycleStore`.

Facts:

- Schema string: `artifact-lifecycle.v1`.
- Entries are keyed by path and sorted by path.
- Duplicate paths are rejected case-insensitively.
- `UpsertAsync` removes the existing path case-insensitively, appends a new timestamped entry, and saves.
- Legacy `.agents/artifacts/lifecycle.md` migrates if JSON is absent.

Migration constraints:

- Path strings are lifecycle identities. If artifacts remain on disk, SQLite lifecycle rows still need to reference stable repo-relative paths.
- Case-insensitive duplicate handling must be preserved.
- Timestamp and sorting determinism matter in tests.

### `.agents/core/0*.md`

No implemented path pattern was found for `.agents/core/0*.md`.

Related implemented facts:

- `RoadmapArtifactPaths.CoreDirectory` is `.agents/core`.
- `RoadmapArtifactPaths.RoadmapCompletionContext` is `.agents/core/roadmap-completion-context.md`.
- Numbered project context files are implemented under `.agents/ctx/01-purpose.md` through `.agents/ctx/08-vocabulary.md`.
- `ProjectContextLoader` requires those `.agents/ctx` files and rejects unexpected numbered context files.

Migration constraints:

- First determine whether the requested `.agents/core/0*.md` actually means implemented `.agents/ctx/0*.md`.
- Do not migrate `.agents/core/roadmap-completion-context.md` by accident. It is a different implemented artifact and is a major input to selection and roadmap completion flows.

### `.agents/decisions/decisions.NNNN.md`

Current implementation stores historical decisions as markdown files.

Facts:

- `LoopArtifacts.PersistDecisionsAsync` writes both historical `decisions.NNNN.md` and live `decisions.md`.
- `PersistDecisionsAsync` throws if the target historical path already exists.
- `ReadLatestDecisionsAsync` prefers live `decisions.md`, then highest numbered historical decision.
- `RetireLiveDecisionsAsync` deletes only live `decisions.md` after execution consumes it.
- Completion archive moves the entire decisions directory into completed epic archive.

Migration constraints:

- Existing references and archive behavior assume `NNNN` sequence identity remains recoverable.
- Keep live `decisions.md` filesystem behavior intact if only historical files move.
- Update `ReadLatestDecisionsAsync` semantics so fallback to historical SQLite rows matches current highest-numbered behavior.
- Completion archive currently expects historical decisions to remain recoverable as completed epic material.

### `.agents/deltas/operational_delta.NNNN.md`

Current implementation stores historical operational deltas as markdown files.

Facts:

- `DecisionSession.TransferAsync` writes live `.agents/operational_delta.md`.
- The live delta is consumed while evolving `.agents/operational_context.md`.
- `LoopArtifacts.RotateOperationalDeltaAsync` moves live delta to numbered history and deletes the live file.
- Completion archive moves the deltas directory into completed epic archive.

Migration constraints:

- Preserve sequence allocation and live-to-history lifecycle.
- If historical deltas move to SQLite, retained live `.agents/operational_delta.md` still participates in a rotation lifecycle.
- Completion archive currently expects historical deltas to remain recoverable as completed epic material.

### `.agents/evidence/execution/*`

Current implementation stores execution evidence as files under `.agents/evidence/execution`.

Facts:

- `RoadmapExecutionBridge` writes numbered execution evidence through `RoadmapArtifacts.WriteNumberedEvidenceAsync`.
- `CompletionCertificationService` writes execution evidence through `CompletionArtifacts.WriteNumberedEvidenceAsync`.
- `TransitionInputResolver` treats execution evidence paths as prompt inputs for `EvaluateEpicCompletionAndDrift`.
- `RoadmapPromptContextBuilder` reads required execution evidence by path.
- `RoadmapUnblockPlanner` searches execution evidence when planning unblocks.
- Transition intent/state records store evidence paths.

Migration constraints:

- Evidence path strings are user-visible and stored in transition/state records.
- Numbered evidence stem allocation currently depends on directory listing.
- Prompt builders require content by path. Migrated persistence must preserve that access contract for execution evidence.
- Evidence is often debugging material. Removing it from Git-visible `.agents` changes operational auditability.

### `.agents/handoffs/handoff.NNNN.md`

Current implementation stores historical handoffs as markdown files.

Facts:

- `ExecutionStep` writes live `.agents/handoffs/handoff.md`.
- `LoopRunner` rotates live handoff before a new decision session.
- `ReadLatestHandoffAsync` prefers live handoff, then highest numbered historical handoff.
- `DecisionSession.BuildProposalPromptAsync` reads latest handoff as decision context.
- Completion archive moves the handoffs directory into completed epic archive.

Migration constraints:

- Preserve live-first historical fallback.
- Preserve `NNNN` sequence semantics.
- Completion archive currently expects historical handoffs to remain recoverable as completed epic material.

### `.agents/journal/transitions.jsonl`

Current implementation is append-like JSONL, but physically rewrites the whole file.

Facts:

- `TransitionJournalStore.AppendAsync` reads the existing JSONL file, trims trailing whitespace, appends one serialized record, and writes the full content back.
- Records use `JsonSerializerDefaults.Web`.
- Records include correlation ID, previous/attempted state, prompt, projection, prompt contract key, input artifact hashes, output paths, duration, result, parser decision, error message, and optional input snapshot.
- Tests cover started/completed pairs, legacy records without input snapshot, changed inputs during prompt execution, and failure paths.

Migration constraints:

- Preserve append ordering.
- Preserve correlation ID grouping.
- Preserve compatibility with legacy records without snapshots.
- SQLite can improve append atomicity, but tests and any external tooling may expect JSONL export.

### `.agents/projections/manifest.json`

Current implementation is strict structured JSON, duplicated in two projects.

Facts:

- Schema string: `projection-manifest.v1`.
- Entries are keyed by runtime prompt name.
- Entries include projection path, prompt/source/context/projection hashes, generated time, validation status, stale status, provenance status, projection identity, prompt type, causal inputs, and stale reasons.
- Legacy `.agents/projections/manifest.md` migrates if JSON is absent.
- `LoopRelay.Roadmap.Cli` and `LoopRelay.Projections` both have projection manifest stores and model logic.
- Projection markdown files under `.agents/projections/*.md` are separate from the manifest and are not in the proposed SQLite list.

Migration constraints:

- Both projection manifest store implementations currently observe the same persisted artifact and must remain semantically aligned.
- Preserve runtime prompt name as the manifest key.
- Projection markdown bodies are separate filesystem artifacts today; manifest rows currently describe those bodies by path and hash.
- Legacy manifest markdown migration is current import behavior when JSON is absent.

### `.agents/splits/split-family-*.json`

Current implementation stores one split family per JSON file.

Facts:

- Schema string: `split-family.v1`.
- Filename embeds family ID: `.agents/splits/split-family-{FamilyId}.json`.
- `SplitEpicTransition` writes a split family after child epics and lifecycle records.
- `SplitFamilyStore.ExistsForChildAsync` lists JSON files first, then migrates matching legacy markdown files when JSON is absent.
- `RoadmapTransitionPersistence` counts `split-family-*.json` files for state summary.

Migration constraints:

- Preserve family ID and child path identity.
- Preserve `ExistsForChildAsync` lookup behavior by child epic path.
- Roadmap state summaries currently consume split family count.
- The persistence domain includes one-to-many child paths and dependency ordering.

## 7. Filesystem-Retained Artifacts

### `.agents/specs/epic.md`

This is implemented as `OrchestrationArtifactPaths.SpecsEpic`. It is a required planning input and a causal input for execution preparation. It is suitable for filesystem retention because it is markdown, agent/human-facing, and included in prompt context.

Risk if moved:

- Planning preflight and prompt builders expect a readable path.
- Execution preparation freshness records path identity and content hash.

### `.agents/specs/s{n}.md`

Milestone specs are path-identified markdown files. Execution preparation records them as authoritative inputs and requires the active set to remain fresh.

Risk if moved:

- Freshness evaluation would lose direct file hash comparison unless replaced by a path-compatible content resolver.
- Prompt context builders expect concrete spec paths.

### `.agents/plan.md`

The plan is produced and revised by the planning pipeline, read by execution, copied into operational context when needed, and archived on completion. It is a user-facing working document and should remain filesystem-backed under the proposed split.

Risk if moved:

- `ExecutionStep` and planning tests assume path existence.
- Milestone extraction mutates plan content.
- Completion archive currently moves the file.

### `.agents/operational_context.md`

Operational context is actively rewritten by decision transfer and used as an execution and provenance input. It is not merely state; it is prompt material.

Risk if moved:

- Decision transfer profiles explicitly allow reads/writes to this path.
- Execution prompt freshness hashes this file.

### `.agents/operational_delta.md`

The live delta is transient prompt material written before operational context evolution and then rotated. It should remain as a live filesystem artifact if the proposal keeps it.

Risk if moved:

- `DecisionSession.TransferAsync` expects a live file to exist after the transfer prompt.
- The live file is required before rotation.

### `.agents/decisions/decisions.md`

The live decisions file is a queue-like handoff between decision and execution. Keeping it on disk preserves current crash recovery behavior: if it exists, `LoopRunner` skips decision and executes directly.

Risk if moved:

- `LoopRunner` uses existence of live decisions to infer pending execution.
- `ExecutionStep` chooses continue/start prompt based on live decisions.

### `.agents/evals/*.md`

No implemented path was found. If this path is intended to be retained, the roadmap needs to map it to implemented `.agents/evidence/evaluations/*.md` or add a new artifact contract.

Risk if ignored:

- A roadmap could preserve a path pattern that no current producer or consumer uses while missing implemented evaluation evidence.

### `.agents/handoffs/handoff.md`

The live handoff is produced by execution and consumed by decision. It is prompt material and operational context for the next loop.

Risk if moved:

- `ExecutionStep` verifies live handoff exists after the agent turn.
- `DecisionSession` reads latest handoff before proposal.

### `.agents/milestones/m*.md`

Milestones are markdown checklists. Execution and commit gating inspect checkbox progress. The files are also archived on completion.

Risk if moved:

- `MilestoneChecklist` parsing depends on markdown content.
- Commit/progress behavior treats checked items as observable execution progress.

### `.agents/roadmap/*.md`

Roadmap source files are human-authored source documents. Selection provenance and transition input snapshots hash these files.

Risk if moved:

- `RoadmapArtifacts.RequireRoadmapSourcePathsAsync` hard-fails when no roadmap source files exist.
- Selection freshness depends on roadmap source path/hash pairs.

## 8. Cross-Boundary Dependencies

### Execution preparation spans both sides

`ExecutionPreparationProvenanceService` stores machine provenance in `.agents/execution-preparation-manifest.json`, but its causal inputs include retained filesystem files:

- `.agents/epic.md` or active epic path
- `.agents/specs/s{n}.md`
- `.agents/operational_context.md`
- `.agents/execution-prompt.md`
- `.agents/plan.md`
- `.agents/milestones/m*.md`

It also hashes `.agents/decision-ledger.json`, which is proposed for SQLite. Any migration must support freshness comparisons across filesystem content and SQLite content.

### Selection provenance spans roadmap source, projection data, and state

`SelectionProvenanceService` stores machine provenance in `.agents/selection-provenance-manifest.json`, but its freshness model depends on:

- `.agents/selection.md`
- `.agents/core/roadmap-completion-context.md`
- `.agents/roadmap/*.md`
- projection manifest and projection content
- retired epic state

The proposed split omits `.agents/selection.md` and `.agents/core/roadmap-completion-context.md`, but both are implemented active inputs.

### Roadmap state is partly derived

`RoadmapTransitionPersistence` saves `.agents/state.json` from current workflow facts:

- active artifact statuses from filesystem paths
- last decision ID from decision ledger
- split family count from `.agents/splits/split-family-*.json`
- projection counts from projection manifest
- transition intent and evidence paths

If state moves to SQLite, it should remain clear which fields are authoritative and which are derived projections from other stores.

### Transition journal records both retained and migrated outputs

`TransitionJournalRecord` stores output paths and input artifact hashes. These paths can point to retained markdown, JSON manifests, historical files, or evidence files.

Moving historical/evidence artifacts into SQLite still requires old journal records to remain interpretable with stable logical artifact identities.

### Live and historical loop artifacts are split by the proposal

The proposal keeps live decisions, live handoff, and live operational delta on disk, but moves their histories to SQLite.

Current logic does not treat these as independent stores. `LoopArtifacts` implements live reads, fallback reads, rotations, and sequence allocation together. Migration has to preserve these separate lifecycle facts:

- live file existence remains filesystem-based
- historical lookup becomes SQLite-based
- sequence allocation becomes SQLite-based
- completion archive keeps historical loop records recoverable with the completed epic

### Projection manifest moves, projection content remains unclear

The proposal moves `.agents/projections/manifest.json`, but implemented projection files such as `.agents/projections/*.md` are not listed. Current projection freshness stores projection paths and hashes. Moving only the manifest is feasible, but the manifest rows continue to describe filesystem projection bodies.

### Completion archive currently expects files

Completion archiving moves entire directories for decisions, deltas, handoffs, milestones, review files, and active documents. If historical files move to SQLite, completion archive behavior must change before migration or it will silently omit migrated historical records from completed epic archives.

## 9. Compatibility and Migration Constraints

### Preserve legacy import behavior

Several stores already implement legacy markdown migration:

- `RoadmapStateStore`: `.agents/state.md` to `.agents/state.json`
- `DecisionLedgerStore`: `.agents/decision-ledger.md` to `.agents/decision-ledger.json`
- `ArtifactLifecycleStore`: `.agents/artifacts/lifecycle.md` to `.agents/artifacts/lifecycle.json`
- `ProjectionManifestStore`: `.agents/projections/manifest.md` to `.agents/projections/manifest.json`
- `SplitFamilyStore`: `.agents/splits/split-family-*.md` to JSON family files

Current code treats legacy markdown as importable source state when canonical JSON is absent. SQLite migration must define import precedence across legacy markdown, exported filesystem state, and canonical database state.

### Preserve corruption semantics deliberately

Current corruption behavior is inconsistent:

- Strict structured stores throw domain exceptions on malformed JSON or schema mismatch.
- `ExecutionPreparationManifestStore` returns empty on malformed JSON.
- `SelectionProvenanceManifestStore` returns empty on malformed JSON.

SQLite migration must not accidentally normalize these behaviors without test changes and explicit product intent.

### Preserve path identity

Because current manifests, journals, state records, and decision ledger entries persist path strings, migration must preserve repo-relative path identity even when content moves into tables. Historical logical paths like `.agents/handoffs/handoff.0003.md` remain part of the observable state.

### Preserve sequence identity

Historical decisions, deltas, handoffs, and evidence use `NNNN` suffixes. Decision ledger uses `DNNNN`. Split families embed IDs in filenames.

The migration must preserve:

- next-number allocation
- collision behavior
- existing IDs in imported workspaces
- stable ordering for latest/highest historical lookup

### Handle mixed-version workspaces

The repository has multiple CLIs and projects that touch `.agents`:

- `LoopRelay.Plan.Cli`
- `LoopRelay.Cli`
- `LoopRelay.Roadmap.Cli`
- `LoopRelay.Completion`
- `LoopRelay.Projections`

A partial rollout where one CLI writes SQLite and another expects files is high risk. Current code has no single storage adapter that all CLIs already share for migrated domains.

### Define Git/submodule projection behavior

Today `.agents` state is reviewable and publishable through the submodule. Moving canonical state into SQLite changes what `AgentsSubmodulePublisher`, `CommitGate`, reviewers, and external tools observe unless filesystem exports preserve the same review surface.

The implementation constraint is recoverability and reviewability of migrated logical state. Whether that is represented by a committed database file, committed exports, or another projection policy is roadmap design.

### Archive compatibility

Completion archive currently materializes files under `.agents/archive/completed-epics/{index}`. Historical decisions, deltas, handoffs, and execution evidence are part of the operational record.

The implementation constraint is that archived historical state remains recoverable with the completed epic. The storage or export mechanism is roadmap design.

### Backup and manual repair

Moving JSON/manifests to SQLite may improve transactional updates, but it removes simple text repair unless tooling replaces it. This matters most for:

- decision ledger
- transition journal
- lifecycle state
- projection manifest
- execution/selection provenance

The existing legacy markdown migration code suggests manual readability was previously important for several of these artifacts.

## 10. Round-Trip Import/Export Requirements

For migrated artifacts, filesystem representation should be a first-class serialization format for the persistence domain. This section records current import/export requirements without proposing tables or schemas.

| Artifact | Lossless filesystem representation | Existing serialization | Required canonical export | Ordering requirements | Stable identity requirements | Human readability / merge friendliness | Comments / formatting significance | Partial export tolerance | Filename / ID preservation | Byte-stable export target |
|---|---|---|---|---|---|---|---|---|---|---|
| `.agents/decision-ledger.json` | Yes | Structured JSON; legacy markdown import | Canonical JSON or documented equivalent ledger export | Sort by `DecisionId` | Preserve `DNNNN`, prompt, projection path, input/output paths | JSON is readable; legacy markdown was more reviewable | JSON formatting not semantically significant today | Import can tolerate absent legacy markdown when JSON/DB exists | Preserve `DNNNN` IDs | Expected, using deterministic JSON |
| `.agents/execution-preparation-manifest.json` | Yes | Structured JSON without strict store validation | Canonical JSON for manifest and derived artifact entries | Sort milestone specs and active artifacts as model does today | Preserve artifact kind, identity, generator, hash, causal inputs | Mostly machine-readable | Formatting not significant; malformed JSON currently loads empty | Missing manifest currently means empty; partial exports need explicit empty semantics | Preserve path identities and hashes | Expected, except timestamp policy must be explicit |
| `.agents/selection-provenance-manifest.json` | Yes | Structured JSON without strict store validation | Canonical JSON for trusted/superseded selection entries | Sort as `SelectionProvenance.UpsertActive` does today | Preserve artifact kind, identity, generator, hash, causal inputs | Mostly machine-readable | Formatting not significant; malformed JSON currently loads empty | Missing manifest currently means empty; partial exports need explicit empty semantics | Preserve selection artifact path and identity | Expected, except timestamp policy must be explicit |
| `.agents/state.json` | Yes | Structured JSON; legacy markdown import | Canonical JSON state snapshot | Preserve deterministic arrays for active artifacts, blockers, next transitions, retired epics | Preserve state names, active paths, decision IDs, transition intent paths | JSON is readable; legacy markdown was more reviewable | Formatting not significant | Import must distinguish absent state from invalid state | Preserve active artifact paths and retired epic identities | Expected, using deterministic JSON |
| `.agents/artifacts/lifecycle.json` | Yes | Structured JSON; legacy markdown import | Canonical JSON lifecycle entries | Sort by path | Preserve path key, state, updated timestamp, notes | JSON is readable; markdown legacy was more reviewable | Formatting not significant | Partial export risks dropping lifecycle entries unless explicitly marked | Preserve paths case-insensitively | Expected, timestamp policy explicit |
| `.agents/core/0*.md` | Unknown | No implemented serialization found | Unknown until path mismatch is resolved | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown |
| `.agents/decisions/decisions.NNNN.md` | Yes | Markdown files by sequence | Canonical markdown history export | Sort by numeric `NNNN` | Preserve sequence number and logical path | High; these are review/debug artifacts | Markdown body is significant; formatting may be prompt-derived | Import may tolerate missing sequence only if domain marks gap behavior | Preserve filenames and numbers | Expected if body bytes are preserved or canonicalized |
| `.agents/deltas/operational_delta.NNNN.md` | Yes | Markdown files by sequence | Canonical markdown delta history export | Sort by numeric `NNNN` | Preserve sequence number and logical path | High; operational context repair/debug artifact | Markdown body is significant | Import may tolerate missing sequence only if domain marks gap behavior | Preserve filenames and numbers | Expected if body bytes are preserved or canonicalized |
| `.agents/evidence/execution/*` | Yes, if evidence bodies and logical paths are preserved | Markdown evidence files by stem and sequence | Canonical evidence export preserving stem, number, and body | Sort by directory, stem, numeric suffix | Preserve evidence path, stem, suffix, content hash | High; evidence is operational audit material | Body is significant; surrounding serialization must not alter prompt evidence | Partial exports may be valid only when referenced evidence paths are absent by design | Preserve filenames and IDs | Expected for exported evidence files |
| `.agents/handoffs/handoff.NNNN.md` | Yes | Markdown files by sequence | Canonical markdown handoff history export | Sort by numeric `NNNN` | Preserve sequence number and logical path | High; handoffs are review/debug artifacts | Markdown body is significant | Import may tolerate missing sequence only if domain marks gap behavior | Preserve filenames and numbers | Expected if body bytes are preserved or canonicalized |
| `.agents/journal/transitions.jsonl` | Yes | JSON Lines | Canonical JSONL export | Append order | Preserve correlation IDs, timestamps, event order, input snapshots, output paths | Moderate; readable event log | Line formatting not semantic, one record per line is structural | Partial export can represent a truncated journal only if marked as truncated | Preserve correlation IDs | Expected per line with deterministic property order |
| `.agents/projections/manifest.json` | Yes | Structured JSON; legacy markdown import | Canonical JSON projection manifest | Sort by runtime prompt name | Preserve runtime prompt, projection path, hashes, causal inputs | Mostly machine-readable | Formatting not significant | Partial export risks stale/missing projection metadata unless explicit | Preserve projection paths and hashes | Expected, timestamp policy explicit |
| `.agents/splits/split-family-*.json` | Yes | One JSON document per family; legacy markdown import | Canonical per-family JSON export | Sort family files by family ID; preserve child dependency order | Preserve family ID, child paths, selected child | JSON readable; markdown legacy was more reviewable | Formatting not significant | Partial export can omit unrelated families only if import scope is explicit | Preserve filenames and family IDs | Expected for each family file |

## 11. Testing Surface

Existing tests that directly cover the split boundary:

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

Test behaviors to preserve or intentionally change:

- JSON authority over legacy markdown when both exist.
- Legacy markdown migration when JSON is absent.
- Malformed legacy markdown fails without writing JSON for strict stores.
- Malformed execution/selection provenance JSON currently becomes empty.
- Deterministic JSON formatting and ordering.
- Filename-derived next sequence for decisions, deltas, handoffs, and evidence.
- Live-first historical fallback for decisions and handoffs.
- Transition journal started/completed correlation and input snapshot reuse.
- Selection and execution freshness invalidation when retained filesystem inputs change.
- Completion archive movement of active and historical files.
- Planning preflight blocking when retained files already exist.

New tests needed for a SQLite migration:

- Import existing `.agents` JSON/history/evidence into SQLite and verify no identity changes.
- Export SQLite state to filesystem and import it back with logical equality.
- Export -> import -> export byte stability for each migrated domain, with explicit exclusions for non-canonical metadata.
- Domain serializer/deserializer tests for decision history, transition history, evidence, lifecycle, selection provenance, execution preparation, projection metadata, and split lineage.
- Mixed live filesystem plus SQLite history reads for decisions and handoffs.
- Rotation from retained live file to SQLite historical row for decisions, handoffs, and deltas.
- Number allocation after importing existing `NNNN` histories.
- Execution evidence written to SQLite but still consumable by prompt context through logical path identity.
- Completion archive behavior when histories/evidence are SQLite-backed.
- Filesystem export compatibility with `.agents` submodule publishing and Git review workflows.
- Partial export/import behavior for each domain, including whether missing exported artifacts mean absent, unchanged, truncated, or invalid state.
- Corrupt SQLite rows versus current corrupt JSON behavior.
- Downgrade or compatibility export behavior for older CLIs.
- Git/submodule publishing behavior with SQLite-backed state.
- Concurrency or crash tests around transition journal append and multi-artifact updates.
- Clarification tests for `.agents/core/0*.md` versus `.agents/ctx/0*.md`.
- Clarification tests for `.agents/evals/*.md` versus `.agents/evidence/evaluations/*.md`.

## 12. Architectural Pressure Points

### Persistence semantics are embedded inside workflow orchestration

The largest pressure point is that classes named like artifact helpers are not only persistence adapters. They encode lifecycle rules:

- `LoopArtifacts` allocates sequences, writes live plus historical decisions, rotates handoffs, rotates deltas, reads live-first histories, retires live decisions, and seeds operational context.
- `CompletionArtifacts` writes numbered evidence, moves directory contents, and participates in completed epic archive materialization.
- `RoadmapArtifacts` reads roadmap sources, writes numbered evidence, and imposes required-source behavior.

SQLite migration is therefore not a direct replacement of file reads and writes. It requires preserving lifecycle semantics that are currently embedded in workflow orchestration code.

### Path constants are split across projects

`.agents` path constants exist in multiple assemblies:

- `OrchestrationArtifactPaths`
- `RoadmapArtifactPaths`
- `CompletionArtifactPaths`
- `ProjectionArtifactPaths`

The same conceptual artifacts are sometimes named in more than one place. Projection manifest persistence is implemented in both `LoopRelay.Roadmap.Cli` and `LoopRelay.Projections`. A SQLite migration risks divergent behavior unless path and store ownership are consolidated or clearly layered.

### `IArtifactStore` is file-shaped

`IArtifactStore` is useful for filesystem abstraction, but it exposes file operations and glob/list semantics. It is not a semantic persistence interface for decisions, handoffs, evidence, journals, or manifests. SQLite migration should avoid forcing relational state through fake file-list operations unless the goal is only compatibility.

### Histories are not modeled as histories

Historical decisions, handoffs, deltas, and evidence are modeled as files with parsed names. Their lifecycle is spread across `LoopArtifacts`, `RoadmapArtifacts`, and `CompletionArtifacts`.

SQLite migration would benefit from explicit history/evidence store interfaces that expose:

- append/write historical entry
- latest entry
- next sequence
- logical path mapping
- archive/export behavior

### Transaction boundaries are currently weak

Several workflows write multiple artifacts that represent one logical transition:

- transition journal started/completed plus state updates
- decision ledger append plus state persistence
- split family plus child epics plus lifecycle updates
- execution preparation manifest plus derived files
- completion archive moves plus synthesized completed epic record

SQLite can improve atomicity, but only if the migration groups logically related writes. Moving individual JSON files into independent tables without workflow transactions will not fix this.

### Freshness logic depends on hashes of serialized artifacts

Freshness currently hashes file content for retained and machine artifacts. If SQLite rows replace JSON files, hash/version semantics must be explicit. Options include:

- canonical JSON serialization from rows
- row version numbers
- content hashes stored at write time
- logical clock or migration version identity

Without a stable replacement, many freshness checks will become either too stale or not stale enough.

### Git review model is an architectural dependency

Docs and implementation assume repository-owned `.agents` files, not private process state. Moving machine artifacts to SQLite changes:

- what reviewers can diff
- what `AgentsSubmodulePublisher` commits
- what external tooling can inspect
- what state is recoverable from Git history

This may be acceptable, but it is a product-level migration decision.

### Completion archive is not storage-agnostic

The completion archive service moves files and directories directly. It does not ask semantic stores for "all decisions for this epic" or "all handoffs for this epic." Historical artifact migration will require archive refactoring before or during SQLite adoption.

### Some requested paths do not match implementation

The proposed split contains at least two path mismatches:

- `.agents/core/0*.md` versus implemented `.agents/ctx/0*.md`
- `.agents/evals/*.md` versus implemented `.agents/evidence/evaluations/*.md`

These must be resolved before implementation planning, because they affect what is migrated and what remains visible.

## 13. Open Questions

1. Where should canonical SQLite storage live relative to `.agents`, and which filesystem export set should `AgentsSubmodulePublisher` publish?
2. Does `.agents/core/0*.md` mean the implemented `.agents/ctx/0*.md` project context files?
3. Should `.agents/core/roadmap-completion-context.md` remain filesystem-backed? It is omitted from the proposed split but is an implemented key artifact.
4. Does `.agents/evals/*.md` mean implemented `.agents/evidence/evaluations/*.md`?
5. What canonical filesystem export shape should historical execution evidence use?
6. Should malformed execution preparation and selection provenance continue to load as empty after migration?
7. Will older CLIs need to operate against migrated workspaces?
8. What JSONL export compatibility level is required for transition journal debugging and external tooling?
9. What archive-level export contract should preserve migrated histories and evidence with completed epics?
10. Should migration preserve byte-identical canonical JSON hashes, or should freshness baselines be intentionally invalidated after migration?
11. Is SQLite intended only for machine manifests/history, or also for derived prompt projections and project context in later phases?
12. What is the desired conflict model when multiple processes append journal/history rows concurrently?

## 14. Persistence Domain Inventory

This section clusters migrated artifacts by conceptual persistence domain rather than filename. It does not propose tables or schemas.

| Domain | Authoritative records today | Filesystem projections / references today | Lifecycle | Relationships | Expected transactional boundary visible from current code |
|---|---|---|---|---|---|
| Decision history | `.agents/decision-ledger.json`; `.agents/decisions/decisions.NNNN.md` | Live `.agents/decisions/decisions.md`; decision ledger legacy markdown import | Ledger append; loop decision history write; live decision retire after execution | Decision entries reference prompts, projections, inputs, outputs; live decision gates execution | Roadmap decision record append is coupled to transition persistence; loop decision proposal writes numbered history and live file together |
| Operational handoff history | `.agents/handoffs/handoff.NNNN.md` | Live `.agents/handoffs/handoff.md` | Execution writes live handoff; next loop rotates to numbered history | Decision session reads latest handoff as prompt context | Rotation and latest-read behavior are coupled in `LoopArtifacts` |
| Operational delta history | `.agents/deltas/operational_delta.NNNN.md` | Live `.agents/operational_delta.md` | Transfer writes live delta; context evolution consumes it; rotation writes numbered history and deletes live | Operational context evolution depends on live delta content | Transfer, context evolution, and delta rotation form one lifecycle |
| Transition history | `.agents/journal/transitions.jsonl` | JSONL file is both canonical record and readable export today | Append logical events in order | Records correlate started/completed/failed events and retain input snapshots/output paths | Prompt transition execution records started/completed/failure around workflow execution |
| Execution evidence | `.agents/evidence/execution/*` | Concrete evidence paths stored in state, journal, and prompt inputs | Append numbered evidence snapshots by stem | Completion evaluation, unblock planning, and prompt context read evidence by path | Evidence write and subsequent transition state often occur in the same workflow phase |
| Lifecycle state | `.agents/artifacts/lifecycle.json` | Legacy lifecycle markdown import | Upsert by artifact path | Lifecycle entries classify artifact readiness/execution state for other roadmap flows | Promotion, split, bootstrap, and validation flows rely on lifecycle updates matching artifact writes |
| Execution preparation | `.agents/execution-preparation-manifest.json` | Retained specs, active epic, operational context, prompt, plan, and milestones are referenced by path/hash | Snapshot authoritative inputs and active derived artifacts; supersede stale artifacts | Depends on decision ledger hash and retained filesystem prompt artifacts | Manifest updates are coupled to generation of operational context, execution prompt, plan, and milestones |
| Selection provenance | `.agents/selection-provenance-manifest.json` | `.agents/selection.md`; roadmap source files; roadmap completion context; projections | Snapshot trusted selection and supersede older active selections | Depends on projection metadata, roadmap source hashes, retired epic state, and completion context | Selection write and provenance update represent one logical selection cycle |
| Projection metadata | `.agents/projections/manifest.json` | Projection markdown files under `.agents/projections/*.md`; legacy manifest markdown import | Upsert by runtime prompt; validate and mark stale/fresh | Depends on project context files, projection prompt sources, projection body hashes | Projection generation writes/validates body and manifest metadata together |
| Split lineage | `.agents/splits/split-family-*.json` | Legacy split-family markdown import; child epic files referenced by path | Create family record; lookup by child path; selected child tracked | Relates parent proposal, child epic paths, dependency order, selected child, lifecycle | Split transition writes child artifacts, lifecycle entries, and split family record as one lineage event |
| Roadmap state | `.agents/state.json` | Legacy state markdown import | Snapshot current workflow state and transition intent | Summarizes active artifacts, last decision, projection counts, split counts, retired epics | Transition persistence writes state together with journal/decision outcomes |
| Project context / core mismatch | Requested `.agents/core/0*.md`; implemented `.agents/ctx/0*.md` | `.agents/core/roadmap-completion-context.md` is a separate implemented artifact | Unknown for requested pattern; implemented context files are required source inputs | Project context feeds projection generation; completion context feeds selection and completion flows | Cannot define a migration boundary until requested path is reconciled with implemented paths |

## 15. Persistence Domain Classification

This table classifies each persistence domain by its primary observed role. Some domains have secondary behaviors, but the primary classification is the least ambiguous role visible in current code.

| Domain | Primary classification | Reason | Secondary observed behavior |
|---|---|---|---|
| Decision history | Journal | Decisions are appended over time through `DecisionLedgerStore.AppendAsync` and numbered loop decision history. | Also source material for execution preparation and roadmap state summaries. |
| Operational handoff history | Journal | Handoffs are rotated into numbered historical records and read latest-first for the next loop. | Live handoff is a working document before rotation. |
| Operational delta history | Journal | Deltas are produced during transfer and rotated into numbered history. | Live delta is a working document before rotation. |
| Transition history | Journal | Transition records are appended in event order with correlation IDs. | Also a diagnostic snapshot of inputs and outputs. |
| Execution evidence | Snapshot | Evidence captures point-in-time execution/completion facts by numbered path. | Also consumed as prompt input and debugging material. |
| Lifecycle state | Source | Lifecycle entries are authoritative state about artifact readiness/execution status. | Mutable upsert document keyed by path. |
| Execution preparation | Snapshot | Manifest captures authoritative inputs and derived artifacts at a preparation point. | Also acts as a freshness projection over retained inputs. |
| Selection provenance | Snapshot | Manifest captures a selection cycle and trusted active selection artifact. | Also acts as a freshness projection over roadmap/projection inputs. |
| Projection metadata | Projection | Manifest describes generated projection bodies, hashes, freshness, and validation status. | Upserted metadata keyed by runtime prompt. |
| Split lineage | Source | Split family records are authoritative lineage linking child epics and selected child. | Lookup projection by child path is derived from family records. |
| Roadmap state | Snapshot | State file captures current workflow state, active artifacts, intent, and counts. | Contains derived projection counts and decision summaries. |
| Project context / core mismatch | Working document | Implemented `.agents/ctx/0*.md` files are required source documents for projections. | Requested `.agents/core/0*.md` has no observed producer or consumer. |

## 16. Persistence Dependency Graph

Only observed relationships are captured here. This is a graph of current persistence behavior, not a proposed architecture.

| Domain | Depends on | Produces | Projects | Invalidates | Consumes |
|---|---|---|---|---|---|
| Decision history | Prompt transitions; decision session proposal output; input/output artifact paths | Decision ledger entries; numbered decision history; live decision file | Last decision ID; decision ledger hash | Execution preparation freshness when decision ledger hash changes | Roadmap state summaries; execution preparation provenance; execution step through live/latest decision |
| Operational handoff history | Execution step output; live handoff file | Numbered handoff history | Latest handoff fallback | Decision context changes when latest handoff changes | Decision session proposal prompt; completion archive |
| Operational delta history | Transfer output; live operational delta | Numbered operational delta history | Historical transfer record | Operational context evolution if live delta is absent or changed before rotation | Completion archive |
| Transition history | Prompt runner/state machine events; transition input snapshots | JSONL transition records | Debug chronology; correlation groups | No invalidation behavior observed; records are historical | Tests and operational debugging; future replay is implied by stored snapshots but not implemented as replay |
| Execution evidence | Roadmap execution bridge; completion certification service | Numbered execution evidence files | Evidence path/hash inputs | Completion/drift evaluation and unblock planning context when evidence changes or is missing | Transition input resolver; roadmap prompt context builder; unblock planner; state/journal evidence paths |
| Lifecycle state | Promotion, split, bootstrap, artifact management flows | Lifecycle entries keyed by path | Artifact readiness/execution status | Lifecycle validation when path state is missing, stale, or duplicated | Roadmap artifact snapshots; invariant validation; promotion/split flows |
| Execution preparation | Active epic; milestone specs; decision ledger; operational context; execution prompt | Execution preparation manifest | Freshness state for specs, operational context, execution prompt, plan, milestones | Active epic/spec/context/prompt/ledger drift; missing derived artifacts; reduced milestone count | Roadmap artifact snapshot; execution preparation invariant checks; prompt context |
| Selection provenance | Roadmap source; roadmap completion context; projection metadata; retired epic state; selection artifact | Selection provenance manifest | Selection freshness and stale reasons | Roadmap/completion/projection/retired-state drift; superseded selection | Selection freshness checks; transition routing and selection-cycle validation |
| Projection metadata | Project context files; projection prompt sources; projection bodies | Projection manifest entries | Projection freshness, validation status, stale status | Project context drift; prompt source drift; projection body drift | Selection provenance; prompt transitions; projection validation |
| Split lineage | Split proposal; child epic paths; dependency order; selected child; lifecycle writes | Split family records | Exists-for-child lookup; split family count | Roadmap state split count changes; child lookup changes | Split transition; roadmap state summaries |
| Roadmap state | Active artifact statuses; decision ledger; split families; projection manifest; transition intent | Roadmap state snapshot | Current state summary; next transitions; retired epic summary | Transition persistence overwrites current state; upstream counts/paths change snapshot content | Roadmap state machine and transition coordination |
| Project context / core mismatch | Implemented `.agents/ctx/0*.md`; roadmap completion context | Projection inputs; selection/completion context inputs | Project context hash; completion context input hash | Projection freshness and selection freshness when context changes | Projection generation; selection provenance; completion workflows |

## 17. Synchronization Model

This section records observed synchronization facts needed for bidirectional filesystem import/export. It does not define the future synchronization algorithm.

| Domain | Canonical today | Import | Export | Conflict resolution observed today | Identity | Version |
|---|---|---|---|---|---|---|
| Decision history | JSON ledger plus numbered markdown history | Legacy ledger markdown imports when JSON absent; numbered decision files read from filesystem | Existing files are the export surface today | JSON ledger has authority over legacy markdown; numbered decision history collision throws | `DecisionId`; `decisions.NNNN.md`; repo-relative paths | `decision-ledger.v1`; sequence suffix for markdown history |
| Operational handoff history | Live and numbered markdown files | Highest numbered handoff discovered by filename scan; no alternate import format | Existing numbered files are the export surface today | Live handoff wins over historical fallback when both exist | `handoff.NNNN.md`; live `handoff.md` | Sequence suffix |
| Operational delta history | Live and numbered markdown files | Numbered deltas discovered by filename scan | Existing numbered files are the export surface today | Rotation writes next sequence and deletes live delta | `operational_delta.NNNN.md`; live `operational_delta.md` | Sequence suffix |
| Transition history | JSONL file | Legacy records without input snapshots deserialize | Existing JSONL is the export surface today | Append order is preserved; no merge/conflict behavior found | Correlation ID plus event order | Record shape; optional input snapshot compatibility |
| Execution evidence | Numbered evidence files | Evidence paths are consumed by exact path; no alternate import format | Existing evidence files are the export surface today | Next number is max suffix plus one; no merge behavior found | Evidence path; stem; `NNNN` suffix | Sequence suffix and content hash |
| Lifecycle state | JSON lifecycle document | Legacy lifecycle markdown imports when JSON absent | Existing JSON is the export surface today | JSON has authority; duplicate paths rejected case-insensitively | Artifact path | `artifact-lifecycle.v1` |
| Execution preparation | JSON manifest | Missing/blank/malformed manifest loads as empty | Existing JSON is the export surface today | Active trusted artifacts supersede prior active entries | Artifact kind plus identity; active epic path; spec path | `execution-preparation.v1`; generated artifact metadata |
| Selection provenance | JSON manifest | Missing/blank/malformed manifest loads as empty | Existing JSON is the export surface today | Active trusted selections supersede prior active entries | Artifact kind plus identity; selection cycle identity | `selection-provenance.v1`; generated artifact metadata |
| Projection metadata | JSON manifest | Legacy projection manifest markdown imports when JSON absent | Existing JSON is the export surface today | Upsert by runtime prompt name | Runtime prompt name | `projection-manifest.v1`; projection hashes |
| Split lineage | Per-family JSON files | Legacy per-family markdown imports when JSON absent | Existing per-family JSON files are the export surface today | JSON family file wins over same-family legacy markdown during lookup | Family ID; child epic path lookup | `split-family.v1`; family ID in filename |
| Roadmap state | JSON state document | Legacy state markdown imports when JSON absent | Existing JSON is the export surface today | JSON has authority over legacy markdown | Current state; active artifact paths; transition intent | `roadmap-state.v1` |
| Project context / core mismatch | Implemented `.agents/ctx/0*.md`; `.agents/core/roadmap-completion-context.md` | Required files loaded by exact path; unexpected numbered context files rejected | Existing markdown files are the export surface today | Missing required context files fail; requested `.agents/core/0*.md` has no behavior | Context source path; completion context path | Numbered context filename order; content hash |

## 18. Database-Level Observations

These observations identify relational pressure without proposing tables or schemas.

| Domain | Natural identity | Ordering | Mutable | Append-only | One-to-many relationships | Referenced by | Expected transaction boundary visible from current code |
|---|---|---|---|---|---|---|---|
| Decision history | `DecisionId`; historical decision sequence path | `DecisionId`; numeric `NNNN` | Ledger entries are not mutated after append in observed code | Yes for ledger; yes for history files | Decision entry to input paths and output paths | Roadmap state; execution preparation; transition journal; execution loop | Decision append plus transition persistence; loop proposal writes live and historical decision together |
| Operational handoff history | `handoff.NNNN.md` | Numeric `NNNN` | Historical handoffs are not mutated after rotation | Yes | None observed beyond content body | Decision session; completion archive | Live handoff rotation into history |
| Operational delta history | `operational_delta.NNNN.md` | Numeric `NNNN` | Historical deltas are not mutated after rotation | Yes | None observed beyond content body | Completion archive | Transfer live delta, context evolution, rotation |
| Transition history | Correlation ID plus event order | Append order | Existing records are not mutated | Yes | Transition record to input hashes and output paths | Debugging/tests; state transition audit | Started/completed/failed records around prompt or state transition |
| Execution evidence | Evidence logical path | Directory/stem/numeric suffix | Evidence files are not mutated after write in observed code | Yes by numbered evidence append | Evidence path referenced by transition intent, journal, prompt inputs | Completion/drift evaluation; unblock planner; prompt context; state/journal | Evidence write plus transition state update in same workflow phase |
| Lifecycle state | Artifact path | Path sort | Yes; upsert by path | No | Path to state/notes/timestamp | Artifact validation; snapshots; promotion/split flows | Artifact write and lifecycle update should describe the same artifact state |
| Execution preparation | Artifact kind plus identity for active derived artifact; active epic/spec path identities | Sorted milestone specs and artifact entries | Yes; overwrite/supersede snapshot | No | Manifest to causal inputs and derived artifacts | Freshness evaluator; roadmap snapshot; execution readiness checks | Generation of prepared artifacts and manifest provenance update |
| Selection provenance | Selection artifact kind plus identity | Sort as model upsert does today | Yes; active selections can be superseded | No | Selection entry to causal inputs | Selection freshness; transition routing | Selection artifact write and provenance update |
| Projection metadata | Runtime prompt name | Runtime prompt name | Yes; upsert by runtime prompt | No | Manifest entry to project context files, causal inputs, stale reasons | Projection validation; selection provenance; prompt transitions | Projection body generation/validation and manifest update |
| Split lineage | Family ID | Family ID; dependency order within family | No mutation observed after family write | Effectively append/create | Family to child epic paths and dependency order | Split lookup; roadmap state split counts | Child epic writes, lifecycle updates, split family write |
| Roadmap state | Singleton current state snapshot per workspace | Arrays have deterministic ordering in persistence document | Yes; overwritten on transition | No | State to active artifacts, blockers, retired epics, next transitions | Roadmap state machine and transition coordination | Transition result persistence plus journal/decision updates |
| Project context / core mismatch | Context source path | Numbered filename order | Working documents are mutable | No | Context source set to projection outputs | Projection generation; selection/completion inputs | Projection generation consumes a complete, valid context set |

## 19. Persistence Platform Capabilities

This section identifies capabilities the persistence layer already relies on or will need to expose as first-class persistence behavior. It does not propose implementation.

| Capability | Currently implemented where | Currently duplicated where | Implicit or explicit today | Domains depending on it |
|---|---|---|---|---|
| Transactions / atomic write | `FileSystemArtifactStore.WriteAsync` temp-write and replace for single files | Workflow-level multi-artifact writes are spread across loop, roadmap, completion, split, projection services | Explicit for single-file writes; implicit/weak for multi-artifact workflows | All migrated domains; especially split lineage, execution preparation, selection provenance, roadmap state, transition history |
| Import | Structured stores migrate legacy markdown when JSON is absent; split family store migrates legacy family markdown on lookup | State, decision ledger, lifecycle, projection manifest, split family stores each implement import separately | Explicit but domain-local | Decision history, lifecycle state, projection metadata, split lineage, roadmap state |
| Export | Current filesystem artifacts are the export surface; JSON stores write deterministic JSON | Numbered evidence/history exporters are implicit in artifact writers | Implicit; not a named platform capability | All migrated domains, especially Git/submodule review and completion archive flows |
| Snapshots | Roadmap state, execution preparation, selection provenance, transition input snapshots | Snapshot construction appears in state persistence, provenance services, transition input resolver | Explicit at domain level, not platform-level | Roadmap state, execution preparation, selection provenance, transition history, execution evidence |
| Projections | Projection manifest/store; roadmap state summary counts; lifecycle snapshots | Projection logic duplicated in roadmap/projections projects and several snapshot builders | Explicit in projection services; implicit in state summaries | Projection metadata, roadmap state, selection provenance, execution preparation |
| Migration | Legacy markdown-to-JSON migration in structured stores | Repeated across state, decision ledger, lifecycle, projection manifest, split family | Explicit but scattered | Imported existing workspaces across structured domains |
| Versioning | Schema version constants in persistence documents; sequence suffixes; decision IDs | Each document owns its schema/version checks | Explicit for strict stores; weaker for execution/selection manifests | Decision history, lifecycle, projection metadata, split lineage, roadmap state, execution/selection provenance |
| Compatibility | Legacy record deserialization; legacy markdown migration; live-first fallback; path identities | Implemented separately in stores and `LoopArtifacts` | Explicit in some tests; implicit in workflow behavior | Decision history, handoff history, transition history, projection metadata, split lineage |
| Validation | `StructuredDocumentStore<T>` schema validation; domain model validation; `ProjectContextLoader` required-file checks | Strict validation duplicated between roadmap and projections structured stores | Explicit for strict stores; inconsistent for provenance manifests | Lifecycle state, decision history, roadmap state, projection metadata, split lineage, project context |
| Corruption detection | Strict stores throw on malformed JSON/schema; provenance manifest stores load empty on malformed JSON | Divergent per store | Explicit but inconsistent | Roadmap state, decision history, lifecycle, projection metadata, split lineage, execution preparation, selection provenance |
| Repair | Legacy markdown migration can regenerate JSON; manual filesystem edits possible today | No centralized repair service found | Mostly implicit/manual | All filesystem-exported domains; especially ledger, journal, lifecycle, projection metadata |
| Backup | Git-tracked `.agents` submodule and filesystem state | No dedicated persistence backup capability found | Implicit through Git/filesystem | All domains under `.agents` |
| Restore | Legacy import paths and filesystem reads restore state from files | No centralized restore capability found | Implicit through file presence and import-on-load | Structured domains plus histories/evidence through path scans |
| Deterministic serialization | Structured stores write indented JSON and deterministic sorted entries; numbered filenames preserve order | Implemented per document/store | Explicit in tests for several stores | Decision history, lifecycle, projection metadata, split lineage, roadmap state, import/export requirements |
| Deterministic deserialization | Structured stores validate and load schemas; path scans order files | Implemented per store and helper | Explicit in strict stores; implicit in filename scans | All migrated domains with import/export |
| Integrity verification | Hash-based freshness checks; schema validation; duplicate detection | Freshness evaluator, provenance services, structured stores | Explicit but domain-specific | Execution preparation, selection provenance, projection metadata, lifecycle state, roadmap state |
| Logical path resolution | `RepositoryArtifactStore`; path constants; `LoopArtifacts` latest/fallback methods | Spread across path constants and artifact helpers | Implicit in repo-relative path strings | Decision history, handoff history, delta history, execution evidence, journals, state/provenance |

## 20. Roadmap-Relevant Findings

### Must Preserve

- Retained markdown prompt artifacts must remain addressable by repo-relative path.
- Live-first behavior for `.agents/decisions/decisions.md` and `.agents/handoffs/handoff.md`.
- Live-to-history rotation for decisions, handoffs, and operational deltas.
- Existing `NNNN` and `DNNNN` identities during import.
- Path strings persisted in state, provenance, lifecycle, decision ledger, split families, and transition journal.
- Freshness behavior for retained file hash changes.
- Legacy migration behavior for existing workspaces as an import compatibility concern.
- Lossless filesystem export/import for migrated persistence domains.
- Deterministic export -> import -> export behavior, with explicit exceptions for non-canonical metadata.
- Completion archive access to historical decisions, deltas, handoffs, milestones, and evidence.
- Git/submodule reviewability through a defined filesystem export/sync contract.

### Must Change

- Separate runtime domain models, canonical SQLite persistence, and filesystem export serialization.
- Extract persistence lifecycle semantics currently embedded in `LoopArtifacts`, `CompletionArtifacts`, and `RoadmapArtifacts`.
- Introduce semantic stores for migrated concepts instead of relying on directory glob scans:
  - decision ledger
  - execution preparation provenance
  - selection provenance
  - roadmap state
  - lifecycle
  - historical loop decisions
  - historical operational deltas
  - historical handoffs
  - execution evidence
  - transition journal
  - projection manifest
  - split families
- Replace directory-scan-derived sequence allocation with domain-backed allocation while preserving imported visible numbers.
- Update `LoopArtifacts` to bridge retained live files with SQLite histories.
- Update roadmap and completion evidence writers/readers to support SQLite-backed evidence identities.
- Update completion archive behavior so migrated historical records remain recoverable with completed epics.
- Define what `AgentsSubmodulePublisher` and related workflows publish once canonical migrated state lives in SQLite.

### Must Investigate Further

- Resolve `.agents/core/0*.md` versus `.agents/ctx/0*.md`.
- Resolve `.agents/evals/*.md` versus `.agents/evidence/evaluations/*.md`.
- Decide the status of `.agents/core/roadmap-completion-context.md`, `.agents/selection.md`, `.agents/epic.md`, `.agents/execution-prompt.md`, `.agents/details.md`, `.agents/projections/*.md`, `.agents/archive/**`, and non-execution evidence directories. These are implemented but not fully covered by the requested split.
- Determine whether SQLite migration should preserve canonical JSON hash values or establish new freshness versions.
- Determine whether malformed provenance manifests should keep current silent-empty behavior.
- Inventory any external scripts or user workflows that inspect `.agents` JSONL/JSON/markdown directly.

### Nice-to-Have Cleanup

- Consolidate duplicated projection manifest persistence between `LoopRelay.Roadmap.Cli` and `LoopRelay.Projections`.
- Centralize `.agents` path ownership to reduce drift across roadmap, completion, projection, planning, and orchestration projects.
- Make history and evidence concepts explicit domain stores with logical path mapping.
- Add first-class import/export command surfaces for SQLite-backed audit artifacts.
- Add a storage compatibility test suite that runs core workflows against filesystem-backed and SQLite-backed stores.
- Normalize structured store corruption behavior where product requirements allow it.
