# Milestone 2 — Lossless Filesystem Serialization for Migrated Domains Deep Dive

<!-- GENERATED:START -->
Generated from `.agents/specs/roadmap.md` and `.agents/specs/audit.md`.

## 1. Milestone Summary

- Milestone Identifier: Milestone 2
- Milestone Name: Lossless Filesystem Serialization for Migrated Domains
- Short Description: Define deterministic import/export serialization for every migrated domain while the filesystem is still the source of truth.
- Implementation Role: Establishes the filesystem representation as a first-class interchange format before SQLite becomes canonical.
- Roadmap Position: Second milestone; depends on Milestone 1 domain behavior and precedes SQLite initialization.
- Primary Outcomes:
- Filesystem state can be loaded into complete domain snapshots.
- Domain snapshots can regenerate the expected filesystem export tree.
- Round-trip tests preserve identity, order, and canonical bytes where required.

## 2. Normative Basis

**Roadmap Authority**
- Roadmap authority: `.agents/specs/roadmap.md` `# Project Goal`, `# Guiding Principles`, and this milestone's `## Milestone` section.
- `.agents/specs/roadmap.md` `## Milestone 2 — Lossless Filesystem Serialization for Migrated Domains`.
**Architectural Authority**
- Architectural authority: `.agents/specs/audit.md` sections `2. Persistence Authority`, `4. Persistence Semantics`, `5. Filesystem-Coupled Behaviors`, `8. Cross-Boundary Dependencies`, `9. Compatibility and Migration Constraints`, `10. Round-Trip Import/Export Requirements`, `14. Persistence Domain Inventory`, `16. Persistence Dependency Graph`, `17. Synchronization Model`, `19. Persistence Platform Capabilities`, and `20. Roadmap-Relevant Findings`.
**Implementation Authority**
- Implementation authority: existing LoopRelay persistence code under `src/LoopRelay.Roadmap.Cli/Services`, `src/LoopRelay.Cli/Services/Execution`, `src/LoopRelay.Completion/Services/ArtifactStorage`, `src/LoopRelay.Projections/Services`, `src/LoopRelay.Core/Abstractions/Artifacts/IArtifactStore.cs`, and `src/LoopRelay.Infrastructure/Services/Artifacts`.
**Supporting Context**
- Supporting context: current tests under `tests/LoopRelay.Roadmap.Cli.Tests`, `tests/LoopRelay.Cli.Tests`, `tests/LoopRelay.Completion.Tests`, `tests/LoopRelay.Projections.Tests`, and `tests/LoopRelay.Infrastructure.Tests`.
- No undocumented authority is introduced here; unresolved audit mismatches remain open questions rather than implementation permission.

## 3. Objective

Implement serializers and deserializers that transform current filesystem representations of migrated domains into validated domain snapshots and export those snapshots back to deterministic filesystem form.

## 4. Non-Goals

- Do not make SQLite canonical or create database schema.
- Do not alter workflow persistence behavior; the filesystem remains source of truth.
- Do not migrate retained live files or human-facing roadmap/spec/plan artifacts.
- Do not resolve ambiguous audit-only paths by invention; `.agents/core/0*.md` and `.agents/evals/*.md` stay open unless the roadmap is clarified.
- Do not introduce non-canonical metadata as required for byte-stable domains.

## 5. Runtime / System State Before

- Milestone 1 domain stores expose file-backed behavior.
- The existing filesystem tree is canonical but import/export is implicit, not an executable domain capability.
- Structured JSON stores write deterministic documents in some domains; markdown histories and evidence are only file collections.
- Malformed, missing, partial, duplicate, and out-of-order states are handled inconsistently by individual stores.

## 6. Runtime / System State After

- Every roadmap-listed migrated domain has an importable domain snapshot representation.
- Export regenerates canonical filesystem files for migrated domains.
- Stable domains pass filesystem -> snapshot -> filesystem byte-stability checks.
- Identity-preserving domains document any non-byte-stable metadata exceptions.
- Malformed required exports fail deterministically.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| Domain snapshot import | Serialization layer | Read all migrated filesystem artifacts into typed snapshots. | Decision ledger, provenance manifests, roadmap state, lifecycle, histories, evidence, journal, projection manifest, split lineage. | Validated in-memory domain snapshot set. | Milestone 1 domain contracts. | Complete logical snapshot is produced for valid filesystem state. | SQLite import, conformance tests, workspace synchronization. |
| Canonical filesystem export | Serialization layer | Write snapshots back to deterministic filesystem representation. | Domain snapshots and export options for scoped domains. | Regenerated JSON, JSONL, and markdown files. | Canonical ordering and formatting rules. | Exported tree matches expected identity, order, and bytes where stable. | Git review, backup/restore, SQLite export. |
| Import validation | Domain serializers | Detect malformed, missing, partial, duplicate, and out-of-order exports. | Filesystem export tree. | Domain snapshot or deterministic validation failure. | Per-domain corruption compatibility rules. | Required malformed state fails deterministically; optional missing domains preserve empty behavior. | SQLite importer, verification mode. |
| Byte-stability rules | Serializer policy | Define stable serialization for JSON, JSONL, and markdown bodies. | Domain snapshot content and metadata. | Canonical export bytes or declared logical-equivalence exception. | Audit round-trip requirements. | Filesystem -> snapshot -> filesystem is byte-stable for stable domains. | Workspace sync, conformance tests, verification. |

## 8. Architectural Responsibilities

- Serializers own interchange shape; domain stores own persistence behavior.
- Domain snapshots own logical equality; filesystem exports own canonical byte representation.
- Validation authority remains per domain, including strict versus empty-on-malformed compatibility.
- No serializer becomes a second runtime authority over state while the filesystem remains canonical.

**Cross-Milestone Constraints**
- SQLite becomes canonical only for machine-managed migrated domains.
- Retained markdown prompt/workflow artifacts remain filesystem-backed.
- Filesystem exports are deterministic, importable serializations of migrated domain state.
- Repo-relative path strings remain durable logical identities.
- `DNNNN`, `NNNN`, family IDs, runtime prompt names, and correlation IDs preserve imported identity.
- Behavior moves behind semantic domain operations before callers stop using filesystem representations.
- Freshness semantics remain trustworthy across retained files and migrated records.

## 9. Components and Modules

**Domain snapshot model set**
- Purpose: Represent imported logical state independent of backing files.
- Responsibilities: Carry identities, content bodies, hashes, sequences, ordering, and validation results.
- Owned State: In-memory snapshots only.
- Consumed State: Current filesystem files and domain model DTOs.
- Public Contracts: Internal import/export APIs.
- Internal Contracts: Snapshots are immutable after validation.
- Dependencies: Milestone 1 domain contracts.
- Tests Required: Snapshot equality tests for every domain.
**Filesystem import readers**
- Purpose: Parse exported `.agents` files into snapshots.
- Responsibilities: Read JSON, JSONL, markdown histories, evidence files, legacy-supported state, and optional missing domains.
- Owned State: None.
- Consumed State: Filesystem export tree.
- Public Contracts: Domain-scoped import methods.
- Internal Contracts: Deterministic ordering and domain-specific failure behavior.
- Dependencies: `IArtifactStore` and serializers.
- Tests Required: Malformed, duplicate, missing, and legacy fixtures.
**Filesystem export writers**
- Purpose: Generate canonical external filesystem form.
- Responsibilities: Write deterministic files, preserve markdown bodies, order records, and avoid duplicate appends.
- Owned State: Export files only.
- Consumed State: Validated snapshots.
- Public Contracts: Domain-scoped export methods.
- Internal Contracts: Overwrite generated files idempotently within export scope.
- Dependencies: Canonical serializer policies.
- Tests Required: Export/import/export byte-stability tests.

## 10. Repository and File Impact

- `src/LoopRelay.Roadmap.Cli/Services/Artifacts` and `RoadmapArtifactPaths` remain the source of existing roadmap artifact path constants until a shared path layer replaces them.
- `src/LoopRelay.Roadmap.Cli/Services/State`, `TransitionState`, `TransitionCoordination`, `Decisions`, `ExecutionPreparation`, `Projections`, `Splits`, and `ArtifactManagement` contain the current roadmap domain stores.
- `src/LoopRelay.Cli/Services/Execution/LoopArtifacts.cs` owns live decision, handoff, and operational delta loop-file behavior today.
- `src/LoopRelay.Completion/Services/ArtifactStorage` owns completed-epic archive and evidence access used by completion workflows.
- `src/LoopRelay.Infrastructure/Services/Artifacts` contains repository-relative path bridging and numbered artifact sequence helpers.
- Expected serializer code can live beside domain stores or under a roadmap persistence/serialization namespace.
- Fixture directories should model full `.agents` trees, domain-scoped exports, malformed exports, and legacy markdown-only inputs.
- No CLI command is required unless an existing test harness needs an executable import/export entry point.

## 11. Public Contracts

- Domain-scoped import/export APIs for migrated persistence domains.
- Canonical filesystem export shapes for JSON documents, JSONL transition records, markdown histories, and evidence files.
- Validation result/error contracts that identify domain, path, identity, and failure reason.

## 12. Internal Contracts

- Import never allocates new identities for existing exported records.
- Export never changes logical path identities, sequence suffixes, decision IDs, family IDs, or correlation IDs.
- Optional missing domains load as empty only where current domain behavior permits it.
- Partial export/import is valid only when scoped explicitly to a domain.

## 13. Data and State Model

**Workspace import snapshot**
- Owner: Serialization layer
- Lifecycle: Created during import, consumed by tests and later SQLite import.
- Durability: Transient.
- Mutability: Immutable after validation.
- Identity: Composite of domain identities and exported paths.
- Validation: Per-domain validators.
- Recovery: Re-read from filesystem export.
- Consumers: SQLite importer, verification, conformance tests.
**Filesystem export tree**
- Owner: Export writer
- Lifecycle: Regenerated from snapshots on demand.
- Durability: Filesystem/submodule as today.
- Mutability: Export scope is overwritten deterministically.
- Identity: Repo-relative paths.
- Validation: Round-trip import and equality checks.
- Recovery: Regenerate from snapshot or future SQLite state.
- Consumers: Git review, backup/restore, legacy tools.

## 14. Lifecycle and State Transitions

- Import lifecycle: Discover export scope -> parse domain files -> validate identity/order/schema -> produce snapshot -> report domain failures.
- Export lifecycle: Validate snapshot -> select domain scope -> render canonical files -> write generated files -> verify by re-importing when requested.
- Failure lifecycle: first deterministic validation error identifies domain and path; no partial snapshot is treated as complete.

## 15. Execution Flow

- Startup flow is test/tool initialization only; workflows still use file-backed stores.
- Normal import reads filesystem files through the same path abstraction and produces snapshots.
- Normal export writes to a target filesystem tree using canonical ordering.
- Failure flow rejects malformed required state or duplicate identities before any snapshot is certified.
- Recovery flow fixes or removes invalid export files and reruns import.

## 16. Dependency Closure

- Hard prerequisite: Milestone 1 domain surface.
- Supporting infrastructure: deterministic JSON/JSONL serializers, markdown body preservation, `IArtifactStore` path access.
- Inherited capability: current legacy markdown import behavior for state, ledger, lifecycle, projection manifest, and splits.
- Future dependency: SQLite importer in Milestone 4.
- Explicitly unavailable dependency: database canonical state.
- Enables Milestones 4, 5, 6, 7, 8, 9, 10, 11, and 13.

## 17. Failure Modes

**Duplicate identity**
- Description: Export contains repeated decision IDs, lifecycle paths, runtime prompts, family IDs, or logical evidence paths.
- Detection: Snapshot validation.
- Behavior: Import fails before producing a complete snapshot.
- Recovery: Remove or reconcile duplicate exported records.
- Diagnostics: Domain, identity, and source paths.
- Tests: Duplicate fixtures for every keyed domain.
**Out-of-order or gap-sensitive histories**
- Description: Numbered histories are malformed or ambiguous.
- Detection: Filename parser and sequence validator.
- Behavior: Preserve numbers and either allow gaps if current behavior tolerates them or fail if identity is invalid.
- Recovery: Rename or restore valid sequence files.
- Diagnostics: Path, stem, suffix, expected pattern.
- Tests: Invalid filename, duplicate suffix, and gap fixtures.
**Non-canonical export drift**
- Description: Export/import/export changes stable bytes.
- Detection: Byte comparison test.
- Behavior: Fail stability check.
- Recovery: Fix ordering, formatting, or non-canonical metadata policy.
- Diagnostics: File path and byte diff summary.
- Tests: Golden export fixtures.

## 18. Validation and Invariants

**Filesystem -> domain snapshot -> filesystem preserves logical state for every migrated roadmap domain.**
- Source Authority: Roadmap Milestone 2 acceptance criteria.
- Enforcement Point: Round-trip tests.
- Failure Behavior: Import/export certification fails.
- Test Strategy: Full workspace fixtures plus domain-scoped fixtures.
**Imported historical sequences and decision IDs preserve existing values.**
- Source Authority: Roadmap Milestone 2 acceptance criteria.
- Enforcement Point: Snapshot identity validation.
- Failure Behavior: Fail import on identity mutation.
- Test Strategy: Fixtures with nontrivial `DNNNN` and `NNNN` values.
**Malformed required state produces deterministic validation failures.**
- Source Authority: Roadmap Milestone 2 acceptance criteria.
- Enforcement Point: Import validator.
- Failure Behavior: Fail with domain/path diagnostics.
- Test Strategy: Malformed JSON, JSONL, markdown, and missing required-file tests.

## 19. Testing Strategy

- Unit tests for each domain serializer/deserializer.
- Integration tests importing a full representative `.agents` tree.
- Contract tests for byte-stable JSON, JSONL, and markdown body preservation.
- Regression tests for current legacy import and malformed behavior.
- Failure-path tests for missing required files, malformed required files, duplicates, partial scoped imports, and invalid sequence names.
- Performance smoke tests importing/exporting large history and evidence sets.

## 20. Fixtures and Test Data

- Minimal valid export for each supported domain.
- Full mixed export with all supported domains populated.
- Malformed JSON, malformed JSONL, invalid markdown history filenames, duplicate identities, and missing required state.
- Optional-domain-absent fixtures for execution and selection provenance.
- Legacy markdown-only fixtures for current import-compatible structured domains.
- Stable golden export tree for export/import/export comparison.

## 21. Acceptance Demonstration

- Setup a fixture `.agents` tree containing all roadmap-listed migrated domains.
- Run filesystem import into domain snapshots.
- Export snapshots into a clean temporary `.agents` tree.
- Import the generated tree again and compare logical equality.
- For stable domains, compare generated files against golden bytes.

## 22. Certification Evidence

- Passing serializer unit tests.
- Full workspace round-trip transcript or test output.
- Golden-file byte comparison output for stable domains.
- Validation failure output for malformed required-state fixtures.

## 23. Implementation Plan

**Model snapshots**
- Purpose: Represent each migrated domain without filesystem authority.
- Deliverables: Immutable snapshot types and equality rules.
- Dependencies: Milestone 1 domain contracts.
- Completion: All roadmap-listed domains have snapshot shape.
**Implement import readers**
- Purpose: Parse current filesystem state losslessly.
- Deliverables: Domain import methods and validation errors.
- Dependencies: Snapshot types.
- Completion: Valid and malformed fixtures covered.
**Implement export writers**
- Purpose: Regenerate canonical filesystem form.
- Deliverables: Domain export methods with deterministic ordering.
- Dependencies: Import readers and canonical policies.
- Completion: Export/import/export tests pass.
**Wire full-workspace snapshot**
- Purpose: Combine domain snapshots for later SQLite import.
- Deliverables: Workspace snapshot aggregate.
- Dependencies: Domain serializers.
- Completion: Full fixture imports as one logical state.

## 24. Parallel Work Opportunities

**Structured JSON domains**
- Owner: Roadmap state engineer
- Dependencies: Snapshot equality rules.
- Sync: Shared deterministic JSON options.
- Risk: Schema/legacy precedence drift.
**Markdown histories and evidence**
- Owner: Loop/completion engineer
- Dependencies: Logical path model.
- Sync: Shared filename parser.
- Risk: Body canonicalization accidentally changes evidence.
**Journal JSONL**
- Owner: Transition engineer
- Dependencies: Record compatibility rules.
- Sync: Workspace snapshot aggregate.
- Risk: Legacy no-snapshot records lose compatibility.

## 25. Risks and Mitigations

**Serializer becomes an alternate authority**
- Class: architectural
- Impact: Runtime behavior diverges from domain stores.
- Likelihood: medium
- Detection: Conformance mismatch.
- Mitigation: Keep serializers snapshot-only and test against domain stores.
- Fallback: Route serialization through domain model constructors.
**Markdown body mutation**
- Class: data
- Impact: Prompt/evidence content changes during round trip.
- Likelihood: medium
- Detection: Golden byte tests.
- Mitigation: Store and export exact bodies unless domain explicitly canonicalizes.
- Fallback: Use body byte preservation for histories/evidence.
**Ambiguous path patterns**
- Class: integration
- Impact: Migrating nonexistent or wrong artifacts.
- Likelihood: medium
- Detection: Open question review and fixture absence.
- Mitigation: Do not include audit-only ambiguous paths without roadmap clarification.
- Fallback: Treat as explicitly unavailable dependency.

## 26. Observability and Diagnostics

- Import/export diagnostics report domain, path, identity, and validation code.
- Round-trip verification reports logical equality and byte-stability separately.
- No runtime metrics are required because canonical workflow behavior does not change.

## 27. Performance and Scalability Considerations

- Baseline expectation is full workspace import/export completes comfortably for typical `.agents` trees.
- Likely bottlenecks are large execution evidence directories and JSONL journals.
- Measure import/export duration and file counts in smoke tests.
- Deferred optimization: incremental or domain-scoped sync indexes.

## 28. Security and Safety Considerations

- Reject export paths that escape repository-relative `.agents` scope.
- Do not execute or interpret markdown bodies; preserve as data.
- Avoid destructive cleanup outside explicit export scope.
- Validate untrusted import files before later database insertion.

## 29. Documentation Updates

- No documentation-only deliverable is required.
- If executable import/export APIs expose CLI diagnostics later, their help text should reflect actual validation behavior.

## 30. Exit Criteria

- All roadmap-listed migrated domains have import and export support.
- Full workspace and domain-scoped round trips pass.
- Stable domains are byte-stable or have explicit non-canonical metadata exceptions.
- Malformed required state fails deterministically.
- No SQLite canonical behavior is claimed.

## 31. Transition to Next Milestone

- Milestone 4 receives importable snapshots and canonical export rules for database initialization.
- Milestone 11 receives the domain-level foundation for workspace synchronization.
- Remaining limitation: synchronization/conflict detection is not implemented yet.

## Open Implementation Questions

- The audit notes `.agents/core/0*.md` has no implemented producer/consumer and may mean `.agents/ctx/0*.md`; this milestone must not migrate it without roadmap clarification.
- The audit notes `.agents/evals/*.md` is not implemented and may mean `.agents/evidence/evaluations`; this milestone must not infer that mapping.
- Timestamp and runtime-generated metadata byte-stability must be declared per domain before golden tests are final.

<!-- GENERATED:END -->

<!-- MANUAL:START -->
<!-- Add manually maintained notes here. This block is preserved by the generator. -->
<!-- MANUAL:END -->
