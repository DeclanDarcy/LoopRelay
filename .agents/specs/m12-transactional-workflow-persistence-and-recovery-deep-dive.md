# Milestone 12 — Transactional Workflow Persistence and Recovery Deep Dive

<!-- GENERATED:START -->
Generated from `.agents/specs/roadmap.md` and `.agents/specs/audit.md`.

## 1. Milestone Summary

- Milestone Identifier: Milestone 12
- Milestone Name: Transactional Workflow Persistence and Recovery
- Short Description: Make multi-domain workflow updates transactionally safe across SQLite-backed domains and retained filesystem artifacts.
- Implementation Role: Improves correctness and recovery after the migration by grouping related workflow persistence changes.
- Roadmap Position: Twelfth milestone; follows workspace synchronization and all domain migrations.
- Primary Outcomes:
- Workflow transitions commit or fail as coherent persistence changes.
- Integrity validation detects orphaned or inconsistent cross-domain state.
- Recovery distinguishes retryable partial work from corrupt state.

## 2. Normative Basis

**Roadmap Authority**
- Roadmap authority: `.agents/specs/roadmap.md` `# Project Goal`, `# Guiding Principles`, and this milestone's `## Milestone` section.
- `.agents/specs/roadmap.md` `## Milestone 12 — Transactional Workflow Persistence and Recovery`.
**Architectural Authority**
- Architectural authority: `.agents/specs/audit.md` sections `2. Persistence Authority`, `4. Persistence Semantics`, `5. Filesystem-Coupled Behaviors`, `8. Cross-Boundary Dependencies`, `9. Compatibility and Migration Constraints`, `10. Round-Trip Import/Export Requirements`, `14. Persistence Domain Inventory`, `16. Persistence Dependency Graph`, `17. Synchronization Model`, `19. Persistence Platform Capabilities`, and `20. Roadmap-Relevant Findings`.
**Implementation Authority**
- Implementation authority: existing LoopRelay persistence code under `src/LoopRelay.Roadmap.Cli/Services`, `src/LoopRelay.Cli/Services/Execution`, `src/LoopRelay.Completion/Services/ArtifactStorage`, `src/LoopRelay.Projections/Services`, `src/LoopRelay.Core/Abstractions/Artifacts/IArtifactStore.cs`, and `src/LoopRelay.Infrastructure/Services/Artifacts`.
**Supporting Context**
- Supporting context: current tests under `tests/LoopRelay.Roadmap.Cli.Tests`, `tests/LoopRelay.Cli.Tests`, `tests/LoopRelay.Completion.Tests`, `tests/LoopRelay.Projections.Tests`, and `tests/LoopRelay.Infrastructure.Tests`.
- No undocumented authority is introduced here; unresolved audit mismatches remain open questions rather than implementation permission.

## 3. Objective

Implement transaction boundaries, recovery classification, integrity validation, and concurrency behavior for multi-domain workflow updates involving SQLite-backed domains and retained filesystem artifacts, without overwriting retained files inconsistently with committed database state.

## 4. Non-Goals

- Do not migrate retained filesystem artifacts into SQLite.
- Do not remove filesystem export/import compatibility.
- Do not implement repair-by-mutation unless explicitly asked; recovery may classify and guide retry without mutating by default.
- Do not claim distributed transaction guarantees for filesystem operations beyond designed staging/commit behavior.

## 5. Runtime / System State Before

- Most machine-managed domains are SQLite-backed and workspace sync exists.
- Workflow updates still may touch multiple domains and retained files in weakly coordinated order.
- Journal, state, decisions, splits, provenance, histories, evidence, and archives can be related but not always atomically updated.

## 6. Runtime / System State After

- Roadmap transitions, decision recording/state updates, split lineage, provenance updates, journal events, and related workflow writes use explicit transaction boundaries.
- Failures do not leave committed state claiming missing outputs.
- Sequence allocation remains unique under concurrent attempts.
- Integrity validation detects orphaned evidence/history, missing logical paths, duplicate identities, invalid archive references, and inconsistent transition state.
- Recovery classifies retryable partial work versus corrupt state.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| Workflow transaction boundaries | Workflow persistence coordinator | Group related SQLite domain writes for roadmap transitions, decisions, splits, provenance, journal, histories, evidence, and archives. | Workflow operation plan and domain write actions. | Committed or rolled-back domain changes. | SQLite transaction support and domain stores. | Failed transition does not leave committed state claiming missing outputs. | Roadmap transition runner, loop runner, completion. |
| Retained filesystem coordination | Workflow persistence coordinator | Stage or order retained file writes so they do not contradict committed SQLite state. | Retained artifact writes/deletes and database transaction state. | Coherent retained file and database state. | Current filesystem artifact store semantics. | Retained filesystem artifacts are not overwritten inconsistently with committed SQLite state. | Loop/roadmap/completion workflows. |
| Cross-domain integrity validation | Integrity validator | Validate references and invariants across SQLite domains and retained files. | Database state, retained file resolver, sync/export metadata. | Integrity report with corruption/retryable classifications. | Logical resolver and sync metadata. | Detects orphaned evidence/history, missing paths, duplicates, and invalid archive references. | Recovery, verification, startup checks. |
| Crash recovery classification | Recovery service | Classify partially completed workflow phases as retryable or corrupt. | Journal events, transaction markers, domain state, retained files. | Recovery status and allowed retry/repair action. | SQLite journal and workflow transaction markers. | Recovery distinguishes retryable partial work from corrupt state. | CLI startup/resume, verification. |
| Concurrent sequence safety | Domain stores | Ensure unique decision/history/evidence sequences under concurrent attempts. | Concurrent domain writes. | Unique committed identities or deterministic conflicts. | Database constraints and transactions. | Sequence allocation remains unique under concurrent attempts. | All append/sequence domains. |

## 8. Architectural Responsibilities

- Workflow persistence coordinator owns transaction boundaries and ordering across domain writes.
- Domain stores own local constraints and sequence allocation inside transactions.
- Filesystem coordinator owns staging/commit behavior for retained artifacts.
- Integrity validator owns cross-domain validation and recovery classification, not silent repair.
- Journal records must represent started/completed/failed transitions accurately even on workflow errors.

**Cross-Milestone Constraints**
- SQLite becomes canonical only for machine-managed migrated domains.
- Retained markdown prompt/workflow artifacts remain filesystem-backed.
- Filesystem exports are deterministic, importable serializations of migrated domain state.
- Repo-relative path strings remain durable logical identities.
- `DNNNN`, `NNNN`, family IDs, runtime prompt names, and correlation IDs preserve imported identity.
- Behavior moves behind semantic domain operations before callers stop using filesystem representations.
- Freshness semantics remain trustworthy across retained files and migrated records.

## 9. Components and Modules

**Workflow persistence coordinator**
- Purpose: Execute multi-domain writes coherently.
- Responsibilities: Open transaction, invoke domain writes, coordinate retained file staging, commit/rollback, emit journal outcomes.
- Owned State: Transaction markers if needed.
- Consumed State: Domain stores and retained file operations.
- Public Contracts: Run workflow persistence unit.
- Internal Contracts: No domain write bypasses coordinator for covered workflows.
- Dependencies: SQLite transaction API and file staging.
- Tests Required: Failure and rollback tests.
**Filesystem staging/commit adapter**
- Purpose: Coordinate retained file writes with database transactions.
- Responsibilities: Stage retained writes/deletes, finalize after DB commit or restore on failure where possible.
- Owned State: Temporary staging files/markers.
- Consumed State: Retained artifact paths.
- Public Contracts: Stage/commit/rollback file mutations.
- Internal Contracts: Never overwrites retained files contrary to committed DB state.
- Dependencies: `IArtifactStore` and existing atomic writes.
- Tests Required: Injected file failure tests.
**Cross-domain integrity validator**
- Purpose: Detect inconsistent persisted state.
- Responsibilities: Check references, orphans, duplicates, archive validity, missing logical paths, sequence uniqueness.
- Owned State: No durable state except reports if executable verification records them.
- Consumed State: Database, resolver, sync metadata.
- Public Contracts: Integrity validation result.
- Internal Contracts: Read-only unless explicit repair mode exists.
- Dependencies: All domain stores.
- Tests Required: Corruption fixture tests.
**Recovery classifier**
- Purpose: Classify partial workflow states.
- Responsibilities: Use journal and transaction markers to identify retryable/corrupt states.
- Owned State: Recovery markers if required.
- Consumed State: Journal, domain states, retained files.
- Public Contracts: Recovery status and retry guidance.
- Internal Contracts: Does not mutate by default.
- Dependencies: Journal and integrity validator.
- Tests Required: Crash simulation tests.

## 10. Repository and File Impact

- `src/LoopRelay.Roadmap.Cli/Services/Artifacts` and `RoadmapArtifactPaths` remain the source of existing roadmap artifact path constants until a shared path layer replaces them.
- `src/LoopRelay.Roadmap.Cli/Services/State`, `TransitionState`, `TransitionCoordination`, `Decisions`, `ExecutionPreparation`, `Projections`, `Splits`, and `ArtifactManagement` contain the current roadmap domain stores.
- `src/LoopRelay.Cli/Services/Execution/LoopArtifacts.cs` owns live decision, handoff, and operational delta loop-file behavior today.
- `src/LoopRelay.Completion/Services/ArtifactStorage` owns completed-epic archive and evidence access used by completion workflows.
- `src/LoopRelay.Infrastructure/Services/Artifacts` contains repository-relative path bridging and numbered artifact sequence helpers.
- Update transition coordination, loop execution, split, provenance, and completion flows to use transaction coordinator where they cross domains.
- Extend tests in transition coordination, execution, state, splits, completion, and persistence areas.
- No retained human-facing artifact path changes are expected.

## 11. Public Contracts

- Workflow persistence transaction API for covered operations.
- Integrity validation result categories: valid, retryable partial, corrupt, unsupported, conflict.
- Recovery status API/command behavior if exposed.
- Journal semantics accurately record started/completed/failed outcomes.

## 12. Internal Contracts

- Covered workflows define their persistence unit before first mutating write.
- Journal started record must not be lost when a workflow begins; completed/failed record must reflect actual outcome.
- Filesystem writes are staged or ordered to avoid contradiction with committed database state.
- Sequence allocation and append identities rely on database constraints inside transactions.

## 13. Data and State Model

**Workflow transaction marker**
- Owner: Workflow persistence coordinator
- Lifecycle: Created at workflow persistence start, completed/failed on outcome.
- Durability: SQLite and/or journal as needed.
- Mutability: Transitions from started to completed/failed.
- Identity: Workflow/correlation ID.
- Validation: Marker state matches journal/domain outcomes.
- Recovery: Used to classify retryable partial work.
- Consumers: Recovery and integrity validation.
**Staged retained file mutations**
- Owner: Filesystem staging adapter
- Lifecycle: Created before final file commit, removed after commit/rollback.
- Durability: Temporary filesystem staging.
- Mutability: Transient.
- Identity: Target retained path and transaction ID.
- Validation: Target path and expected prior state.
- Recovery: Rollback or complete staged mutation.
- Consumers: Workflow coordinator.
**Integrity report**
- Owner: Integrity validator
- Lifecycle: Generated on validation/recovery/verification.
- Durability: Transient unless verification mode stores reports.
- Mutability: Immutable result.
- Identity: Workspace validation run.
- Validation: Self-describing result categories.
- Recovery: Rerun after repair/retry.
- Consumers: Startup, verification, operators.

## 14. Lifecycle and State Transitions

- Transaction: Planned -> Started -> Domain writes staged/executed -> Retained file finalization -> Committed -> Journal completed.
- Failure: Started -> error during domain/file write -> rollback DB/staged files -> journal failed -> recovery classification.
- Recovery: Inspect markers/journal/state -> classify retryable or corrupt -> allow retry or block with diagnostics.

## 15. Execution Flow

- Startup may run lightweight integrity/recovery checks for incomplete transactions.
- Normal operation executes covered workflow persistence through coordinator.
- Failure flow rolls back database changes, restores/stops staged file mutations, and records failed journal event when possible.
- Recovery flow uses journal/markers to decide retryability and prevents corrupt state from being trusted.
- Shutdown has no open transaction; in-progress markers are recoverable on next startup.

## 16. Dependency Closure

- Hard prerequisite: Milestones 5-11 canonical domain stores and sync.
- Hard prerequisite: Milestone 7 SQLite journal.
- Supporting infrastructure: filesystem atomic writes/staging and logical resolver.
- Future dependency: Milestone 13 verification mode consumes integrity checks.
- Enables Milestone 13.

## 17. Failure Modes

**Database rollback after partial domain writes**
- Description: A covered workflow fails after several domain writes.
- Detection: Exception within transaction.
- Behavior: Rollback all DB writes and record failure.
- Recovery: Retry workflow if retained file state permits.
- Diagnostics: Workflow ID, domain phase, rolled-back status.
- Tests: Injected domain write failure.
**Retained file finalization fails**
- Description: Database commit succeeded or is ready but retained file write/delete fails.
- Detection: File staging/finalization error.
- Behavior: Classify retryable/corrupt based on commit point and staged state.
- Recovery: Complete or roll back staged file operation if possible.
- Diagnostics: Target path, transaction ID, file phase.
- Tests: Injected file write/delete failure.
**Orphaned reference**
- Description: State/journal/archive references evidence/history/path that does not exist.
- Detection: Integrity validator via resolver.
- Behavior: Report corrupt or retryable partial depending on markers.
- Recovery: Restore/import missing record or retry producing workflow.
- Diagnostics: Referring domain and missing path.
- Tests: Orphaned evidence/history/archive fixtures.
**Concurrent sequence conflict**
- Description: Two workflow attempts allocate same sequence.
- Detection: Unique constraint/transaction conflict.
- Behavior: One succeeds, one retries/fails deterministically.
- Recovery: Retry failed attempt after reloading sequence.
- Diagnostics: Domain and attempted identity.
- Tests: Concurrent append/sequence tests.

## 18. Validation and Invariants

**A failed transition does not leave committed state that claims outputs exist when required records do not.**
- Source Authority: Milestone 12 acceptance criteria.
- Enforcement Point: Transaction coordinator and integrity validation.
- Failure Behavior: Integrity report corrupt; test fails.
- Test Strategy: Injected failures after each persistence phase.
**Journal events accurately represent started, completed, and failed transitions.**
- Source Authority: Milestone 12 acceptance criteria.
- Enforcement Point: Journal integration tests.
- Failure Behavior: Missing or inaccurate journal outcome.
- Test Strategy: Error-path transition tests.
**Retained filesystem artifacts are not overwritten inconsistently with committed SQLite state.**
- Source Authority: Milestone 12 acceptance criteria.
- Enforcement Point: File staging and recovery tests.
- Failure Behavior: Mismatch classified as corrupt.
- Test Strategy: Injected retained-file write/delete failures.

## 19. Testing Strategy

- Unit tests for transaction coordinator and file staging adapter.
- Integration tests for roadmap transitions, decision recording/state update, split lineage, provenance update, journal events, loop histories/evidence, and archive operations under transaction coordinator.
- Crash/recovery simulation tests with injected failures at every persistence phase.
- Integrity validation tests for orphaned evidence/history, missing paths, duplicates, invalid archive references, and stale sync metadata.
- Concurrency tests for sequence allocation and appends.
- Performance smoke tests for transaction overhead in representative workflows.

## 20. Fixtures and Test Data

- Valid full SQLite workspace.
- Retryable partial workflow markers.
- Corrupt orphaned evidence/history references.
- Invalid archive reference fixture.
- Duplicate identity fixture.
- Staged retained file mutation fixture.
- Concurrent sequence allocation harness.

## 21. Acceptance Demonstration

- Run a roadmap transition with injected failure after decision ledger append but before state update and verify rollback/integrity.
- Run split lineage creation with injected failure after child references and verify no inconsistent committed split state.
- Run evidence write plus state/journal update with injected file finalization failure and verify recovery classification.
- Run integrity validation and confirm valid/retryable/corrupt distinctions.

## 22. Certification Evidence

- Transaction failure matrix output.
- Integrity validation report for valid and corrupted fixtures.
- Concurrency sequence allocation test output.
- Recovery classification transcript.

## 23. Implementation Plan

**Define workflow persistence units**
- Purpose: Identify executable multi-domain write boundaries.
- Deliverables: Coordinator integration points for transitions, splits, provenance, journal, histories/evidence, archives.
- Dependencies: Existing workflow code.
- Completion: Covered workflows route through coordinator.
**Implement coordinator and staging**
- Purpose: Commit/rollback database and retained file mutations coherently.
- Deliverables: Transaction coordinator and file staging adapter.
- Dependencies: SQLite transactions and artifact store.
- Completion: Injected failure tests pass.
**Implement integrity validator**
- Purpose: Detect cross-domain corruption.
- Deliverables: Validation rules and reports.
- Dependencies: Resolver and all stores.
- Completion: Corruption fixtures classified.
**Implement recovery classifier**
- Purpose: Distinguish retryable partial work from corruption.
- Deliverables: Recovery status model and checks.
- Dependencies: Journal/markers/integrity validator.
- Completion: Crash simulation tests pass.

## 24. Parallel Work Opportunities

**Transaction coordinator**
- Owner: Persistence engineer
- Dependencies: Domain stores stable.
- Sync: Workflow integration points.
- Risk: Coordinator becomes too generic and obscures domain behavior.
**Filesystem staging**
- Owner: Infrastructure engineer
- Dependencies: Artifact store semantics.
- Sync: Retained artifact paths.
- Risk: Staging breaks existing atomic write assumptions.
**Integrity/recovery fixtures**
- Owner: Test engineer
- Dependencies: Validation rule list.
- Sync: Corruption category definitions.
- Risk: Fixture-only corruption not representative.

## 25. Risks and Mitigations

**Filesystem/database atomicity mismatch**
- Class: architectural
- Impact: True atomic commit cannot span SQLite and arbitrary files.
- Likelihood: high
- Detection: Crash simulation tests.
- Mitigation: Use staging, commit ordering, markers, and recovery classification instead of claiming impossible guarantees.
- Fallback: Prefer database commit as source and regenerate retained/export files where safe.
**Transaction scope too broad**
- Class: performance
- Impact: Locks and contention slow workflows.
- Likelihood: medium
- Detection: Transaction timing tests.
- Mitigation: Keep scopes to persistence units and avoid prompt execution inside transactions.
- Fallback: Split transaction into staged phases with explicit markers.
**Recovery mutates incorrectly**
- Class: data
- Impact: Partial state becomes worse.
- Likelihood: medium
- Detection: Recovery fixtures.
- Mitigation: Classify by default; require explicit repair for mutation.
- Fallback: Block workflow until user/import repair.

## 26. Observability and Diagnostics

- Transaction diagnostics include workflow ID, correlation ID, phase, domains touched, commit/rollback outcome, and retained file staging status.
- Integrity reports include rule IDs, severity, domain, identity/path, and recovery classification.
- Health checks can report in-progress/retryable/corrupt workflow states.

## 27. Performance and Scalability Considerations

- Do not hold database transactions across long-running agent/prompt execution.
- Likely bottlenecks are broad integrity validation and large archive/evidence operations.
- Measure transaction duration, lock contention, and validation runtime.
- Deferred optimization: incremental integrity checks and domain version dependency graph.

## 28. Security and Safety Considerations

- Ensure staging paths cannot escape workspace.
- Do not expose sensitive prompt/evidence bodies in transaction logs.
- Avoid destructive retained file operations without validated staging/recovery path.
- Use database constraints to prevent policy bypass through duplicate identities.

## 29. Documentation Updates

- No documentation-only deliverable is required.
- Executable recovery/verification output must accurately distinguish retryable partial state from corruption.

## 30. Exit Criteria

- Covered multi-domain workflow writes use transaction coordinator.
- Failure paths are covered and rollback/recovery behavior is deterministic.
- Integrity validation detects required cross-domain corruption classes.
- Concurrent sequence allocation remains unique.
- Retained filesystem files are coordinated safely.
- No impossible cross-store atomicity guarantee is claimed.

## 31. Transition to Next Milestone

- Milestone 13 receives integrity validation and recovery checks to expose as verification mode.
- Remaining limitation: verification mode is not yet a complete user/test executable surface.

## Open Implementation Questions

- The roadmap requires transactionally safe behavior across SQLite-backed domains and retained files; because true atomicity across SQLite and filesystem is limited, this milestone must implement staging/recovery semantics and avoid claiming stronger guarantees than the software can enforce.

<!-- GENERATED:END -->

<!-- MANUAL:START -->
<!-- Add manually maintained notes here. This block is preserved by the generator. -->
<!-- MANUAL:END -->
