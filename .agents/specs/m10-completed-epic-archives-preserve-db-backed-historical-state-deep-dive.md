# Milestone 10 — Completed Epic Archives Preserve DB-Backed Historical State Deep Dive

<!-- GENERATED:START -->
Generated from `.agents/specs/roadmap.md` and `.agents/specs/audit.md`.

## 1. Milestone Summary

- Milestone Identifier: Milestone 10
- Milestone Name: Completed Epic Archives Preserve DB-Backed Historical State
- Short Description: Update completion archives so DB-backed histories and execution evidence remain recoverable with completed epics.
- Implementation Role: Restores the completed-epic recoverability contract after histories and evidence leave canonical filesystem directories.
- Roadmap Position: Tenth milestone; follows migration of loop histories and execution evidence.
- Primary Outcomes:
- Completing an epic captures required historical records even when they are SQLite-backed.
- Archive exports include migrated historical records in deterministic filesystem form.
- Imported archive state restores the same logical archived state.

## 2. Normative Basis

**Roadmap Authority**
- Roadmap authority: `.agents/specs/roadmap.md` `# Project Goal`, `# Guiding Principles`, and this milestone's `## Milestone` section.
- `.agents/specs/roadmap.md` `## Milestone 10 — Completed Epic Archives Preserve DB-Backed Historical State`.
**Architectural Authority**
- Architectural authority: `.agents/specs/audit.md` sections `2. Persistence Authority`, `4. Persistence Semantics`, `5. Filesystem-Coupled Behaviors`, `8. Cross-Boundary Dependencies`, `9. Compatibility and Migration Constraints`, `10. Round-Trip Import/Export Requirements`, `14. Persistence Domain Inventory`, `16. Persistence Dependency Graph`, `17. Synchronization Model`, `19. Persistence Platform Capabilities`, and `20. Roadmap-Relevant Findings`.
**Implementation Authority**
- Implementation authority: existing LoopRelay persistence code under `src/LoopRelay.Roadmap.Cli/Services`, `src/LoopRelay.Cli/Services/Execution`, `src/LoopRelay.Completion/Services/ArtifactStorage`, `src/LoopRelay.Projections/Services`, `src/LoopRelay.Core/Abstractions/Artifacts/IArtifactStore.cs`, and `src/LoopRelay.Infrastructure/Services/Artifacts`.
**Supporting Context**
- Supporting context: current tests under `tests/LoopRelay.Roadmap.Cli.Tests`, `tests/LoopRelay.Cli.Tests`, `tests/LoopRelay.Completion.Tests`, `tests/LoopRelay.Projections.Tests`, and `tests/LoopRelay.Infrastructure.Tests`.
- No undocumented authority is introduced here; unresolved audit mismatches remain open questions rather than implementation permission.

## 3. Objective

Refactor completion behavior so completed epic archives associate, materialize, export, import, and recover SQLite-backed historical decisions, handoffs, deltas, and execution evidence while preserving retained filesystem artifact archival behavior.

## 4. Non-Goals

- Do not migrate retained live files or retained plan/spec/milestone/operational context files.
- Do not implement full workspace synchronization or conflict detection.
- Do not change completed-epic discovery semantics except as needed to include DB-backed records deterministically.
- Do not silently drop migrated historical records when archive association is incomplete.

## 5. Runtime / System State Before

- Completion archive service moves files/directories directly into `.agents/archive/completed-epics/{index}`.
- Histories and execution evidence are now canonical SQLite records.
- Retained live and human-facing files still exist on disk and follow existing archive flow.
- Archive indexing and discovery are filesystem-shaped.

## 6. Runtime / System State After

- Archive completion associates migrated historical records with the completed epic.
- Completion-time export/materialization includes DB-backed histories and evidence.
- Archived historical state can be recovered/imported with the completed epic.
- Retained filesystem artifacts continue archiving as before.
- No migrated historical record is silently dropped during completion.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| Archive association for migrated records | Completion archive service and domain stores | Identify which DB-backed histories/evidence belong to completed epic. | Completion request, state/journal references, history/evidence stores. | Archive association rows/metadata. | Milestones 8 and 9 stores. | Historical decisions, handoffs, deltas, and evidence remain recoverable with completed epic. | Archive recovery, export/import, verification. |
| Completion-time materialization | Archive materializer | Export associated DB-backed records into deterministic archive filesystem form. | Archive association and domain records. | Archived markdown/evidence files under completed epic archive. | Domain export serializers. | Exported archive state includes migrated records deterministically. | Git review, backup/restore, older archive readers. |
| Archived state import/recovery | Archive importer/recovery service | Restore archived histories/evidence from exported archive state. | Completed epic archive export. | Logical archived state in database or recovery view. | Filesystem import serializers. | Importing exported archive restores same logical archived state. | Verification, backup/restore, completion discovery. |
| Retained artifact preservation | Completion archive service | Keep current file-based archive handling for retained files. | Plan, context, milestones, live files, review files. | Archived retained files as before. | Existing `CompletedEpicArchiveService` behavior. | Live retained files preserved exactly as before. | Completion workflows and archive readers. |

## 8. Architectural Responsibilities

- Completion archive service owns completed-epic archive boundary and index/discovery behavior.
- Domain stores own retrieval/export of their migrated records.
- Archive association owns which records belong to an epic; it must be deterministic and auditable through persisted references.
- Import/recovery owns restoration of archived logical state without changing active workspace state unless explicitly requested.

**Cross-Milestone Constraints**
- SQLite becomes canonical only for machine-managed migrated domains.
- Retained markdown prompt/workflow artifacts remain filesystem-backed.
- Filesystem exports are deterministic, importable serializations of migrated domain state.
- Repo-relative path strings remain durable logical identities.
- `DNNNN`, `NNNN`, family IDs, runtime prompt names, and correlation IDs preserve imported identity.
- Behavior moves behind semantic domain operations before callers stop using filesystem representations.
- Freshness semantics remain trustworthy across retained files and migrated records.

## 9. Components and Modules

**Archive association service**
- Purpose: Select DB-backed historical records for a completed epic.
- Responsibilities: Use state, journal, paths, and completion context to associate histories/evidence.
- Owned State: Archive association metadata if needed.
- Consumed State: History/evidence stores, journal, completion request.
- Public Contracts: Get associated migrated records for archive.
- Internal Contracts: No silent omission; missing association is failure.
- Dependencies: Milestones 8 and 9 stores.
- Tests Required: Association coverage and missing-record tests.
**Archive materializer**
- Purpose: Write deterministic archive filesystem representation.
- Responsibilities: Export associated histories/evidence into archive paths and keep retained file moves unchanged.
- Owned State: Archive filesystem contents.
- Consumed State: Domain records and retained files.
- Public Contracts: Complete archive operation result.
- Internal Contracts: Materialization is idempotent for same archive index/source state.
- Dependencies: Export serializers.
- Tests Required: Archive content golden tests.
**Archive importer/recovery**
- Purpose: Restore logical archived state from exported archive.
- Responsibilities: Parse archived migrated records and reconstruct archived state view or database records.
- Owned State: Recovered archived domain state.
- Consumed State: Archive export tree.
- Public Contracts: Import/recover completed epic archive.
- Internal Contracts: Does not mix archived records into active histories unless explicitly requested.
- Dependencies: Import serializers.
- Tests Required: Archive import equality tests.

## 10. Repository and File Impact

- `src/LoopRelay.Roadmap.Cli/Services/Artifacts` and `RoadmapArtifactPaths` remain the source of existing roadmap artifact path constants until a shared path layer replaces them.
- `src/LoopRelay.Roadmap.Cli/Services/State`, `TransitionState`, `TransitionCoordination`, `Decisions`, `ExecutionPreparation`, `Projections`, `Splits`, and `ArtifactManagement` contain the current roadmap domain stores.
- `src/LoopRelay.Cli/Services/Execution/LoopArtifacts.cs` owns live decision, handoff, and operational delta loop-file behavior today.
- `src/LoopRelay.Completion/Services/ArtifactStorage` owns completed-epic archive and evidence access used by completion workflows.
- `src/LoopRelay.Infrastructure/Services/Artifacts` contains repository-relative path bridging and numbered artifact sequence helpers.
- Update `src/LoopRelay.Completion/Services/ArtifactStorage/CompletedEpicArchiveService.cs` and roadmap archive evidence loaders.
- Add adapters from completion archive service to SQLite history/evidence stores.
- Tests extend completion archive, completed epic evidence loader, and completion certification suites.

## 11. Public Contracts

- Completed epic archive discovery remains deterministic and compatible with existing completed-epic discovery behavior.
- Archive exports include deterministic filesystem representations of associated migrated records.
- Archive import/recovery exposes logical archived state without pretending it is active workspace state.
- Archive failure reports missing migrated records rather than dropping them.

## 12. Internal Contracts

- Archive materialization must read DB-backed records before destructive retained-file moves where failure would cause loss.
- Associated record selection must be deterministic from persisted state/journal/paths.
- Archive export paths must not collide across histories/evidence or retained files.
- Import of archive state preserves archived logical paths and original sequences.

## 13. Data and State Model

**Archive association metadata**
- Owner: Completion archive service
- Lifecycle: Created during completion and read during recovery/import.
- Durability: SQLite and/or deterministic archive manifest as executable metadata if required.
- Mutability: Immutable after archive completion.
- Identity: Completed epic archive ID/index plus logical record paths.
- Validation: All referenced migrated records exist.
- Recovery: Rebuild from archive export where possible.
- Consumers: Archive materializer, recovery, verification.
**Archived migrated histories/evidence**
- Owner: Archive materializer
- Lifecycle: Materialized on completion, imported/recovered later.
- Durability: Archive filesystem export and/or database archive records.
- Mutability: Immutable archived state.
- Identity: Original logical paths plus archive identity.
- Validation: Export/import equality and hash checks.
- Recovery: Import archive export.
- Consumers: Completed epic discovery, backup/restore, verification.
**Retained archived files**
- Owner: Existing completion archive flow
- Lifecycle: Moved/copied according to current behavior.
- Durability: Archive filesystem.
- Mutability: Archived immutable files.
- Identity: Existing archive paths.
- Validation: Current archive tests.
- Recovery: Existing archive read behavior.
- Consumers: Archive readers and completion workflows.

## 14. Lifecycle and State Transitions

- Completion archive: Determine archive index -> collect retained files and DB-backed associations -> validate all records -> materialize archive -> mark association complete.
- Recovery: Read archive -> import retained and migrated records -> reconstruct completed epic archived state -> verify equality.
- Failure: Missing migrated record or materialization error aborts archive before claiming completion; retained file behavior must avoid irreversible partial loss.

## 15. Execution Flow

- Startup/discovery lists completed epic archives as before and can include migrated record metadata.
- Normal completion collects both retained filesystem artifacts and DB-backed history/evidence records.
- Failure flow reports missing/invalid migrated records and does not silently omit them.
- Recovery flow imports exported archive into a clean or recovery context and compares logical archived state.
- Export flow writes deterministic archive contents for Git review/backup.

## 16. Dependency Closure

- Hard prerequisite: Milestone 8 loop histories in SQLite.
- Hard prerequisite: Milestone 9 execution evidence in SQLite.
- Hard prerequisite: Milestone 7 journal for persisted references where association uses journal data.
- Inherited capability: deterministic export/import serializers.
- Future dependency: Milestone 11 workspace-level sync and Milestone 13 verification.
- Enables Milestones 11, 12, and 13.

## 17. Failure Modes

**Migrated record omitted**
- Description: Archive completes without associated history/evidence.
- Detection: Pre-completion association validation and post-archive verification.
- Behavior: Fail archive; do not claim completion.
- Recovery: Restore/import missing record and retry completion.
- Diagnostics: Archive ID, missing logical path, source domain.
- Tests: Missing record archive test.
**Archive materialization collision**
- Description: Export paths inside completed epic archive collide.
- Detection: Materializer path validation.
- Behavior: Abort before overwrite.
- Recovery: Fix archive path mapping or duplicate source identity.
- Diagnostics: Target archive path and source records.
- Tests: Collision fixture.
**Partial archive after failure**
- Description: Some retained files moved but DB-backed records not materialized.
- Detection: Archive transaction/verification check.
- Behavior: Report partial/corrupt archive and trigger recovery/rollback where available.
- Recovery: Restore retained files from archive or complete materialization from DB.
- Diagnostics: Archive phase and completed steps.
- Tests: Injected failure during materialization.

## 18. Validation and Invariants

**No migrated historical record is silently dropped during completion.**
- Source Authority: Milestone 10 acceptance criteria.
- Enforcement Point: Association validation and archive verification.
- Failure Behavior: Archive fails or is marked invalid.
- Test Strategy: Archive fixtures with all DB-backed record kinds.
**Retained filesystem artifacts are preserved exactly as before.**
- Source Authority: Milestone 10 acceptance criteria.
- Enforcement Point: Existing archive regression tests.
- Failure Behavior: Retained archive diff mismatch.
- Test Strategy: Run current completion archive tests plus migrated mode.
**Exported archive state imports to equivalent logical archived state.**
- Source Authority: Milestone 10 acceptance criteria.
- Enforcement Point: Archive export/import equality tests.
- Failure Behavior: Recovered state mismatch.
- Test Strategy: Clean archive import fixture.

## 19. Testing Strategy

- Unit tests for archive association selection.
- Integration tests completing an epic with DB-backed histories and evidence.
- Archive materialization golden tests.
- Archive import/recovery equality tests.
- Regression tests for retained filesystem archive behavior.
- Failure-path tests for missing records, materialization collisions, partial archive, and invalid archive index.

## 20. Fixtures and Test Data

- Completed epic with associated decisions, handoffs, deltas, and execution evidence.
- Archive tree with retained files plus materialized migrated records.
- Missing migrated record fixture.
- Path collision fixture.
- Partial archive fixture from injected failure.
- Legacy archive fixture without DB-backed records for compatibility.

## 21. Acceptance Demonstration

- Seed a workspace with SQLite-backed histories/evidence and retained active files.
- Complete an epic.
- Inspect archive export and verify retained files plus materialized histories/evidence are present.
- Import the exported archive into a clean recovery context.
- Compare recovered archived state to source associated records.

## 22. Certification Evidence

- Completion archive test output in migrated mode.
- Archive association report showing all migrated record kinds.
- Archive export/import equality report.
- Failure-path diagnostics for missing record and partial archive fixtures.

## 23. Implementation Plan

**Define archive association**
- Purpose: Select migrated records belonging to completed epic.
- Deliverables: Association service/query and validation.
- Dependencies: History/evidence stores and journal/state references.
- Completion: Association tests cover all record kinds.
**Refactor archive materialization**
- Purpose: Include DB-backed records without breaking retained file flow.
- Deliverables: Materializer and archive path mapping.
- Dependencies: Association service.
- Completion: Archive content tests pass.
**Implement archive import/recovery**
- Purpose: Restore logical archived state.
- Deliverables: Archive import/recovery APIs.
- Dependencies: Materialized export shape.
- Completion: Clean recovery equality passes.
**Harden failure paths**
- Purpose: Prevent silent omission and partial loss.
- Deliverables: Validation and injected failure tests.
- Dependencies: Materializer.
- Completion: Partial/missing fixtures fail safely.

## 24. Parallel Work Opportunities

**Association queries**
- Owner: Completion/persistence engineer
- Dependencies: History/evidence schema.
- Sync: Archive identity and state/journal references.
- Risk: Association under-selects records.
**Materialization export**
- Owner: Serialization engineer
- Dependencies: Archive path mapping.
- Sync: Retained archive layout.
- Risk: Path collisions with existing archive files.
**Recovery tests**
- Owner: Test engineer
- Dependencies: Export shape stable.
- Sync: Logical archived-state equality.
- Risk: Tests depend on active workspace state.

## 25. Risks and Mitigations

**Incomplete association model**
- Class: architectural
- Impact: Archives miss required evidence/history.
- Likelihood: medium
- Detection: Association coverage and verification.
- Mitigation: Use persisted references from state/journal/completion context and fail on unresolved records.
- Fallback: Archive all records in relevant completion window if precise association unavailable.
**Filesystem archive remains non-transactional**
- Class: operational
- Impact: Partial archive on crash.
- Likelihood: medium
- Detection: Injected failure tests.
- Mitigation: Validate before moves and materialize into staging before finalizing archive.
- Fallback: Provide recovery that completes or marks partial archive invalid.
**Archive import pollutes active state**
- Class: data
- Impact: Recovered archived records become active histories.
- Likelihood: low
- Detection: Recovery isolation tests.
- Mitigation: Separate archived-state identity from active workspace identity.
- Fallback: Require explicit restore command to promote archived records.

## 26. Observability and Diagnostics

- Archive diagnostics include archive ID/index, retained file count, migrated record counts, and missing record list.
- Materialization diagnostics include target archive paths and hash summaries.
- Recovery diagnostics include restored record counts and equality status.

## 27. Performance and Scalability Considerations

- Archive materialization scales with associated histories/evidence bodies.
- Likely bottleneck is exporting large evidence sets.
- Measure completion archive duration, materialized file count, and bytes written.
- Deferred optimization: streaming materialization and archive manifest indexes.

## 28. Security and Safety Considerations

- Validate archive paths stay under completed epic archive root.
- Do not overwrite existing archive contents on collision.
- Avoid losing retained files by using staging or preflight validation.
- Do not expose full evidence bodies in logs unless explicit debug output requests them.

## 29. Documentation Updates

- No documentation-only deliverable is required.
- Executable archive diagnostics should report migrated record inclusion because it affects user-visible completion safety.

## 30. Exit Criteria

- Completion archives include and recover DB-backed histories and execution evidence.
- Retained filesystem artifact archive behavior remains compatible.
- Archive export/import equality passes.
- Failure paths prevent silent record drops.
- No full workspace synchronization or transactional workflow recovery is claimed.

## 31. Transition to Next Milestone

- Milestone 11 receives archive-aware export/import behavior for workspace synchronization.
- Milestone 13 receives archive recoverability checks.
- Remaining limitation: workspace sync/conflict handling is not complete.

## Open Implementation Questions

- The exact archive-level export contract is not fully specified by the roadmap; this milestone must choose the minimal deterministic materialization that preserves recoverability and existing discovery behavior.

<!-- GENERATED:END -->

<!-- MANUAL:START -->
<!-- Add manually maintained notes here. This block is preserved by the generator. -->
<!-- MANUAL:END -->
