# Milestone 5 — Core Roadmap State Runs Canonically from SQLite Deep Dive

<!-- GENERATED:START -->
Generated from `.agents/specs/roadmap.md` and `.agents/specs/audit.md`.

## 1. Milestone Summary

- Milestone Identifier: Milestone 5
- Milestone Name: Core Roadmap State Runs Canonically from SQLite
- Short Description: Move decision ledger, roadmap state, artifact lifecycle, and split lineage to SQLite as canonical state with deterministic filesystem export/import.
- Implementation Role: First canonical SQLite domain migration, focused on structured identity-driven machine state.
- Roadmap Position: Fifth milestone; depends on SQLite importable state and logical identity.
- Primary Outcomes:
- Core roadmap workflows use SQLite as authoritative store for initial structured domains.
- Filesystem equivalents can be deleted and regenerated from SQLite without logical loss.
- Clean database import from regenerated exports produces equivalent state.

## 2. Normative Basis

**Roadmap Authority**
- Roadmap authority: `.agents/specs/roadmap.md` `# Project Goal`, `# Guiding Principles`, and this milestone's `## Milestone` section.
- `.agents/specs/roadmap.md` `## Milestone 5 — Core Roadmap State Runs Canonically from SQLite`.
**Architectural Authority**
- Architectural authority: `.agents/specs/audit.md` sections `2. Persistence Authority`, `4. Persistence Semantics`, `5. Filesystem-Coupled Behaviors`, `8. Cross-Boundary Dependencies`, `9. Compatibility and Migration Constraints`, `10. Round-Trip Import/Export Requirements`, `14. Persistence Domain Inventory`, `16. Persistence Dependency Graph`, `17. Synchronization Model`, `19. Persistence Platform Capabilities`, and `20. Roadmap-Relevant Findings`.
**Implementation Authority**
- Implementation authority: existing LoopRelay persistence code under `src/LoopRelay.Roadmap.Cli/Services`, `src/LoopRelay.Cli/Services/Execution`, `src/LoopRelay.Completion/Services/ArtifactStorage`, `src/LoopRelay.Projections/Services`, `src/LoopRelay.Core/Abstractions/Artifacts/IArtifactStore.cs`, and `src/LoopRelay.Infrastructure/Services/Artifacts`.
**Supporting Context**
- Supporting context: current tests under `tests/LoopRelay.Roadmap.Cli.Tests`, `tests/LoopRelay.Cli.Tests`, `tests/LoopRelay.Completion.Tests`, `tests/LoopRelay.Projections.Tests`, and `tests/LoopRelay.Infrastructure.Tests`.
- No undocumented authority is introduced here; unresolved audit mismatches remain open questions rather than implementation permission.

## 3. Objective

Switch the decision ledger, roadmap state, artifact lifecycle, and split lineage domains to SQLite-backed canonical stores while preserving existing public workflow behavior and deterministic filesystem export/import.

## 4. Non-Goals

- Do not migrate provenance, projection metadata, journal, loop histories, evidence, archives, or workspace sync.
- Do not migrate retained live markdown files or human-facing roadmap/spec artifacts.
- Do not make multi-domain workflow updates fully transactional beyond necessary domain-store write correctness.
- Do not remove filesystem export support or legacy import compatibility.

## 5. Runtime / System State Before

- Milestone 4 database can import migrated state but workflows still use file-backed persistence.
- Core structured domains exist in filesystem JSON and legacy markdown import paths.
- Freshness and path references can resolve migrated logical identities.

## 6. Runtime / System State After

- New decision ledger entries write to SQLite and export deterministically.
- Roadmap state loads/saves from SQLite.
- Lifecycle entries upsert by case-insensitive path identity in SQLite.
- Split family creation and child-path lookup use SQLite after importing existing family files.
- Filesystem JSON exports are projections/interchange, not canonical runtime authority.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| SQLite decision ledger | Decision domain store | Append decisions and allocate `DNNNN` from imported/current rows. | Decision entry data and existing ledger rows. | Canonical ledger row and deterministic export. | Milestone 4 database and Milestone 2 export rules. | `DNNNN` allocation preserves imported IDs and advances correctly. | Decision recorder, transition persistence, execution preparation freshness. |
| SQLite roadmap state | Roadmap state store | Load/save current workflow state snapshot. | Transition state, active paths, blockers, retired epics, counts. | Canonical state row/document and export. | Logical path resolver and core domain rows. | State fields round-trip without logical loss. | State machine, startup/resume planners, transition coordination. |
| SQLite lifecycle | Artifact lifecycle store | Upsert entries keyed by case-insensitive artifact path. | Path, state, timestamp, notes. | Canonical lifecycle rows and export. | Lifecycle domain contract. | Duplicate path identity handling matches current behavior. | Artifact validation, promotion, split/bootstrap flows. |
| SQLite split lineage | Split family store | Create family records and lookup by child path. | Family ID, parent/child paths, dependency order, selected child. | Canonical split rows and per-family export. | Imported split family snapshots. | Lookup by child path works after importing existing `split-family-*.json` files. | Split transition, roadmap state summaries. |

## 8. Architectural Responsibilities

- Each core domain store owns its canonical SQLite rows and deterministic export projection.
- Filesystem export writers own regenerated JSON files but not runtime authority.
- Roadmap workflows own business transitions and consume domain stores, not SQLite directly.
- Validation authority for path identity, duplicate IDs, and schema equivalence remains inside domain stores.

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
- Purpose: Persist decision ledger, roadmap state, lifecycle, and split lineage in canonical database tables.
- Responsibilities: Implement the same domain operations as the file-backed store.
- Owned State: SQLite rows for decision ledger, roadmap state, lifecycle, and split lineage.
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
- Update current stores in `Services/Decisions`, `Services/State`, `Services/ArtifactManagement`, and `Services/Splits` to use storage-backed implementations through shared domain contracts.
- Add SQLite-backed tests alongside existing store tests and reuse current fixture names where possible.
- Filesystem export artifacts remain under existing `.agents` paths: `decision-ledger.json`, `state.json`, `artifacts/lifecycle.json`, and `splits/split-family-*.json`.

## 11. Public Contracts

- Existing workflow behavior and `.agents` export file paths remain unchanged.
- Core domain stores now treat SQLite as canonical when database mode is active.
- Import precedence between legacy markdown, exported JSON, and canonical database state is explicit: canonical database wins after migration; legacy/export files are import sources only when importing or regenerating.

## 12. Internal Contracts

- Decision append and ID allocation happen inside a database transaction.
- Lifecycle path uniqueness is enforced case-insensitively by domain logic and database constraints.
- Roadmap state derived counts must be computed from canonical domain stores or explicitly stored as snapshot fields according to current model semantics.
- Split lookup by child path must not depend on filesystem file scans once SQLite is canonical.

## 13. Data and State Model

**Decision ledger rows**
- Owner: Decision domain store
- Lifecycle: Imported, appended, exported.
- Durability: SQLite canonical.
- Mutability: Append-only.
- Identity: `DNNNN` decision ID.
- Validation: Unique valid IDs and deterministic ordering.
- Recovery: Export/import round trip or database backup.
- Consumers: State summaries, provenance, journal, verification.
**Roadmap state snapshot**
- Owner: Roadmap state store
- Lifecycle: Loaded on startup, overwritten on transition persistence.
- Durability: SQLite canonical.
- Mutability: Mutable singleton snapshot.
- Identity: Workspace current state.
- Validation: Schema and path reference validation.
- Recovery: Regenerate from export/import where supported.
- Consumers: Roadmap state machine and planners.
**Lifecycle rows**
- Owner: Artifact lifecycle store
- Lifecycle: Upserted by artifact operations.
- Durability: SQLite canonical.
- Mutability: Mutable by path upsert.
- Identity: Case-insensitive artifact path.
- Validation: No duplicate path identities.
- Recovery: Export/import lifecycle JSON.
- Consumers: Invariant validation and artifact snapshots.
**Split family rows**
- Owner: Split family store
- Lifecycle: Created on split and read by child lookup.
- Durability: SQLite canonical.
- Mutability: Effectively immutable after creation unless current behavior later changes.
- Identity: Family ID and child path relation.
- Validation: Family uniqueness and child path preservation.
- Recovery: Export/import family JSON files.
- Consumers: Split transitions and state summaries.

## 14. Lifecycle and State Transitions

- Decision: Imported -> Active ledger -> Append new entry -> Export projection.
- Roadmap state: Missing/empty -> Loaded/imported -> Saved current snapshot -> Export projection.
- Lifecycle: Path absent -> Upserted active entry -> Re-upsert replaces same path identity -> Export sorted entries.
- Split family: Family created -> Child lookup available -> Export per-family JSON.

## 15. Execution Flow

- Startup validates database, loads core domain stores, and does not trust stale exported JSON as canonical.
- Normal operation writes core domain changes to SQLite, then exports deterministic filesystem projections when the workflow requires Git/review visibility.
- Failure flow rolls back domain transaction and reports domain/identity diagnostics.
- Recovery flow imports regenerated exports into a clean database and compares equivalence.
- Shutdown has no special action beyond completed writes.

## 16. Dependency Closure

- Hard prerequisite: Milestone 4 SQLite workspace store and imported database state.
- Hard prerequisite: Milestone 3 logical identity/freshness.
- Inherited capability: Milestone 2 export/import serializers.
- Explicitly unavailable dependencies: provenance/journal/history/evidence SQLite canonical stores.
- Enables Milestones 6, 7, 11, 12, and 13.

## 17. Failure Modes

**ID allocation race**
- Description: Concurrent decision appends allocate same `DNNNN`.
- Detection: Unique constraint or transaction conflict.
- Behavior: One append succeeds; the other retries or fails deterministically.
- Recovery: Retry append after reloading next ID.
- Diagnostics: Decision ID and transaction status.
- Tests: Concurrent append smoke test.
**Stale export mistaken for authority**
- Description: Filesystem JSON differs from canonical database.
- Detection: Startup/import precedence check.
- Behavior: Database state wins; export is regenerated or conflict reported when sync mode exists.
- Recovery: Regenerate export from SQLite.
- Diagnostics: Domain and stale file path.
- Tests: Delete/modify export then regenerate.
**Invalid imported split lookup**
- Description: Child path lookup misses imported family data.
- Detection: Lookup test after import.
- Behavior: Fail lookup/validation, do not scan stale files as canonical.
- Recovery: Re-import valid split families.
- Diagnostics: Child path and family ID.
- Tests: Imported split family lookup fixture.

## 18. Validation and Invariants

**Core machine state is canonical in SQLite after this milestone.**
- Source Authority: Milestone 5 objective.
- Enforcement Point: Composition and store tests.
- Failure Behavior: Workflow writes only filesystem JSON or trusts stale exports.
- Test Strategy: Tests delete exports and verify state loads from SQLite.
**Exported filesystem files can be regenerated without logical loss.**
- Source Authority: Milestone 5 acceptance criteria.
- Enforcement Point: Delete/regenerate/import equivalence tests.
- Failure Behavior: Logical comparison mismatch.
- Test Strategy: Round-trip clean database import from regenerated exports.
**Lifecycle entries remain keyed by case-insensitive path identity.**
- Source Authority: Milestone 5 acceptance criteria and audit.
- Enforcement Point: Domain constraints and tests.
- Failure Behavior: Duplicate case variant accepted.
- Test Strategy: Case-variant upsert tests.

## 19. Testing Strategy

- Unit tests for SQLite decision ledger append/next-ID allocation.
- Unit tests for SQLite roadmap state save/load and export.
- Unit tests for lifecycle path upsert and case-insensitive uniqueness.
- Unit tests for split family creation and child lookup.
- Integration tests running core roadmap transitions against SQLite stores.
- Export/import equivalence tests into a clean database.
- Failure-path tests for stale exports, duplicate IDs, invalid paths, and transaction conflicts.

## 20. Fixtures and Test Data

- Imported core-state database with decision IDs, lifecycle paths, state blockers, retired epics, and split families.
- Legacy markdown and JSON import fixtures for core structured domains.
- Stale/deleted export fixtures.
- Case-variant lifecycle path fixtures.
- Split family fixture with multiple children and selected child.

## 21. Acceptance Demonstration

- Import existing `.agents` core structured files into SQLite.
- Run a roadmap transition that appends a decision and saves state.
- Delete exported `decision-ledger.json`, `state.json`, `artifacts/lifecycle.json`, and `splits/split-family-*.json`.
- Regenerate exports from SQLite.
- Import regenerated exports into a clean database and compare core domain equality.

## 22. Certification Evidence

- Passing SQLite core domain store tests.
- Workflow regression output in SQLite core-state mode.
- Delete/regenerate export transcript.
- Clean database import equivalence report.
- Concurrency/unique allocation smoke test output.

## 23. Implementation Plan

**Implement SQLite core stores**
- Purpose: Make core domains database-backed behind existing contracts.
- Deliverables: Decision, state, lifecycle, and split stores.
- Dependencies: Milestone 4 schema and transactions.
- Completion: Store-level tests pass.
**Integrate workflow composition**
- Purpose: Route core domain operations to SQLite in database mode.
- Deliverables: Composition/configuration changes.
- Dependencies: SQLite stores.
- Completion: Core workflow tests use database stores.
**Add deterministic exports**
- Purpose: Preserve filesystem interchange and Git review.
- Deliverables: Export projection for four core domains.
- Dependencies: Milestone 2 serializers.
- Completion: Exports regenerate after deletion.
**Validate clean re-import**
- Purpose: Prove round-trip losslessness.
- Deliverables: Clean database import equality test.
- Dependencies: Exports.
- Completion: Equivalent state after import.

## 24. Parallel Work Opportunities

**Decision/state stores**
- Owner: Roadmap transition engineer
- Dependencies: Core schema.
- Sync: Transaction and summary fields.
- Risk: Derived state counts gain a second authority.
**Lifecycle/split stores**
- Owner: Artifact management engineer
- Dependencies: Path identity constraints.
- Sync: Shared path normalization.
- Risk: Case sensitivity differs from current behavior.
**Export/import tests**
- Owner: Test engineer
- Dependencies: Store APIs stable.
- Sync: Canonical serializer expectations.
- Risk: Tests compare bytes where only logical equality is required.

## 25. Risks and Mitigations

**State snapshot duplicates derived authority**
- Class: architectural
- Impact: Counts or summaries drift from canonical stores.
- Likelihood: medium
- Detection: Integrity tests comparing state counts to stores.
- Mitigation: Treat derived fields as snapshot outputs and recompute during save.
- Fallback: Mark mismatched state invalid and rebuild snapshot.
**Legacy import precedence confusion**
- Class: data
- Impact: Old markdown overwrites canonical database.
- Likelihood: medium
- Detection: Precedence tests.
- Mitigation: Database wins after migration; legacy sources only used during explicit import.
- Fallback: Require explicit import mode for legacy files.
**SQLite-only mode breaks older tools**
- Class: integration
- Impact: Filesystem consumers see stale/missing JSON.
- Likelihood: medium
- Detection: Export deletion/regeneration and stale tests.
- Mitigation: Regenerate exports for compatibility flows.
- Fallback: Keep dual-write exports until Milestone 11 sync policy.

## 26. Observability and Diagnostics

- Core store diagnostics include domain, operation, identity, and database transaction status.
- Export diagnostics include regenerated file count and domain snapshot hash.
- Integrity checks report core-domain row counts and identity conflicts.

## 27. Performance and Scalability Considerations

- Baseline core domain operations should be at least comparable to JSON file load/save for typical sizes.
- Likely bottlenecks are decision ledger append sorting and state export serialization.
- Measure append/save/export latency and row counts.
- Deferred optimization: indexes for decision ID, lifecycle path, and child path lookup beyond required constraints.

## 28. Security and Safety Considerations

- Use parameterized SQL and domain validation before writes.
- Protect against path traversal in lifecycle/split paths.
- Do not let exported JSON overwrite retained human files.
- Keep database transactions scoped to expected domain writes.

## 29. Documentation Updates

- No documentation-only deliverable is required.
- Executable CLI help may need to distinguish canonical database state from regenerated exports if a user-visible mode switch exists.

## 30. Exit Criteria

- Decision ledger, roadmap state, lifecycle, and split lineage run canonically from SQLite.
- Filesystem exports regenerate deterministically and can seed a clean equivalent database.
- All existing behavior for IDs, paths, lifecycle keys, and split lookup is preserved.
- All tests and acceptance demonstration pass.
- No provenance, journal, history, evidence, archive, sync, or transaction-recovery capability is falsely claimed.

## 31. Transition to Next Milestone

- Milestone 6 receives SQLite-backed core state, decision identity, lifecycle, split counts, and logical path resolution.
- Milestone 7 receives database-backed core transition context.
- Remaining limitation: provenance, projection metadata, journal, histories, and evidence are still file-backed.

## Open Implementation Questions

- The exact export timing policy for Git/submodule visibility remains a synchronization concern for Milestone 11; this milestone must provide export capability without over-defining publication policy.

<!-- GENERATED:END -->

<!-- MANUAL:START -->
<!-- Add manually maintained notes here. This block is preserved by the generator. -->
<!-- MANUAL:END -->
