# Project Goal

Build a persistence architecture where **human-facing `.agents` artifacts remain filesystem-backed**, while **machine-managed persistence domains move to SQLite as canonical storage**, with **lossless two-way filesystem export/import** for every migrated domain.

The intended system preserves current path identities, sequence identities, freshness behavior, Git-reviewable exports, completion archive recoverability, and legacy workspace compatibility while replacing scattered file/glob persistence with domain-backed, transactional storage. This roadmap follows the implementation-roadmap prompt and the revised SQLite persistence audit.  

---

# Guiding Principles

* **SQLite becomes canonical only for machine-managed domains.** Retained markdown prompt/workflow artifacts continue to live on the filesystem.

* **Filesystem export is first-class.** Exported files are not disposable compatibility shims; they are deterministic, importable serializations of SQLite-backed state.

* **Path identity must survive migration.** Existing references such as `.agents/handoffs/handoff.0003.md` or `.agents/evidence/execution/foo.0001.md` remain meaningful even when backed by database rows.

* **Live workflow files remain live files.** Current live artifacts such as `.agents/decisions/decisions.md`, `.agents/handoffs/handoff.md`, and `.agents/operational_delta.md` stay filesystem-backed while their histories move to SQLite.

* **Behavior moves before storage changes.** Current filesystem behavior should first be expressed as domain behavior, then backed by SQLite.

* **Import/export must be round-trip safe.** Filesystem → SQLite → filesystem should preserve logical state, stable identities, ordering, and canonical serialization.

* **Freshness must remain trustworthy.** Existing hash-based freshness behavior needs a database-compatible equivalent before canonical files are removed.

* **Capability increments should stand alone.** Each milestone should leave the software able to do something objectively useful that it could not do before.

---

# Roadmap

## Milestone 1 — File-Backed Domain Persistence Surface

### Implementation Objective

Introduce domain-level persistence behavior for the artifacts that will later move to SQLite, while still using the existing filesystem as the backing store.

This makes current persistence semantics executable through domain concepts instead of raw file paths, globs, and ad hoc serialization.

### Why Now

The system currently embeds persistence behavior inside workflow helpers and artifact utilities. SQLite cannot be introduced safely while sequence allocation, live/history rotation, journal append behavior, evidence writing, lifecycle upserts, and manifest snapshots are still only expressed as filesystem operations.

This milestone creates the minimum executable boundary needed before any storage engine changes.

### Capability Gained

The software can read, write, append, rotate, upsert, and list migrated persistence domains through semantic persistence operations while preserving current filesystem behavior.

Examples of domain behavior that becomes explicitly executable:

* decision ledger append
* roadmap state snapshot save/load
* artifact lifecycle upsert
* split family lookup by child path
* transition journal append
* historical decision/handoff/delta lookup
* numbered evidence write
* projection manifest upsert
* execution and selection provenance snapshot behavior

### Major Implementation Areas

* Domain persistence contracts for migrated artifact categories
* File-backed implementations preserving current behavior
* Shared behavior for sequence allocation, live/history fallback, append ordering, and path identity
* Conformance tests proving the domain surface matches current filesystem behavior
* Removal of persistence semantics from workflow code only where needed to make the new domain behavior authoritative

### Acceptance Criteria

* Existing workflows continue to pass using file-backed domain persistence.
* Historical decisions, handoffs, deltas, and evidence still allocate the same next `NNNN` values as before.
* Live-first reads for decisions and handoffs behave exactly as before.
* Structured JSON artifacts still serialize deterministically.
* Legacy markdown import behavior remains unchanged for stores that already support it.
* No migrated domain requires callers to directly glob its backing directory to obtain current behavior.

### Enables

* Deterministic import/export
* SQLite-backed implementations
* Cross-store conformance tests
* Later workflow transactions

---

## Milestone 2 — Lossless Filesystem Serialization for Migrated Domains

### Implementation Objective

Implement deterministic import/export serialization for every domain that will move to SQLite, still using filesystem-backed state as the source of truth.

This establishes the filesystem representation as a first-class external format before SQLite becomes canonical.

### Why Now

Two-way export/import is a core requirement of the refactor. It should not be added after SQLite migration as a compatibility patch. The software needs to know how to parse and regenerate all migrated filesystem equivalents before those equivalents become projections.

### Capability Gained

The software can load the current filesystem representation of migrated state into domain snapshots and export those snapshots back to canonical filesystem form.

Supported domains include:

* decision ledger
* execution preparation provenance
* selection provenance
* roadmap state
* artifact lifecycle
* historical decisions
* historical operational deltas
* historical handoffs
* execution evidence
* transition journal
* projection manifest
* split lineage

### Major Implementation Areas

* Domain serializers and deserializers
* Canonical JSON and JSONL generation
* Canonical markdown body preservation for historical records and evidence
* Import validation for malformed, missing, partial, duplicate, or out-of-order exports
* Byte-stability rules for exportable files
* Explicit non-canonical metadata handling for timestamps or runtime-generated values

### Acceptance Criteria

* Filesystem import produces a complete logical snapshot of all migrated domains.
* Exporting that snapshot regenerates the expected filesystem tree.
* Filesystem → domain snapshot → filesystem is byte-stable for stable domains.
* Filesystem → domain snapshot → filesystem preserves all logical identities even when byte stability is not required.
* Imported historical sequences preserve existing `NNNN` values.
* Imported decision ledger entries preserve existing `DNNNN` values.
* Missing optional domains preserve current empty-state behavior.
* Malformed required state produces deterministic validation failures.

### Enables

* SQLite import from legacy workspaces
* SQLite export back to filesystem
* Compatibility with Git review workflows
* Deterministic storage conformance testing

---

## Milestone 3 — Logical Artifact Identity and Freshness Resolution

### Implementation Objective

Introduce a runtime capability for resolving artifact identity, content, and freshness across retained filesystem artifacts and future SQLite-backed artifacts.

### Why Now

Current state, provenance, journal, lifecycle, split, and evidence records persist repo-relative path strings. Freshness checks depend on file hashes. Once content moves into SQLite, path references and hashes must still resolve consistently.

This capability must exist before any domain becomes SQLite-canonical.

### Capability Gained

The software can treat logical artifact paths as stable identities independent of whether the content currently lives on disk, in SQLite, or in an exported filesystem projection.

### Major Implementation Areas

* Logical artifact identity resolution
* Content lookup by repo-relative path
* Canonical hash generation for SQLite-backed records
* Compatibility resolution for historical paths
* Freshness comparison across filesystem and database-backed content
* Clear failure behavior for unresolved logical paths

### Acceptance Criteria

* A retained filesystem artifact resolves to its existing file content and hash.
* A migrated artifact can resolve to equivalent logical content through its domain store.
* Existing path references inside state, journal, provenance, lifecycle, split lineage, and decision records remain interpretable.
* Freshness checks produce the same result in file-backed mode as before.
* Missing migrated artifacts produce domain-specific stale or invalid results instead of silent path failures.
* Legacy historical paths such as numbered handoff, decision, delta, and evidence files can be resolved after import.

### Enables

* SQLite-backed provenance and state
* SQLite-backed evidence
* Completion archives that can recover migrated records
* Export/import conflict detection

---

## Milestone 4 — SQLite Workspace Store and Importable Database State

### Implementation Objective

Add a SQLite-backed workspace store capable of initializing, validating, versioning, and importing migrated persistence domains from their filesystem representation.

### Why Now

The system now has domain persistence behavior, filesystem serialization, and logical identity resolution. SQLite can be introduced as a storage engine without changing workflow semantics yet.

### Capability Gained

A workspace can create a canonical SQLite database from existing `.agents` machine-managed files and validate that the imported database represents the same logical state.

### Major Implementation Areas

* SQLite database initialization
* Schema version tracking
* Store integrity validation
* Transaction support
* Filesystem import into SQLite
* Database snapshot comparison against imported filesystem state
* Corruption and incompatible-version detection

### Acceptance Criteria

* A workspace with existing `.agents` state can import migrated domains into SQLite.
* Import preserves all logical identities, sequence numbers, timestamps, hashes, and path references.
* Re-import is idempotent when source state has not changed.
* Import fails safely on duplicate identities, invalid schemas, or incompatible partial state.
* Database integrity checks can distinguish valid empty state, valid imported state, corrupt state, and unsupported schema versions.
* Existing workflows can still run against filesystem-backed persistence after database import.

### Enables

* Selective migration of canonical domains to SQLite
* Database-backed conformance tests
* Transactional persistence
* Export from SQLite back to filesystem

---

## Milestone 5 — Core Roadmap State Runs Canonically from SQLite

### Implementation Objective

Move core structured roadmap state domains to SQLite as canonical persistence while preserving deterministic filesystem export/import.

Initial canonical domains:

* decision ledger
* roadmap state
* artifact lifecycle
* split lineage

### Why Now

These domains are already structured, identity-driven, and machine-managed. They establish the core database-backed state model before more content-heavy markdown histories and evidence move.

They also drive transition state, lifecycle validation, split behavior, and decision identity.

### Capability Gained

Roadmap workflows can use SQLite as the authoritative store for core machine state while still exporting filesystem equivalents that older tooling, Git review, and import paths can understand.

### Major Implementation Areas

* SQLite-backed decision ledger append and next-ID allocation
* SQLite-backed roadmap state load/save
* SQLite-backed lifecycle upsert by path
* SQLite-backed split family creation and lookup by child path
* Filesystem export for each domain
* Filesystem import precedence between legacy markdown, exported JSON, and canonical database state
* Existing transition behavior updated to use canonical domain stores

### Acceptance Criteria

* New decision ledger entries are written to SQLite and exported to deterministic filesystem form.
* `DNNNN` allocation preserves imported IDs and advances correctly.
* Roadmap state saves and loads from SQLite without losing active paths, transition intent, blockers, retired epics, split counts, or projection counts.
* Lifecycle entries remain keyed by case-insensitive path identity.
* Split family lookup by child path works from SQLite after importing existing `split-family-*.json` files.
* Exported filesystem files can be deleted and regenerated from SQLite without logical loss.
* Importing regenerated exports into a clean SQLite database produces equivalent state.

### Enables

* SQLite-backed provenance domains
* Database-backed transition persistence
* Full workspace export/import
* Later multi-domain transactions

---

## Milestone 6 — Provenance and Projection Metadata Run Canonically from SQLite

### Implementation Objective

Move machine-managed provenance and projection metadata to SQLite while preserving current freshness behavior and deterministic export/import.

Canonical domains:

* execution preparation provenance
* selection provenance
* projection manifest

### Why Now

These domains depend on retained filesystem artifacts, core roadmap state, decision ledger identity, projection paths, and freshness hashing. Core state and logical artifact resolution need to exist first.

### Capability Gained

Execution preparation, selection, and projection freshness can be evaluated from SQLite-backed metadata while still referencing retained filesystem artifacts and exported projection bodies by stable path.

### Major Implementation Areas

* SQLite-backed execution preparation manifest behavior
* SQLite-backed selection provenance behavior
* SQLite-backed projection manifest behavior
* Freshness evaluation using logical artifact identity
* Deterministic export/import for provenance and projection metadata
* Preservation of current malformed/empty behavior where required
* Consolidated runtime behavior for duplicated projection manifest persistence

### Acceptance Criteria

* Execution preparation freshness detects the same retained-file drift as before.
* Selection freshness detects the same roadmap source, projection, retired epic, and completion-context drift as before.
* Projection metadata is keyed by runtime prompt identity and preserves projection paths, hashes, stale status, validation status, and causal inputs.
* Exported manifests can be imported back into SQLite with logical equality.
* Missing manifests preserve current empty-state behavior where that behavior is part of the domain contract.
* Malformed exported provenance fails or loads empty according to the domain-specific behavior selected for compatibility.

### Enables

* SQLite-backed transition journal input snapshots
* Database-compatible freshness baselines
* Reliable export/import of complete machine metadata
* Migration of evidence and historical markdown references

---

## Milestone 7 — Transition Journal Runs Canonically from SQLite with JSONL Interchange

### Implementation Objective

Move transition history from JSONL file persistence to SQLite while preserving append ordering, correlation grouping, legacy record compatibility, and JSONL export/import.

### Why Now

The journal records cross-domain transitions and references both retained filesystem artifacts and migrated machine artifacts. Core state, provenance, and logical identity resolution need to exist before it becomes canonical.

### Capability Gained

The software can record transition history transactionally in SQLite while still exporting the same operational chronology as JSONL.

### Major Implementation Areas

* SQLite-backed append-only transition events
* Started/completed/failed event correlation
* Legacy JSONL import
* Deterministic JSONL export
* Input snapshot preservation
* Output path preservation
* Ordering guarantees independent of filesystem append behavior

### Acceptance Criteria

* New transition events append to SQLite in order.
* Started/completed/failed records preserve correlation IDs.
* Legacy JSONL records without input snapshots import successfully.
* Exported JSONL preserves event order and logical event content.
* JSONL export imported into a clean database reproduces equivalent journal state.
* Transition history remains interpretable when output paths reference SQLite-backed artifacts.
* Concurrent or repeated appends do not corrupt event order.

### Enables

* Transactional workflow recording
* Crash-safe transition auditing
* Full import/export of operational history
* Workflow-level atomicity

---

## Milestone 8 — Loop Histories Move to SQLite While Live Files Stay on Filesystem

### Implementation Objective

Move historical loop records to SQLite while preserving live filesystem workflow files and current live-first behavior.

Canonical SQLite histories:

* `.agents/decisions/decisions.NNNN.md`
* `.agents/handoffs/handoff.NNNN.md`
* `.agents/deltas/operational_delta.NNNN.md`

Retained live files:

* `.agents/decisions/decisions.md`
* `.agents/handoffs/handoff.md`
* `.agents/operational_delta.md`

### Why Now

The system can already preserve path identity, sequence identity, import/export, and journal references. Historical markdown records can now move without breaking live workflow semantics.

### Capability Gained

Loop execution can keep using live filesystem handoff files while historical records are stored transactionally in SQLite and exported back to their numbered markdown paths.

### Major Implementation Areas

* SQLite-backed historical decision records
* SQLite-backed historical handoff records
* SQLite-backed historical operational delta records
* Live-to-history rotation into SQLite
* Highest-number historical fallback from SQLite
* Deterministic markdown export/import for histories
* Sequence allocation after import

### Acceptance Criteria

* A new decision proposal writes the live decision file and a SQLite historical decision record with the correct next `NNNN`.
* Execution still consumes and retires the live decision file.
* Execution still writes the live handoff file.
* The next decision loop rotates the live handoff into SQLite history.
* Operational delta transfer still writes the live delta file, evolves operational context, and rotates historical delta state into SQLite.
* Reading latest decisions and handoffs still prefers live files before historical records.
* Export regenerates `decisions.NNNN.md`, `handoff.NNNN.md`, and `operational_delta.NNNN.md` files with preserved sequence numbers.
* Importing regenerated histories preserves sequence allocation and latest fallback behavior.

### Enables

* SQLite-backed completion archives
* Reduced filesystem churn for machine histories
* Full loop-state round-trip export/import
* Migration of evidence histories using the same logical-path model

---

## Milestone 9 — Execution Evidence Moves to SQLite with Path-Compatible Access

### Implementation Objective

Move execution evidence content to SQLite while preserving logical evidence paths, numbered stems, prompt consumption, unblock planning, completion evaluation, and deterministic filesystem export/import.

### Why Now

Evidence is heavily path-referenced by state, journal, prompt inputs, and completion workflows. It should move only after logical artifact resolution and historical markdown import/export are proven.

### Capability Gained

Execution evidence can be stored canonically in SQLite while all existing workflows that reference evidence by path continue to resolve and consume the correct content.

### Major Implementation Areas

* SQLite-backed evidence body storage
* Evidence logical path preservation
* Stem and numeric suffix allocation
* Evidence content hashing
* Prompt-context evidence resolution
* Unblock-planning evidence search
* Completion-evaluation evidence access
* Deterministic filesystem export/import for evidence files

### Acceptance Criteria

* New execution evidence writes to SQLite with a stable logical path matching the exported filesystem path.
* Evidence numbering preserves existing stem and `NNNN` behavior after import.
* Prompt builders can read required evidence by logical path.
* Unblock planning can search SQLite-backed execution evidence.
* Completion evaluation can consume SQLite-backed execution evidence.
* Export regenerates evidence files under the expected execution evidence paths.
* Importing exported evidence into a clean database preserves body content, logical path, hash, stem, and sequence.
* Missing referenced evidence produces the same stale, invalid, or blocked behavior as missing filesystem evidence did previously.

### Enables

* Complete migration of the requested machine-managed artifact set
* Completion archive recovery of DB-backed evidence
* Full workspace export/import
* Reduced Git churn from execution evidence

---

## Milestone 10 — Completed Epic Archives Preserve DB-Backed Historical State

### Implementation Objective

Update completion behavior so completed epic archives remain recoverable when historical decisions, deltas, handoffs, and execution evidence are SQLite-backed.

### Why Now

Completion currently expects historical records to exist as files. Once histories and evidence move into SQLite, archive behavior must preserve the same recoverability contract without relying on directory moves.

### Capability Gained

Completing an epic captures all required historical state even when that state is canonically stored in SQLite.

### Major Implementation Areas

* Archive association for migrated historical records
* Completion-time export or archive materialization behavior
* Recovery of archived histories and evidence
* Preservation of retained filesystem artifacts in existing archive flow
* Import/export behavior for completed epic historical state

### Acceptance Criteria

* Completing an epic preserves live retained files exactly as before where those files remain filesystem-backed.
* Historical decisions, handoffs, deltas, and execution evidence remain recoverable with the completed epic after completion.
* Exported archive state includes migrated historical records in deterministic filesystem form.
* Importing an exported completed-epic archive restores the same logical archived state.
* Archive indexing remains deterministic and compatible with existing completed-epic discovery behavior.
* No migrated historical record is silently dropped during completion.

### Enables

* Safe end-to-end SQLite operation across full epic lifecycle
* Git-reviewable archive exports
* Backup/restore of completed work
* Removal of filesystem historical directories as canonical state

---

## Milestone 11 — Bidirectional Workspace Synchronization

### Implementation Objective

Provide complete workspace-level synchronization between canonical SQLite state and filesystem export state.

This includes full import, full export, selective domain export/import, conflict detection, and compatibility behavior for mixed-version workspaces.

### Why Now

Individual domains now support export/import. The system needs a coherent workspace-level capability so users, Git workflows, older integrations, and database-backed execution can interoperate safely.

### Capability Gained

A workspace can move between SQLite-canonical state and filesystem-equivalent state intentionally and safely.

Supported flows include:

* existing filesystem workspace → SQLite database
* SQLite database → filesystem export
* SQLite database → filesystem export → clean SQLite database
* selective export/import of individual persistence domains
* compatibility export for Git/submodule publishing
* detection of stale or conflicting filesystem exports

### Major Implementation Areas

* Workspace import command behavior
* Workspace export command behavior
* Domain-scoped synchronization
* Conflict detection between database state and filesystem export state
* Export freshness markers or equivalent validation behavior
* Mixed-version workspace safeguards
* `.agents` submodule publish integration for exported state

### Acceptance Criteria

* A full workspace export regenerates all filesystem equivalents for migrated domains.
* A full workspace import from generated exports produces an equivalent SQLite database.
* Export → import → export is stable according to each domain’s canonical serialization rules.
* Selective domain import/export works without corrupting unrelated domains.
* Conflicting filesystem exports are detected before they overwrite newer canonical database state.
* Workspaces with database state and stale exported files fail safely or require explicit reconciliation.
* Submodule publishing can publish the intended filesystem export surface.
* Older filesystem-only state can be imported without losing legacy-supported data.

### Enables

* Practical daily use of SQLite-canonical persistence
* Git-reviewable DB-backed workflows
* Backup/restore through filesystem exports
* Safer rollout across multiple CLIs and integrations

---

## Milestone 12 — Transactional Workflow Persistence and Recovery

### Implementation Objective

Make multi-domain workflow updates transactionally safe across SQLite-backed domains and retained filesystem artifacts.

### Why Now

Once most machine-managed domains are SQLite-backed, correctness depends on transitions updating related records atomically. The migration should not merely place old file-shaped state into tables; it should improve recovery from partial writes, crashes, and concurrent operations.

### Capability Gained

Workflow transitions can commit or fail as coherent persistence changes, reducing partial-state corruption and making recovery deterministic.

### Major Implementation Areas

* Transactional boundaries for roadmap transitions
* Transactional decision recording and state updates
* Transactional split lineage creation
* Transactional provenance updates after artifact generation
* Transactional journal started/completed/failed recording
* Crash recovery for partially completed workflow phases
* Integrity validation across related domains
* Concurrency behavior for append and sequence allocation

### Acceptance Criteria

* A failed transition does not leave committed state that claims outputs exist when their required records do not.
* Decision ledger updates and roadmap state updates remain consistent after failure.
* Split lineage, child references, and lifecycle state remain consistent after failure.
* Journal events accurately represent started, completed, and failed transitions even when workflow execution errors.
* Sequence allocation remains unique under concurrent attempts.
* Integrity validation detects orphaned evidence, orphaned history records, missing logical paths, duplicate identities, and invalid archive references.
* Recovery behavior can distinguish retryable partial work from corrupt state.
* Existing retained filesystem artifacts are not overwritten inconsistently with committed SQLite state.

### Enables

* Reliable production use of SQLite persistence
* Safer concurrent or repeated CLI invocations
* Stronger corruption detection and recovery
* Confidence in retiring canonical filesystem persistence for migrated domains

---

## Milestone 13 — Storage Compatibility and Verification Mode

### Implementation Objective

Add an executable verification mode that proves filesystem-backed and SQLite-backed persistence produce equivalent domain behavior for supported workflows.

### Why Now

The migration touches many workflows and several CLIs. After canonical SQLite operation exists, the software needs a behavior-level compatibility capability that can validate migrated workspaces and prevent regressions.

### Capability Gained

Users and tests can verify that a workspace’s database state, filesystem exports, retained filesystem artifacts, and logical artifact references are mutually consistent.

### Major Implementation Areas

* Storage conformance execution across file-backed and SQLite-backed modes
* Workspace integrity verification
* Export/import equivalence checks
* Freshness verification across retained and migrated artifacts
* Archive recoverability checks
* Logical path resolution checks
* Domain-specific corruption detection

### Acceptance Criteria

* Verification succeeds for a valid SQLite-canonical workspace with fresh exports.
* Verification succeeds for a valid legacy filesystem workspace after import.
* Verification detects stale exports relative to canonical database state.
* Verification detects missing exported files for required migrated records.
* Verification detects unresolved logical paths in state, journal, provenance, lifecycle, split lineage, and evidence references.
* Verification detects non-deterministic export/import behavior.
* Verification detects archive records that cannot be recovered.
* Verification can run without mutating canonical state unless explicitly asked to repair or re-export.

### Enables

* Safe rollout of the refactor
* Regression protection across all migrated domains
* Confidence in bidirectional synchronization
* Compatibility checks for future persistence changes

---

# Epic Recommendation

This should be implemented as a **parent roadmap with milestone-level Epics**.

The work is cohesive as one architectural/product objective: **move machine-managed `.agents` persistence to SQLite while retaining filesystem-backed human artifacts and providing lossless filesystem import/export**.

However, the implementation naturally crosses several independently reviewable capability boundaries:

1. **Domain persistence parity**
2. **Filesystem serialization**
3. **SQLite storage foundation**
4. **Structured state migration**
5. **Provenance and projection migration**
6. **Journal migration**
7. **Loop history migration**
8. **Evidence migration**
9. **Archive compatibility**
10. **Workspace synchronization**
11. **Transactional recovery**
12. **Verification**

Each milestone leaves the software more capable on its own, but the full intended system emerges only when all are complete. A single Epic would be too large to review safely, while unrelated Epics would obscure the dependency chain. A parent roadmap with milestone-level Epics gives the best balance of cohesion, sequencing, and engineering reviewability.
