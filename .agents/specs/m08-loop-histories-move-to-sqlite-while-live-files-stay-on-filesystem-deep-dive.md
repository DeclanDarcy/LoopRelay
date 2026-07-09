# Milestone 8 — Loop Histories Move to SQLite While Live Files Stay on Filesystem Deep Dive

<!-- GENERATED:START -->
Generated from `.agents/specs/roadmap.md` and `.agents/specs/audit.md`.

## 1. Milestone Summary

- Milestone Identifier: Milestone 8
- Milestone Name: Loop Histories Move to SQLite While Live Files Stay on Filesystem
- Short Description: Move historical decisions, handoffs, and operational deltas to SQLite while retaining live workflow files and live-first behavior.
- Implementation Role: Migrates content-heavy loop histories without changing live file handoff semantics.
- Roadmap Position: Eighth milestone; follows path identity, SQLite substrate, and journal migration.
- Primary Outcomes:
- Historical loop records are transactionally stored in SQLite.
- Live decisions, handoff, and operational delta remain filesystem workflow files.
- Latest decision/handoff reads continue to prefer live files before historical records.

## 2. Normative Basis

**Roadmap Authority**
- Roadmap authority: `.agents/specs/roadmap.md` `# Project Goal`, `# Guiding Principles`, and this milestone's `## Milestone` section.
- `.agents/specs/roadmap.md` `## Milestone 8 — Loop Histories Move to SQLite While Live Files Stay on Filesystem`.
**Architectural Authority**
- Architectural authority: `.agents/specs/audit.md` sections `2. Persistence Authority`, `4. Persistence Semantics`, `5. Filesystem-Coupled Behaviors`, `8. Cross-Boundary Dependencies`, `9. Compatibility and Migration Constraints`, `10. Round-Trip Import/Export Requirements`, `14. Persistence Domain Inventory`, `16. Persistence Dependency Graph`, `17. Synchronization Model`, `19. Persistence Platform Capabilities`, and `20. Roadmap-Relevant Findings`.
**Implementation Authority**
- Implementation authority: existing LoopRelay persistence code under `src/LoopRelay.Roadmap.Cli/Services`, `src/LoopRelay.Cli/Services/Execution`, `src/LoopRelay.Completion/Services/ArtifactStorage`, `src/LoopRelay.Projections/Services`, `src/LoopRelay.Core/Abstractions/Artifacts/IArtifactStore.cs`, and `src/LoopRelay.Infrastructure/Services/Artifacts`.
**Supporting Context**
- Supporting context: current tests under `tests/LoopRelay.Roadmap.Cli.Tests`, `tests/LoopRelay.Cli.Tests`, `tests/LoopRelay.Completion.Tests`, `tests/LoopRelay.Projections.Tests`, and `tests/LoopRelay.Infrastructure.Tests`.
- No undocumented authority is introduced here; unresolved audit mismatches remain open questions rather than implementation permission.

## 3. Objective

Implement SQLite-backed historical loop stores for numbered decisions, handoffs, and operational deltas while preserving retained live files, live-to-history rotation, latest fallback, sequence allocation, and deterministic markdown export/import.

## 4. Non-Goals

- Do not migrate live `.agents/decisions/decisions.md`, `.agents/handoffs/handoff.md`, or `.agents/operational_delta.md`.
- Do not migrate execution evidence or completion archives yet.
- Do not alter prompt text/body content during history export/import.
- Do not make full workflow transitions atomic beyond the history write/rotation operations.

## 5. Runtime / System State Before

- Loop histories are numbered markdown files under decisions, handoffs, and deltas directories.
- Live files gate execution/decision behavior and are retained filesystem artifacts.
- Sequence allocation scans filenames.
- Completion archive still expects historical files to be recoverable.

## 6. Runtime / System State After

- Historical decisions, handoffs, and deltas are canonical SQLite records with preserved logical paths.
- Live files remain filesystem-backed and continue to gate workflows.
- Rotation writes history records to SQLite and removes/retains live files exactly as current behavior requires.
- Export regenerates numbered markdown history files with preserved `NNNN` values.
- Import preserves sequence allocation and latest fallback behavior.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| SQLite historical decision records | Loop history store | Persist numbered decision history while live decision file stays on disk. | Decision markdown body and current sequence state. | Canonical decision history row and export path. | Logical path resolver and SQLite sequence preservation. | New decision proposal writes live file and correct next historical record. | Loop runner, execution step, completion archive. |
| SQLite historical handoff records | Loop history store | Rotate live handoff into history and read latest fallback. | Live handoff body and existing history rows. | Canonical handoff history row and export path. | `LoopArtifacts` live handoff behavior. | Next decision loop rotates live handoff into SQLite history. | Decision session, loop runner, completion archive. |
| SQLite historical operational delta records | Loop history store | Rotate live operational delta after context evolution. | Live delta body and current sequence state. | Canonical delta history row and export path. | Operational context evolution flow. | Transfer writes live delta, evolves context, and rotates historical delta state into SQLite. | Completion archive, diagnostics. |
| Live-first latest reads | Loop artifact facade | Read retained live file before historical SQLite fallback. | Live file presence and history rows. | Latest decisions/handoff content. | Retained live file paths. | Latest reads behave exactly as before. | Execution step, decision session. |

## 8. Architectural Responsibilities

- Live file lifecycle remains owned by filesystem loop artifacts.
- Historical sequence allocation and storage are owned by SQLite loop history store.
- Loop artifact facade owns live-first composition between retained files and history store.
- Markdown body preservation is the serializer's responsibility; domain store must not canonicalize prompt bodies unexpectedly.

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
- Purpose: Persist historical loop decisions, handoffs, and operational deltas in canonical database tables.
- Responsibilities: Implement the same domain operations as the file-backed store.
- Owned State: SQLite rows for historical loop decisions, handoffs, and operational deltas.
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
- Update `src/LoopRelay.Cli/Services/Execution/LoopArtifacts.cs` or its domain adapters to delegate history behavior.
- Add history stores in a shared loop persistence area accessible to CLI and completion archive code.
- Tests extend `LoopArtifactsTests`, decision session tests, execution tests, and future archive compatibility tests.

## 11. Public Contracts

- Live paths remain `.agents/decisions/decisions.md`, `.agents/handoffs/handoff.md`, and `.agents/operational_delta.md`.
- Export paths remain `.agents/decisions/decisions.NNNN.md`, `.agents/handoffs/handoff.NNNN.md`, and `.agents/deltas/operational_delta.NNNN.md`.
- Latest read behavior remains live-first, then highest numbered historical record.
- Sequence allocation preserves imported `NNNN` values.

## 12. Internal Contracts

- History writes allocate next sequence inside SQLite transaction/constraint behavior.
- Rotation from live file to SQLite history must not delete the live file until history write succeeds.
- Latest fallback queries order by numeric sequence, not lexical path alone.
- Export/import preserves exact markdown body bytes or declared canonical markdown body policy.

## 13. Data and State Model

**Decision history rows**
- Owner: Loop history store
- Lifecycle: Created during proposal/persist decision flow.
- Durability: SQLite canonical.
- Mutability: Append-only.
- Identity: `decisions.NNNN.md` logical path and sequence.
- Validation: Unique sequence/path and nonempty body where current behavior requires.
- Recovery: Export/import markdown histories.
- Consumers: Execution, latest decision fallback, completion archive.
**Handoff history rows**
- Owner: Loop history store
- Lifecycle: Created when live handoff rotates before next decision.
- Durability: SQLite canonical.
- Mutability: Append-only.
- Identity: `handoff.NNNN.md` logical path and sequence.
- Validation: Unique sequence/path.
- Recovery: Export/import markdown histories.
- Consumers: Decision session latest handoff, archive.
**Operational delta history rows**
- Owner: Loop history store
- Lifecycle: Created after live delta is consumed/evolved.
- Durability: SQLite canonical.
- Mutability: Append-only.
- Identity: `operational_delta.NNNN.md` logical path and sequence.
- Validation: Unique sequence/path.
- Recovery: Export/import markdown histories.
- Consumers: Archive and diagnostics.
**Retained live loop files**
- Owner: Filesystem loop artifacts
- Lifecycle: Written/consumed/deleted according to current loop flow.
- Durability: Filesystem canonical.
- Mutability: Mutable working files.
- Identity: Fixed live repo-relative paths.
- Validation: Existence/required content checks.
- Recovery: Existing live-file crash behavior.
- Consumers: Loop runner and decision/execution steps.

## 14. Lifecycle and State Transitions

- Decision: Proposal body -> write live decision file and SQLite history row -> execution consumes live file -> live decision retired.
- Handoff: Execution writes live handoff -> next decision loop rotates to SQLite `handoff.NNNN.md` history -> live handoff deleted/cleared as current behavior requires.
- Delta: Transfer writes live delta -> operational context evolves -> live delta rotates to SQLite `operational_delta.NNNN.md` -> live delta deleted.
- Failure: if history write fails, retained live file remains available for recovery; no false history claim is made.

## 15. Execution Flow

- Startup loads history store and checks live file presence as current loop logic does.
- Normal decision flow persists live decision and history record with correct sequence.
- Normal execution flow writes live handoff; next loop rotates it before building decision prompt.
- Failure flow preserves current crash-recovery semantics by not deleting live files before successful history persistence.
- Export flow regenerates numbered markdown history files from SQLite.

## 16. Dependency Closure

- Hard prerequisite: Milestone 3 logical path identity.
- Hard prerequisite: Milestone 4 SQLite substrate and import state.
- Inherited capability: Milestone 2 markdown history export/import.
- Supporting infrastructure: current `LoopArtifacts` behavior.
- Future dependency: Milestone 10 archive compatibility and Milestone 12 transaction recovery.
- Enables Milestones 9, 10, 11, 12, and 13.

## 17. Failure Modes

**History write fails after live file creation**
- Description: Live file exists but SQLite history insert fails.
- Detection: Insert/constraint exception.
- Behavior: Keep live file and report failure; do not claim historical record.
- Recovery: Retry history persist/rotation after resolving database issue.
- Diagnostics: Live path, intended history path, sequence.
- Tests: Injected SQLite failure during rotation.
**Duplicate sequence after import**
- Description: Imported histories produce conflicting next sequence.
- Detection: Import validation and unique constraint.
- Behavior: Import fails or write retries with correct next sequence.
- Recovery: Repair duplicate export or re-import valid state.
- Diagnostics: History kind and duplicate `NNNN`.
- Tests: Duplicate sequence fixture.
**Latest read ignores live file**
- Description: SQLite fallback returned despite live file existing.
- Detection: Latest read tests.
- Behavior: Invalid behavior; tests fail.
- Recovery: Fix facade ordering.
- Diagnostics: Live path and selected history path.
- Tests: Live-first fixture.

## 18. Validation and Invariants

**Retained live files stay filesystem-backed and live-first.**
- Source Authority: Milestone 8 objective and acceptance criteria.
- Enforcement Point: Loop artifact facade tests.
- Failure Behavior: Live file ignored or migrated.
- Test Strategy: Live-present/latest-read tests.
**Historical `NNNN` sequence identity is preserved across import/export.**
- Source Authority: Roadmap guiding principles and Milestone 8 acceptance criteria.
- Enforcement Point: History store constraints and round-trip tests.
- Failure Behavior: Sequence mutation or duplicate.
- Test Strategy: Imported sequence and next-allocation tests.
**Markdown history body content remains logically preserved.**
- Source Authority: Milestone 2 and 8 acceptance criteria.
- Enforcement Point: Body comparison tests.
- Failure Behavior: Export/import content mismatch.
- Test Strategy: Golden markdown body fixtures.

## 19. Testing Strategy

- Unit tests for SQLite decision/handoff/delta history stores and sequence allocation.
- Loop integration tests for live decision write/retire and live handoff rotation.
- Decision session tests for latest handoff fallback.
- Operational delta transfer/context evolution/rotation tests.
- Export/import round-trip tests for histories.
- Failure-path tests for history insert failure, live file preservation, duplicate sequence, and missing live files.

## 20. Fixtures and Test Data

- Existing history files ending at `0003` for each history kind.
- Live decision/handoff/delta files with and without corresponding histories.
- Markdown bodies with formatting that must be preserved.
- Duplicate sequence and invalid filename import fixtures.
- Crash-like fixture with live handoff present and no new history row.

## 21. Acceptance Demonstration

- Import existing numbered decisions, handoffs, and deltas into SQLite.
- Run a decision proposal and verify live decisions file plus new SQLite decision history row.
- Run execution to produce live handoff, then start next decision loop and verify handoff rotation into SQLite.
- Run delta transfer and verify live delta is consumed, context evolves, and delta history row is created.
- Export histories and import into clean database, then verify latest fallback and next sequence.

## 22. Certification Evidence

- Loop history SQLite store test output.
- Live-first latest read regression output.
- Rotation failure-path test output.
- History export/import equivalence report.

## 23. Implementation Plan

**Implement history stores**
- Purpose: Persist numbered markdown histories in SQLite.
- Deliverables: Decision, handoff, and delta history store operations.
- Dependencies: SQLite schema and logical paths.
- Completion: Store tests pass.
**Adapt live/history facade**
- Purpose: Preserve live file behavior while moving histories.
- Deliverables: Updated `LoopArtifacts` or equivalent adapter.
- Dependencies: History stores.
- Completion: Live-first and rotation tests pass.
**Implement markdown export/import**
- Purpose: Regenerate numbered history files.
- Deliverables: History serializers/exporters.
- Dependencies: Milestone 2 policies.
- Completion: Round-trip tests pass.
**Integrate workflow tests**
- Purpose: Prove decision/execution/delta flows still work.
- Deliverables: Updated integration tests.
- Dependencies: Facade and stores.
- Completion: Loop workflow tests pass.

## 24. Parallel Work Opportunities

**History store/schema**
- Owner: Persistence engineer
- Dependencies: SQLite substrate.
- Sync: Shared sequence allocator.
- Risk: Sequence behavior diverges by history kind.
**Loop facade integration**
- Owner: CLI loop engineer
- Dependencies: Store API.
- Sync: Live file lifecycle.
- Risk: Deleting live files too early.
**Markdown round-trip fixtures**
- Owner: Test engineer
- Dependencies: Export format.
- Sync: Body preservation policy.
- Risk: Over-canonicalizing markdown.

## 25. Risks and Mitigations

**Live/history split creates inconsistent recovery**
- Class: operational
- Impact: Crash leaves live file and missing/duplicate history.
- Likelihood: medium
- Detection: Injected failure tests.
- Mitigation: Write history before deleting live file; detect live/history mismatch on startup.
- Fallback: Retry rotation from retained live file.
**Completion archive loses histories**
- Class: integration
- Impact: Completed epics omit migrated histories.
- Likelihood: high before M10
- Detection: Archive compatibility tests marked pending for M10.
- Mitigation: Expose archive retrieval/export hooks now.
- Fallback: Completion remains blocked from claiming migrated histories until M10.
**History body bytes change**
- Class: data
- Impact: Prompt/debug artifacts lose fidelity.
- Likelihood: medium
- Detection: Golden body tests.
- Mitigation: Store exact body and preserve export.
- Fallback: Treat body as opaque text.

## 26. Observability and Diagnostics

- History diagnostics include kind, sequence, logical path, live path, and rotation phase.
- Startup diagnostics can report pending live files and latest historical sequence.
- Export diagnostics include per-history-kind row/file counts.

## 27. Performance and Scalability Considerations

- Latest fallback queries should use indexes by history kind and sequence.
- Sequence allocation should avoid scanning exported files in SQLite mode.
- Measure write, latest read, and export times over large history fixtures.
- Deferred optimization: history body compression or paging if evidence shows need.

## 28. Security and Safety Considerations

- Validate history logical paths and stems; do not accept path traversal from filenames.
- Preserve live file write authority exactly as current loop flow permits.
- Avoid deleting live files unless the corresponding SQLite history write has succeeded.
- Treat markdown bodies as data and avoid logging full content by default.

## 29. Documentation Updates

- No documentation-only deliverable is required.
- Executable diagnostics should identify live versus historical source when reporting latest decisions/handoff.

## 30. Exit Criteria

- Historical decisions, handoffs, and deltas are canonical in SQLite.
- Live files remain filesystem-backed and live-first.
- Rotation and sequence allocation preserve current behavior.
- Exports/imports preserve histories and next sequence.
- No evidence/archive/transactional workflow capability is claimed.

## 31. Transition to Next Milestone

- Milestone 9 receives proven logical-path and sequence behavior for content-heavy migrated records.
- Milestone 10 receives loop history stores that must be included in completed epic archives.
- Remaining limitation: execution evidence remains file-backed and archives are not yet storage-agnostic.

<!-- GENERATED:END -->

<!-- MANUAL:START -->
<!-- Add manually maintained notes here. This block is preserved by the generator. -->
<!-- MANUAL:END -->
