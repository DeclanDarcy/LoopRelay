# Milestone 9 — Execution Evidence Moves to SQLite with Path-Compatible Access Deep Dive

<!-- GENERATED:START -->
Generated from `.agents/specs/roadmap.md` and `.agents/specs/audit.md`.

## 1. Milestone Summary

- Milestone Identifier: Milestone 9
- Milestone Name: Execution Evidence Moves to SQLite with Path-Compatible Access
- Short Description: Move execution evidence content to SQLite while preserving logical evidence paths, numbering, prompt consumption, search, completion evaluation, and export/import.
- Implementation Role: Completes migration of the requested machine-managed content set before archive and workspace synchronization.
- Roadmap Position: Ninth milestone; depends on logical path resolution and proven history migration.
- Primary Outcomes:
- New execution evidence writes to SQLite with stable path-compatible identities.
- Prompt builders, unblock planning, and completion evaluation consume SQLite-backed evidence.
- Export/import preserves evidence bodies, hashes, stems, and sequence numbers.

## 2. Normative Basis

**Roadmap Authority**
- Roadmap authority: `.agents/specs/roadmap.md` `# Project Goal`, `# Guiding Principles`, and this milestone's `## Milestone` section.
- `.agents/specs/roadmap.md` `## Milestone 9 — Execution Evidence Moves to SQLite with Path-Compatible Access`.
**Architectural Authority**
- Architectural authority: `.agents/specs/audit.md` sections `2. Persistence Authority`, `4. Persistence Semantics`, `5. Filesystem-Coupled Behaviors`, `8. Cross-Boundary Dependencies`, `9. Compatibility and Migration Constraints`, `10. Round-Trip Import/Export Requirements`, `14. Persistence Domain Inventory`, `16. Persistence Dependency Graph`, `17. Synchronization Model`, `19. Persistence Platform Capabilities`, and `20. Roadmap-Relevant Findings`.
**Implementation Authority**
- Implementation authority: existing LoopRelay persistence code under `src/LoopRelay.Roadmap.Cli/Services`, `src/LoopRelay.Cli/Services/Execution`, `src/LoopRelay.Completion/Services/ArtifactStorage`, `src/LoopRelay.Projections/Services`, `src/LoopRelay.Core/Abstractions/Artifacts/IArtifactStore.cs`, and `src/LoopRelay.Infrastructure/Services/Artifacts`.
**Supporting Context**
- Supporting context: current tests under `tests/LoopRelay.Roadmap.Cli.Tests`, `tests/LoopRelay.Cli.Tests`, `tests/LoopRelay.Completion.Tests`, `tests/LoopRelay.Projections.Tests`, and `tests/LoopRelay.Infrastructure.Tests`.
- No undocumented authority is introduced here; unresolved audit mismatches remain open questions rather than implementation permission.

## 3. Objective

Implement SQLite-backed execution evidence storage that preserves logical evidence paths, numbered stem allocation, content hashing, path-compatible reads/search, prompt and completion consumers, and deterministic filesystem export/import.

## 4. Non-Goals

- Do not migrate non-execution evidence directories unless the roadmap later requires them.
- Do not change retained prompt/plan/spec artifacts or live loop files.
- Do not implement completed-epic archive recovery; Milestone 10 owns that.
- Do not alter evidence body content or prompt semantics.

## 5. Runtime / System State Before

- Execution evidence files live under `.agents/evidence/execution/*`.
- Evidence paths are stored in state, journal, prompt inputs, unblock planning, and completion evaluation.
- Numbering uses stem plus `NNNN` filename scans.
- Logical resolver can resolve migrated paths in principle but evidence is still file-backed.

## 6. Runtime / System State After

- Execution evidence bodies are canonical SQLite records.
- New evidence writes receive stable logical paths matching exported filesystem paths.
- Evidence numbering preserves existing stem/sequence behavior after import.
- Prompt, unblock, and completion consumers resolve/read/search SQLite-backed evidence by logical path.
- Export regenerates evidence files under expected execution evidence paths.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| SQLite evidence body storage | Execution evidence store | Persist evidence bodies with logical paths, stems, suffixes, hashes, and metadata. | Evidence stem/body and current sequence state. | Canonical evidence row and logical path. | SQLite substrate and logical resolver. | New evidence writes to SQLite with stable logical path matching exported path. | Execution bridge, completion certification, prompt context. |
| Stem/suffix allocation | Evidence store | Allocate `stem.NNNN.md` identities from imported and current records. | Stem and existing evidence rows. | Next logical evidence path. | Imported evidence identities. | Numbering preserves existing stem and `NNNN` behavior after import. | Roadmap execution bridge and completion evidence writer. |
| Path-compatible evidence access | Logical resolver and evidence store | Read evidence by stored path and search execution evidence. | Logical path or search query/scope. | Evidence body, hash, metadata, or missing/stale result. | Milestone 3 resolver. | Prompt builders, unblock planning, and completion evaluation consume SQLite-backed evidence. | Roadmap prompt context, unblock planner, completion evaluation. |
| Evidence export/import | Evidence serializer | Regenerate and import execution evidence files. | SQLite rows or filesystem evidence tree. | Exported files or imported rows. | Milestone 2 markdown/body preservation. | Clean import preserves body, path, hash, stem, and sequence. | Workspace sync, archive export, verification. |

## 8. Architectural Responsibilities

- Execution evidence store owns content bodies and sequence allocation for `.agents/evidence/execution`.
- Completion and roadmap evidence writers use the same domain store behavior.
- Logical resolver owns path-compatible read access for consumers.
- Search consumers define query semantics; evidence store provides indexed/readable execution evidence content without exposing file scans.

**Cross-Milestone Constraints**
- SQLite becomes canonical only for machine-managed migrated domains.
- Retained markdown prompt/workflow artifacts remain filesystem-backed.
- Filesystem exports are deterministic, importable serializations of migrated domain state.
- Repo-relative path strings remain durable logical identities.
- `DNNNN`, `NNNN`, family IDs, runtime prompt names, and correlation IDs preserve imported identity.
- Behavior moves behind semantic domain operations before callers stop using filesystem representations.
- Freshness semantics remain trustworthy across retained files and migrated records.

## 9. Components and Modules

**SQLite domain store**
- Purpose: Persist execution evidence bodies and metadata in canonical database tables.
- Responsibilities: Implement the same domain operations as the file-backed store.
- Owned State: SQLite rows for execution evidence bodies and metadata.
- Consumed State: Imported filesystem snapshot and retained logical path references.
- Public Contracts: Existing domain store interface.
- Internal Contracts: All writes happen through transaction-aware store methods.
- Dependencies: SQLite workspace store and serialization layer.
- Tests Required: File-backed versus SQLite conformance tests.
**Filesystem importer/exporter**
- Purpose: Preserve deterministic round-trip compatibility.
- Responsibilities: Read exported files into domain snapshots and regenerate canonical exports.
- Owned State: No canonical state; produces interchange snapshots.
- Consumed State: SQLite rows and legacy filesystem files.
- Public Contracts: Workspace sync/import/export operations where available.
- Internal Contracts: Canonical ordering and identity preservation.
- Dependencies: Milestone 2 serializers.
- Tests Required: Export/import/export stability tests.
**Workflow integration adapter**
- Purpose: Route existing workflows to canonical domain stores.
- Responsibilities: Keep current callers behavior-compatible while changing backing storage.
- Owned State: None.
- Consumed State: Domain store results and retained filesystem artifacts.
- Public Contracts: Existing workflow behavior.
- Internal Contracts: No direct SQL or file export dependency in workflow logic.
- Dependencies: Domain store and logical path resolution where needed.
- Tests Required: Workflow regression and failure-path tests.

## 10. Repository and File Impact

- `src/LoopRelay.Roadmap.Cli/Services/Artifacts` and `RoadmapArtifactPaths` remain the source of existing roadmap artifact path constants until a shared path layer replaces them.
- `src/LoopRelay.Roadmap.Cli/Services/State`, `TransitionState`, `TransitionCoordination`, `Decisions`, `ExecutionPreparation`, `Projections`, `Splits`, and `ArtifactManagement` contain the current roadmap domain stores.
- `src/LoopRelay.Cli/Services/Execution/LoopArtifacts.cs` owns live decision, handoff, and operational delta loop-file behavior today.
- `src/LoopRelay.Completion/Services/ArtifactStorage` owns completed-epic archive and evidence access used by completion workflows.
- `src/LoopRelay.Infrastructure/Services/Artifacts` contains repository-relative path bridging and numbered artifact sequence helpers.
- Update `RoadmapArtifacts.WriteNumberedEvidenceAsync` usage and `CompletionArtifacts.WriteNumberedEvidenceAsync` usage through an evidence domain store.
- Update `TransitionInputResolver`, `RoadmapPromptContextBuilder`, `RoadmapUnblockPlanner`, and completion certification evidence reads.
- Tests extend execution bridge, completion certification, transition input resolver, prompt context, unblock planner, and export/import suites.

## 11. Public Contracts

- Logical evidence paths remain under `.agents/evidence/execution/{stem}.NNNN.md`.
- Evidence body reads by path remain available to prompt and completion consumers.
- Missing referenced evidence produces the same stale, invalid, or blocked behavior as missing filesystem evidence.
- Exported evidence files remain deterministic and Git-reviewable.

## 12. Internal Contracts

- Evidence writes allocate path and hash atomically with body storage.
- Existing referenced evidence paths resolve through the evidence store after import.
- Search over execution evidence uses canonical SQLite rows in database mode, not exported files.
- Export/import preserves body content as opaque text.

## 13. Data and State Model

**Execution evidence rows**
- Owner: Execution evidence store
- Lifecycle: Created by execution/completion evidence writes; read by prompts/evaluation/search; exported.
- Durability: SQLite canonical.
- Mutability: Append-only content snapshots.
- Identity: Logical path, stem, and `NNNN` suffix.
- Validation: Unique path/stem/sequence and content hash.
- Recovery: Export/import or database backup.
- Consumers: Prompt context, unblock planning, completion evaluation, journal/state references.
**Evidence content hash**
- Owner: Evidence store/hash service
- Lifecycle: Computed at write/import and recomputed for verification.
- Durability: SQLite row and/or derived metadata.
- Mutability: Immutable for evidence body.
- Identity: Hash algorithm plus logical path.
- Validation: Compare body hash to stored hash.
- Recovery: Recompute from body.
- Consumers: Freshness, transition inputs, verification.

## 14. Lifecycle and State Transitions

- Write: Request stem/body -> allocate next suffix -> compute hash -> insert row -> return logical path.
- Read: Consumer path -> resolver classifies evidence -> evidence store returns body/hash or missing result.
- Search: Query/scope -> evidence store searches canonical rows -> returns logical paths and excerpts/metadata as current consumers require.
- Export/import: Rows -> files; files -> rows preserving path/stem/sequence/body/hash.

## 15. Execution Flow

- Startup validates evidence table and indexes.
- Normal execution writes evidence to SQLite and stores logical paths in state/journal/provenance.
- Prompt/completion consumers read by logical path through resolver.
- Failure flow reports missing evidence with consumer-specific stale/invalid/blocked status.
- Recovery flow imports exported evidence or regenerates missing evidence through workflow.

## 16. Dependency Closure

- Hard prerequisite: Milestone 3 logical path resolution.
- Hard prerequisite: Milestone 4 SQLite substrate.
- Hard prerequisite: Milestone 8 path-compatible history sequence model.
- Inherited capability: Milestone 2 evidence export/import body preservation.
- Future dependency: Milestone 10 archive recovery for DB-backed evidence.
- Enables Milestones 10, 11, 12, and 13.

## 17. Failure Modes

**Duplicate evidence path**
- Description: Write/import attempts to reuse existing stem/suffix path.
- Detection: Unique constraint and importer validation.
- Behavior: Fail write/import; do not overwrite evidence.
- Recovery: Repair export or retry with next sequence.
- Diagnostics: Logical path and stem/sequence.
- Tests: Duplicate path fixture.
**Referenced evidence missing**
- Description: State/journal/prompt references an absent evidence row.
- Detection: Resolver lookup.
- Behavior: Return stale/invalid/blocked according to consuming workflow.
- Recovery: Restore/import evidence or update state through valid workflow.
- Diagnostics: Referring record and evidence path.
- Tests: Missing evidence consumer tests.
**Evidence hash mismatch**
- Description: Stored hash does not match body after import or corruption.
- Detection: Integrity verification.
- Behavior: Mark evidence invalid/corrupt and block trusted consumption.
- Recovery: Re-import valid evidence or restore backup.
- Diagnostics: Path, stored hash, computed hash.
- Tests: Corrupted body/hash fixture.

## 18. Validation and Invariants

**Evidence logical paths remain stable and path-compatible.**
- Source Authority: Milestone 9 objective and acceptance criteria.
- Enforcement Point: Path allocation and resolver tests.
- Failure Behavior: Path mutation or unresolved reference.
- Test Strategy: Import/export and consumer resolution fixtures.
**Evidence body, hash, stem, and sequence are preserved across export/import.**
- Source Authority: Milestone 9 acceptance criteria.
- Enforcement Point: Round-trip tests and hash checks.
- Failure Behavior: Logical equality mismatch.
- Test Strategy: Golden evidence fixtures.
**Prompt/completion consumers do not require physical evidence files in SQLite mode.**
- Source Authority: Milestone 9 capability gained.
- Enforcement Point: Tests deleting exports before consumption.
- Failure Behavior: Consumer file read fails.
- Test Strategy: Delete exported evidence and run prompt/completion tests.

## 19. Testing Strategy

- Unit tests for evidence write, allocation, read, search, hash validation, and export/import.
- Integration tests for roadmap execution bridge and completion certification writing SQLite-backed evidence.
- Prompt context and transition input resolver tests reading evidence by logical path.
- Unblock planner search tests over SQLite-backed evidence.
- Failure-path tests for missing referenced evidence, duplicate path, corrupted hash, and deleted exports.
- Performance smoke tests for large evidence search/read/export sets.

## 20. Fixtures and Test Data

- Evidence files with multiple stems and nontrivial suffixes.
- Evidence bodies with markdown formatting and long content.
- State/journal/provenance references to evidence paths.
- Duplicate path and invalid stem fixtures.
- Deleted export fixture where SQLite rows still exist.
- Corrupted body/hash fixture.

## 21. Acceptance Demonstration

- Import existing `.agents/evidence/execution/*` into SQLite.
- Write new execution evidence with a known stem and verify next logical path.
- Delete exported evidence files and run prompt context, unblock planning, and completion evaluation reads.
- Export evidence files, import into clean database, and compare body/path/hash/stem/sequence equality.

## 22. Certification Evidence

- Evidence store test output.
- Prompt/unblock/completion consumer test output with exports removed.
- Evidence export/import equality report.
- Corruption/missing evidence diagnostic output.

## 23. Implementation Plan

**Implement evidence store**
- Purpose: Persist evidence content and identity in SQLite.
- Deliverables: Write/read/search/allocation APIs.
- Dependencies: SQLite substrate and resolver.
- Completion: Store tests pass.
**Integrate writers**
- Purpose: Route roadmap and completion evidence creation to the domain store.
- Deliverables: Updated evidence writer call sites.
- Dependencies: Evidence store.
- Completion: Execution/completion tests write SQLite evidence.
**Integrate consumers**
- Purpose: Make prompt, unblock, and completion readers path-compatible.
- Deliverables: Resolver-backed reads/search.
- Dependencies: Evidence store/resolver.
- Completion: Consumers pass with exported files absent.
**Add export/import**
- Purpose: Preserve filesystem interchange.
- Deliverables: Evidence serializers and round-trip tests.
- Dependencies: Evidence store.
- Completion: Clean import equality passes.

## 24. Parallel Work Opportunities

**Evidence store/schema**
- Owner: Persistence engineer
- Dependencies: Logical path model.
- Sync: Stem allocation rules.
- Risk: Path allocation differs from current file scans.
**Consumer integration**
- Owner: Roadmap/completion engineer
- Dependencies: Resolver-backed read API.
- Sync: Missing evidence behavior.
- Risk: One consumer still reads physical files.
**Search and fixtures**
- Owner: Test engineer
- Dependencies: Search API.
- Sync: Unblock planner expectations.
- Risk: Search semantics change accidentally.

## 25. Risks and Mitigations

**Evidence consumers still assume files**
- Class: integration
- Impact: Prompt/completion workflows fail after export deletion.
- Likelihood: medium
- Detection: Deleted-export consumer tests.
- Mitigation: Use resolver for all evidence reads/search.
- Fallback: Materialize temporary evidence export for legacy consumer until adapted.
**Large evidence bodies slow database operations**
- Class: performance
- Impact: Evidence search/export becomes slow.
- Likelihood: medium
- Detection: Large fixture smoke tests.
- Mitigation: Index metadata, stream exports, avoid unnecessary body reads.
- Fallback: Defer full-text indexing until measured need.
**Archive dependency remains unresolved**
- Class: operational
- Impact: Completion can drop DB-backed evidence before M10.
- Likelihood: high if completion archives run in migrated mode
- Detection: Archive tests.
- Mitigation: Expose archive association/export hooks and gate archive claims until M10.
- Fallback: Force completion archive materialization from export before archive until M10.

## 26. Observability and Diagnostics

- Evidence diagnostics include logical path, stem, sequence, body hash, and writer/consumer operation.
- Search diagnostics include query scope and result count without dumping bodies by default.
- Integrity checks report missing referenced evidence and hash mismatches.

## 27. Performance and Scalability Considerations

- Write/read by path should be indexed by logical path and stem/sequence.
- Search should be no worse than current directory scan for typical workspaces.
- Measure write latency, read-by-path latency, search time, and export throughput.
- Deferred optimization: full-text index or compressed body storage.

## 28. Security and Safety Considerations

- Validate stems and logical paths; reject traversal or invalid filename-derived identities.
- Avoid logging full evidence bodies in diagnostics.
- Use parameterized writes for evidence content.
- Preserve evidence immutability; do not overwrite existing evidence bodies on duplicate path.

## 29. Documentation Updates

- No documentation-only deliverable is required.
- Executable diagnostics should clarify when evidence content comes from SQLite versus regenerated export if that distinction is user-visible.

## 30. Exit Criteria

- Execution evidence writes and reads canonically use SQLite.
- All path-compatible consumers work without physical exported evidence files.
- Export/import preserves evidence identity and content.
- Missing/corrupt evidence failure paths are covered.
- No archive recovery or full synchronization capability is claimed.

## 31. Transition to Next Milestone

- Milestone 10 receives SQLite-backed histories and evidence that must remain recoverable with completed epics.
- Milestone 11 receives all requested migrated domains available for workspace-level synchronization.
- Remaining limitation: completed-epic archive behavior is still filesystem-shaped.

## Open Implementation Questions

- The roadmap scopes this milestone to `.agents/evidence/execution/*`; non-execution evidence directories remain filesystem-backed unless later clarified.

<!-- GENERATED:END -->

<!-- MANUAL:START -->
<!-- Add manually maintained notes here. This block is preserved by the generator. -->
<!-- MANUAL:END -->
