# Milestone 7 — Transition Journal Runs Canonically from SQLite with JSONL Interchange Deep Dive

<!-- GENERATED:START -->
Generated from `.agents/specs/roadmap.md` and `.agents/specs/audit.md`.

## 1. Milestone Summary

- Milestone Identifier: Milestone 7
- Milestone Name: Transition Journal Runs Canonically from SQLite with JSONL Interchange
- Short Description: Move transition history from JSONL file persistence to SQLite while preserving ordered JSONL import/export and legacy record compatibility.
- Implementation Role: Makes operational transition chronology transactional and path-compatible before workflow-level atomicity.
- Roadmap Position: Seventh milestone; depends on core/provenance SQLite state and logical identity.
- Primary Outcomes:
- Transition events append to SQLite in order.
- Started/completed/failed correlation and input snapshots are preserved.
- JSONL export/import remains deterministic and legacy-compatible.

## 2. Normative Basis

**Roadmap Authority**
- Roadmap authority: `.agents/specs/roadmap.md` `# Project Goal`, `# Guiding Principles`, and this milestone's `## Milestone` section.
- `.agents/specs/roadmap.md` `## Milestone 7 — Transition Journal Runs Canonically from SQLite with JSONL Interchange`.
**Architectural Authority**
- Architectural authority: `.agents/specs/audit.md` sections `2. Persistence Authority`, `4. Persistence Semantics`, `5. Filesystem-Coupled Behaviors`, `8. Cross-Boundary Dependencies`, `9. Compatibility and Migration Constraints`, `10. Round-Trip Import/Export Requirements`, `14. Persistence Domain Inventory`, `16. Persistence Dependency Graph`, `17. Synchronization Model`, `19. Persistence Platform Capabilities`, and `20. Roadmap-Relevant Findings`.
**Implementation Authority**
- Implementation authority: existing LoopRelay persistence code under `src/LoopRelay.Roadmap.Cli/Services`, `src/LoopRelay.Cli/Services/Execution`, `src/LoopRelay.Completion/Services/ArtifactStorage`, `src/LoopRelay.Projections/Services`, `src/LoopRelay.Core/Abstractions/Artifacts/IArtifactStore.cs`, and `src/LoopRelay.Infrastructure/Services/Artifacts`.
**Supporting Context**
- Supporting context: current tests under `tests/LoopRelay.Roadmap.Cli.Tests`, `tests/LoopRelay.Cli.Tests`, `tests/LoopRelay.Completion.Tests`, `tests/LoopRelay.Projections.Tests`, and `tests/LoopRelay.Infrastructure.Tests`.
- No undocumented authority is introduced here; unresolved audit mismatches remain open questions rather than implementation permission.

## 3. Objective

Implement a SQLite-backed append-only transition journal that preserves event ordering, correlation grouping, legacy JSONL import, deterministic JSONL export, input snapshots, output paths, and compatibility with paths that reference SQLite-backed artifacts.

## 4. Non-Goals

- Do not make entire workflows transactionally atomic; Milestone 12 owns workflow-level atomicity.
- Do not migrate loop histories, evidence, or archive behavior.
- Do not remove JSONL interchange or debugging export.
- Do not implement replay unless the roadmap later requires it.

## 5. Runtime / System State Before

- Core, provenance, and projection metadata domains are SQLite-backed.
- Transition journal is still `.agents/journal/transitions.jsonl`, appended by read/trim/rewrite behavior.
- Journal records reference retained and migrated paths, correlation IDs, input snapshots, output paths, and error details.

## 6. Runtime / System State After

- New transition events append to SQLite in durable order.
- JSONL export preserves operational chronology and logical event content.
- Legacy JSONL records without input snapshots import successfully.
- Journal remains interpretable when output paths reference SQLite-backed artifacts.
- Repeated/concurrent appends do not corrupt event order.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| SQLite append-only journal | Transition journal store | Append ordered started/completed/failed records. | Transition event data and optional input snapshot. | Canonical journal row with monotonic order. | SQLite transaction support. | New events append in order and correlation IDs are preserved. | Transition runner, state machine, diagnostics, verification. |
| JSONL import/export | Journal serializer | Read legacy JSONL and export deterministic JSONL. | SQLite journal rows or legacy JSONL file. | Canonical journal rows or JSONL file. | Milestone 2 serialization policies. | JSONL export imported into clean database reproduces equivalent journal state. | Workspace sync, Git review, debugging. |
| Legacy record compatibility | Journal importer | Accept records without input snapshots and preserve optional fields. | Existing JSONL records. | Canonical rows with nullable snapshot fields. | Current record model compatibility. | Legacy no-snapshot records import successfully. | Existing workspace migration. |
| Path-compatible event references | Journal store and resolver | Store output/input path references that may point to SQLite-backed artifacts. | Logical artifact paths and hashes. | Resolvable journal references. | Milestone 3 logical resolver. | Transition history remains interpretable with SQLite-backed output paths. | Verification, diagnostics, future recovery. |

## 8. Architectural Responsibilities

- Journal store owns append order and correlation grouping persistence.
- JSONL serializer owns interchange, not runtime authority.
- Transition runner owns when events are emitted; journal store owns durable append behavior.
- Logical resolver owns interpretation of artifact paths referenced by records.

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
- Purpose: Persist transition journal events in canonical database tables.
- Responsibilities: Implement the same domain operations as the file-backed store.
- Owned State: SQLite rows for transition journal events.
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
- Update `src/LoopRelay.Roadmap.Cli/Services/TransitionState/TransitionJournalStore.cs` behind the domain store contract.
- Tests extend `TransitionJournalTests` and transition coordination failure-path tests.
- JSONL export remains at `.agents/journal/transitions.jsonl`.

## 11. Public Contracts

- Transition journal append/read behavior remains event-shaped and correlation-ID compatible.
- JSONL interchange remains one record per line with deterministic event order.
- Legacy records without input snapshots are valid import inputs.
- Diagnostics identify append order, correlation ID, prompt/transition, and referenced paths.

## 12. Internal Contracts

- Append assigns a monotonic order independent of filesystem order.
- Started/completed/failed records sharing correlation ID remain queryable together.
- Concurrent appends serialize through SQLite transaction/constraint behavior.
- JSONL export is generated from canonical rows and is not read as authority during normal SQLite mode.

## 13. Data and State Model

**Transition journal rows**
- Owner: Transition journal store
- Lifecycle: Appended and never mutated in normal operation.
- Durability: SQLite canonical.
- Mutability: Append-only.
- Identity: Monotonic order plus correlation ID/event kind.
- Validation: Record shape, ordering, optional snapshot compatibility.
- Recovery: Export/import JSONL or database backup.
- Consumers: Diagnostics, verification, future recovery.
**JSONL export**
- Owner: Journal exporter
- Lifecycle: Regenerated from canonical rows.
- Durability: Filesystem interchange.
- Mutability: Overwrite export scope.
- Identity: Line order and correlation IDs.
- Validation: Import equivalence.
- Recovery: Regenerate from SQLite.
- Consumers: Git review, debugging, legacy import.

## 14. Lifecycle and State Transitions

- Event: Created by transition runner -> Appended with order -> Correlated by ID -> Exported to JSONL.
- Legacy import: JSONL line -> parse record -> assign/preserve event order -> insert canonical row -> validate sequence.
- Failure append: transaction fails -> no row committed -> caller records/propagates failure according to workflow behavior.

## 15. Execution Flow

- Startup validates journal table and prepares append order allocator.
- Normal transition writes started/completed/failed events to SQLite.
- Export flow reads rows by order and renders deterministic JSONL.
- Failure flow returns append error with correlation and transition context; no partial line rewrite can corrupt file.
- Recovery flow imports JSONL into clean database and compares row equivalence.

## 16. Dependency Closure

- Hard prerequisite: Milestone 4 SQLite store and transaction support.
- Hard prerequisite: Milestone 6 provenance/projection metadata if journal input snapshots reference them.
- Hard prerequisite: Milestone 3 logical path resolution.
- Inherited capability: Milestone 2 JSONL serialization.
- Future dependency: Milestone 12 workflow atomicity.
- Enables Milestones 10, 11, 12, and 13.

## 17. Failure Modes

**Append order conflict**
- Description: Concurrent appends contend for order allocation.
- Detection: SQLite constraint/transaction conflict.
- Behavior: Serialize, retry, or fail deterministically without corrupting order.
- Recovery: Retry append or inspect journal integrity.
- Diagnostics: Correlation ID, attempted event, transaction state.
- Tests: Concurrent append smoke test.
**Malformed legacy JSONL line**
- Description: Import encounters invalid JSON or incompatible record.
- Detection: JSONL parser.
- Behavior: Fail import with line number and no partial valid claim.
- Recovery: Repair or remove invalid line and re-import.
- Diagnostics: Line number, parse error, path.
- Tests: Malformed line fixture.
**Unresolved output path**
- Description: Journal references a path that no resolver provider can handle.
- Detection: Verification or diagnostic resolution.
- Behavior: Journal remains stored but verification reports unresolved path.
- Recovery: Restore/import referenced artifact or correct record through supported repair.
- Diagnostics: Correlation ID and output path.
- Tests: Unresolved path verification fixture.

## 18. Validation and Invariants

**Transition event order is preserved independent of filesystem append behavior.**
- Source Authority: Milestone 7 acceptance criteria.
- Enforcement Point: Monotonic order constraints and export tests.
- Failure Behavior: Order mismatch or duplicate order fails integrity.
- Test Strategy: Append/export/import order tests.
**Started/completed/failed records preserve correlation IDs.**
- Source Authority: Milestone 7 acceptance criteria.
- Enforcement Point: Record validation and tests.
- Failure Behavior: Correlation group incomplete or altered.
- Test Strategy: Started/completed/failed fixture tests.
**Legacy JSONL records without input snapshots remain importable.**
- Source Authority: Milestone 7 acceptance criteria.
- Enforcement Point: Importer compatibility.
- Failure Behavior: Legacy workspace import fails incorrectly.
- Test Strategy: No-snapshot legacy fixture.

## 19. Testing Strategy

- Unit tests for append order, correlation grouping, and row validation.
- JSONL import/export equivalence tests.
- Legacy JSONL import tests without input snapshots.
- Integration tests around transition runner started/completed/failed events.
- Failure-path tests for malformed lines, append conflicts, unresolved paths, and export/import order drift.
- Concurrency smoke tests for repeated appends.

## 20. Fixtures and Test Data

- Journal with started/completed pair and failed event.
- Legacy JSONL without input snapshots.
- Journal records referencing SQLite-backed core/provenance outputs and retained files.
- Malformed JSONL line and incompatible record fixtures.
- Large journal fixture for export ordering.

## 21. Acceptance Demonstration

- Import an existing `.agents/journal/transitions.jsonl` containing legacy and current records.
- Append a started/completed pair through SQLite journal store.
- Export JSONL and verify line order and correlation IDs.
- Import exported JSONL into a clean database and compare journal equivalence.
- Run verification of output paths through logical resolver.

## 22. Certification Evidence

- SQLite journal append test output.
- JSONL export/import equivalence report.
- Legacy no-snapshot import test output.
- Concurrent append smoke test output.

## 23. Implementation Plan

**Implement journal schema/store**
- Purpose: Persist ordered transition events in SQLite.
- Deliverables: Append/read/query store.
- Dependencies: SQLite substrate.
- Completion: Append/order tests pass.
**Implement JSONL importer/exporter**
- Purpose: Preserve interchange and debugging surface.
- Deliverables: Legacy-compatible import and deterministic export.
- Dependencies: Record model and serializers.
- Completion: JSONL round-trip tests pass.
**Integrate transition runner**
- Purpose: Route journal writes to SQLite canonical store.
- Deliverables: Composition and caller updates.
- Dependencies: Journal store.
- Completion: Transition coordination tests pass.
**Add path resolution checks**
- Purpose: Keep journal references interpretable.
- Deliverables: Verification hooks/tests.
- Dependencies: Logical resolver.
- Completion: Referenced migrated paths resolve.

## 24. Parallel Work Opportunities

**Journal store and schema**
- Owner: Transition engineer
- Dependencies: SQLite transaction API.
- Sync: Order allocation strategy.
- Risk: Concurrency ordering semantics differ.
**JSONL compatibility**
- Owner: Serialization engineer
- Dependencies: Record model.
- Sync: Legacy optional fields.
- Risk: Export bytes drift unexpectedly.
**Transition integration tests**
- Owner: Test engineer
- Dependencies: Store API stable.
- Sync: Correlation fixtures.
- Risk: Tests rely on physical JSONL file as authority.

## 25. Risks and Mitigations

**Append ordering changes under concurrency**
- Class: data
- Impact: Operational chronology becomes unreliable.
- Likelihood: medium
- Detection: Concurrent append tests.
- Mitigation: Use SQLite transaction and unique monotonic order.
- Fallback: Serialize journal writes through a workspace lock.
**JSONL export loses debugging compatibility**
- Class: integration
- Impact: Existing tooling cannot inspect transition history.
- Likelihood: medium
- Detection: Golden JSONL fixture tests.
- Mitigation: Preserve line record content and optional legacy fields.
- Fallback: Provide compatibility export mode.
**Journal write failure masks workflow failure**
- Class: operational
- Impact: Diagnostics become incomplete.
- Likelihood: medium
- Detection: Failure-path transition tests.
- Mitigation: Propagate journal errors with correlation context.
- Fallback: Emit explicit failed-journal diagnostic without claiming transition success.

## 26. Observability and Diagnostics

- Journal diagnostics include append order, correlation ID, event kind, prompt/transition name, and referenced output paths.
- Export diagnostics include row count, first/last order, and JSONL path.
- Integrity checks report ordering gaps/duplicates and unresolved path references.

## 27. Performance and Scalability Considerations

- Append should be O(1) plus transaction overhead.
- Export is O(number of journal records).
- Likely bottleneck is large JSONL export for long-running workspaces.
- Deferred optimization: paged journal export and indexed correlation queries.

## 28. Security and Safety Considerations

- Use parameterized writes for record payloads.
- Do not allow imported JSONL paths to escape logical artifact resolver scope.
- Avoid logging full prompt/evidence bodies in journal diagnostics.
- Preserve audit integrity by preventing mutation of existing journal rows in normal operations.

## 29. Documentation Updates

- No documentation-only deliverable is required.
- Executable diagnostic output should continue to reference JSONL export paths where users already expect them.

## 30. Exit Criteria

- Transition journal writes canonically to SQLite.
- JSONL export/import preserves order and logical content.
- Legacy no-snapshot records import.
- Concurrent/repeated appends do not corrupt event order.
- No workflow-level transaction recovery is claimed.

## 31. Transition to Next Milestone

- Milestone 12 receives an append-only SQLite journal suitable for transaction-aware workflow recording.
- Milestone 13 receives journal verification and export/import equivalence behavior.
- Remaining limitation: histories and evidence remain file-backed.

## Open Implementation Questions

- The required JSONL byte-compatibility level for external debugging tools is not fully specified; this milestone preserves logical event content and deterministic ordering at minimum.

<!-- GENERATED:END -->

<!-- MANUAL:START -->
<!-- Add manually maintained notes here. This block is preserved by the generator. -->
<!-- MANUAL:END -->
