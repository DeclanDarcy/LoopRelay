# Milestone 13 — Storage Compatibility and Verification Mode Deep Dive

<!-- GENERATED:START -->
Generated from `.agents/specs/roadmap.md` and `.agents/specs/audit.md`.

## 1. Milestone Summary

- Milestone Identifier: Milestone 13
- Milestone Name: Storage Compatibility and Verification Mode
- Short Description: Add executable verification that proves filesystem-backed and SQLite-backed persistence are behaviorally equivalent and mutually consistent.
- Implementation Role: Provides rollout confidence, regression protection, and workspace consistency checks after migration.
- Roadmap Position: Final roadmap milestone; depends on migrated domains, synchronization, and transaction recovery.
- Primary Outcomes:
- Verification succeeds for valid SQLite-canonical and imported legacy workspaces.
- Verification detects stale exports, missing files, unresolved paths, nondeterministic round trips, and unrecoverable archives.
- Verification runs read-only unless explicitly asked to repair or re-export.

## 2. Normative Basis

**Roadmap Authority**
- Roadmap authority: `.agents/specs/roadmap.md` `# Project Goal`, `# Guiding Principles`, and this milestone's `## Milestone` section.
- `.agents/specs/roadmap.md` `## Milestone 13 — Storage Compatibility and Verification Mode`.
**Architectural Authority**
- Architectural authority: `.agents/specs/audit.md` sections `2. Persistence Authority`, `4. Persistence Semantics`, `5. Filesystem-Coupled Behaviors`, `8. Cross-Boundary Dependencies`, `9. Compatibility and Migration Constraints`, `10. Round-Trip Import/Export Requirements`, `14. Persistence Domain Inventory`, `16. Persistence Dependency Graph`, `17. Synchronization Model`, `19. Persistence Platform Capabilities`, and `20. Roadmap-Relevant Findings`.
**Implementation Authority**
- Implementation authority: existing LoopRelay persistence code under `src/LoopRelay.Roadmap.Cli/Services`, `src/LoopRelay.Cli/Services/Execution`, `src/LoopRelay.Completion/Services/ArtifactStorage`, `src/LoopRelay.Projections/Services`, `src/LoopRelay.Core/Abstractions/Artifacts/IArtifactStore.cs`, and `src/LoopRelay.Infrastructure/Services/Artifacts`.
**Supporting Context**
- Supporting context: current tests under `tests/LoopRelay.Roadmap.Cli.Tests`, `tests/LoopRelay.Cli.Tests`, `tests/LoopRelay.Completion.Tests`, `tests/LoopRelay.Projections.Tests`, and `tests/LoopRelay.Infrastructure.Tests`.
- No undocumented authority is introduced here; unresolved audit mismatches remain open questions rather than implementation permission.

## 3. Objective

Implement an executable verification mode that checks storage conformance across file-backed and SQLite-backed behavior, workspace integrity, export/import equivalence, freshness consistency, archive recoverability, logical path resolution, and domain-specific corruption without mutating canonical state by default.

## 4. Non-Goals

- Do not introduce new persistence domains or architecture.
- Do not repair or re-export state by default.
- Do not treat documentation/report generation as completion; verification output is executable diagnostic behavior.
- Do not relax domain invariants to make verification pass.

## 5. Runtime / System State Before

- All migrated domains, workspace sync, and transaction recovery exist.
- Tests cover domain behavior, but no unified user/test verification mode proves workspace consistency end to end.
- Stale exports, unresolved logical paths, nondeterministic serialization, and archive recoverability may require separate checks.

## 6. Runtime / System State After

- Verification can run against SQLite-canonical workspaces with fresh exports.
- Verification can run against legacy filesystem workspaces after import.
- Verification detects stale exports, missing exported files, unresolved logical paths, nondeterministic export/import behavior, unrecoverable archives, and domain corruption.
- Verification is read-only by default.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| Storage conformance execution | Verification service | Compare file-backed and SQLite-backed domain behavior for supported workflows. | Workspace state, domain stores, conformance fixtures. | Pass/fail conformance result. | Milestone 1 conformance contracts and all SQLite stores. | Valid SQLite-canonical workspace with fresh exports verifies successfully. | Regression tests, rollout checks. |
| Workspace integrity verification | Verification service | Run database integrity, cross-domain integrity, logical path, freshness, and archive checks. | Database, exports, retained files, sync metadata. | Verification result with diagnostics. | Milestones 11 and 12 validators. | Detects stale exports, missing files, unresolved paths, archive failures, and corruption. | Users, CI, migration rollout. |
| Export/import equivalence verification | Verification service | Check deterministic round-trip behavior without mutating canonical state. | Canonical database and temporary export/import target. | Equivalence/stability result. | Workspace sync service. | Detects nondeterministic export/import behavior. | CI, compatibility checks. |
| Read-only verification mode | CLI/API surface | Expose verification without mutating canonical state unless explicit repair/re-export is requested. | Verification options and workspace root. | Exit code/result diagnostics. | Verification service. | Verification can run without mutating canonical state. | CLI users, automated checks. |

## 8. Architectural Responsibilities

- Verification service owns orchestration of read-only checks and result aggregation.
- Domain validators own domain-specific corruption detection.
- Workspace sync owns temporary export/import equivalence mechanics.
- CLI/API surface owns exit status and human-readable diagnostics derived from executable checks.
- Repair/re-export, if exposed, must be explicit and separate from default verification.

**Cross-Milestone Constraints**
- SQLite becomes canonical only for machine-managed migrated domains.
- Retained markdown prompt/workflow artifacts remain filesystem-backed.
- Filesystem exports are deterministic, importable serializations of migrated domain state.
- Repo-relative path strings remain durable logical identities.
- `DNNNN`, `NNNN`, family IDs, runtime prompt names, and correlation IDs preserve imported identity.
- Behavior moves behind semantic domain operations before callers stop using filesystem representations.
- Freshness semantics remain trustworthy across retained files and migrated records.

## 9. Components and Modules

**Verification service**
- Purpose: Run complete storage compatibility checks.
- Responsibilities: Coordinate conformance, integrity, freshness, sync, archive, and path resolution checks.
- Owned State: Transient verification result.
- Consumed State: Database, exports, retained files, validators.
- Public Contracts: Verify workspace operation.
- Internal Contracts: Read-only by default.
- Dependencies: All prior milestone services.
- Tests Required: Valid and invalid workspace verification tests.
**Conformance runner**
- Purpose: Exercise supported workflows across storage modes.
- Responsibilities: Run domain behavior comparisons using established fixtures.
- Owned State: Temporary workspaces.
- Consumed State: File-backed and SQLite-backed stores.
- Public Contracts: Conformance result.
- Internal Contracts: Does not mutate source workspace.
- Dependencies: Milestone 1 conformance tests.
- Tests Required: Storage-mode equivalence tests.
**Verification CLI/API adapter**
- Purpose: Expose executable verification.
- Responsibilities: Parse options, call service, emit diagnostics/exit result, preserve read-only default.
- Owned State: No persistent state.
- Consumed State: Verification results.
- Public Contracts: Command/API behavior if exposed.
- Internal Contracts: Repair/re-export requires explicit option.
- Dependencies: CLI composition.
- Tests Required: Console/exit behavior tests.

## 10. Repository and File Impact

- `src/LoopRelay.Roadmap.Cli/Services/Artifacts` and `RoadmapArtifactPaths` remain the source of existing roadmap artifact path constants until a shared path layer replaces them.
- `src/LoopRelay.Roadmap.Cli/Services/State`, `TransitionState`, `TransitionCoordination`, `Decisions`, `ExecutionPreparation`, `Projections`, `Splits`, and `ArtifactManagement` contain the current roadmap domain stores.
- `src/LoopRelay.Cli/Services/Execution/LoopArtifacts.cs` owns live decision, handoff, and operational delta loop-file behavior today.
- `src/LoopRelay.Completion/Services/ArtifactStorage` owns completed-epic archive and evidence access used by completion workflows.
- `src/LoopRelay.Infrastructure/Services/Artifacts` contains repository-relative path bridging and numbered artifact sequence helpers.
- Add verification service in persistence/verification area and wire into relevant CLI composition if a command is exposed.
- Reuse domain validators, workspace sync service, transaction recovery checks, and logical resolver.
- Tests add valid/invalid workspace fixture suites and read-only mutation guards.

## 11. Public Contracts

- Verification operation/command with read-only default.
- Verification result categories: success, stale export, missing export, unresolved path, nondeterministic sync, unrecoverable archive, corrupt domain, unsupported version, mutation-required.
- Exit status or API result maps deterministically to verification outcome.
- Optional repair/re-export mode, if implemented, is explicit and not default.

## 12. Internal Contracts

- Verification never mutates canonical database or export tree by default.
- Temporary export/import checks use isolated temp workspaces.
- Each finding includes domain, identity/path, rule, severity, and suggested executable recovery path where known.
- Verification composes existing validators rather than duplicating domain logic.

## 13. Data and State Model

**Verification result**
- Owner: Verification service
- Lifecycle: Generated per verification run.
- Durability: Transient unless caller captures output.
- Mutability: Immutable after run.
- Identity: Verification run ID/timestamp if available.
- Validation: Findings derive from executable checks.
- Recovery: Rerun after state changes or explicit repair.
- Consumers: CLI/CI/users.
**Temporary round-trip workspace**
- Owner: Verification service
- Lifecycle: Created for export/import equivalence and removed after run.
- Durability: Temporary filesystem/database.
- Mutability: Mutated only in temp scope.
- Identity: Verification run scope.
- Validation: Compared to source canonical state.
- Recovery: Discard and rerun.
- Consumers: Verification service only.

## 14. Lifecycle and State Transitions

- Verify: Open workspace read-only -> validate database/schema -> validate sync/export freshness -> resolve references -> run freshness/archive checks -> run temp round-trip -> aggregate result.
- Failure finding: Check detects issue -> record domain/path/rule/severity -> continue where safe -> return non-success result.
- Optional repair/re-export: Only when explicitly requested -> invoke sync/recovery behavior -> rerun checks if required.

## 15. Execution Flow

- Startup flow validates command/options and opens workspace without mutation.
- Normal verification runs checks in deterministic order and uses temp workspace for round-trip tests.
- Failure flow aggregates actionable diagnostics and exits/returns failure category.
- Recovery flow is external or explicit repair; default verify does not mutate.
- Shutdown cleans temporary verification resources.

## 16. Dependency Closure

- Hard prerequisite: Milestones 1-12.
- Inherited capability: domain conformance tests, workspace sync, logical resolver, archive recovery, transaction integrity validation.
- Supporting infrastructure: CLI composition and temporary workspace creation.
- Future dependency: none in this roadmap.

## 17. Failure Modes

**Stale export detected**
- Description: Filesystem export does not match canonical database.
- Detection: Sync metadata/hash check.
- Behavior: Verification fails with stale export finding.
- Recovery: Run explicit export/reconcile.
- Diagnostics: Domain and changed paths.
- Tests: Stale export fixture.
**Missing exported file**
- Description: Required migrated record lacks filesystem export.
- Detection: Export completeness check.
- Behavior: Verification fails unless domain scope marks export optional.
- Recovery: Regenerate export.
- Diagnostics: Domain, logical path, expected export path.
- Tests: Missing export fixture.
**Unresolved logical path**
- Description: State/journal/provenance/lifecycle/split/evidence reference cannot resolve.
- Detection: Logical resolver checks.
- Behavior: Verification fails with unresolved path.
- Recovery: Restore/import referenced artifact or repair referencing state.
- Diagnostics: Referring domain and path.
- Tests: Unresolved path fixture.
**Nondeterministic round trip**
- Description: Export/import/export or database equivalence changes unexpectedly.
- Detection: Temporary round-trip comparison.
- Behavior: Verification fails with domain/file differences.
- Recovery: Fix serializer/domain equality.
- Diagnostics: Domain and diff summary.
- Tests: Intentional nondeterminism fixture.
**Archive unrecoverable**
- Description: Completed epic archive cannot recover associated migrated records.
- Detection: Archive recovery check.
- Behavior: Verification fails.
- Recovery: Restore archive materialization or database records.
- Diagnostics: Archive ID and missing records.
- Tests: Broken archive fixture.

## 18. Validation and Invariants

**Verification succeeds for valid SQLite-canonical workspace with fresh exports.**
- Source Authority: Milestone 13 acceptance criteria.
- Enforcement Point: Valid fixture tests.
- Failure Behavior: False failure blocks exit.
- Test Strategy: Golden valid workspace verification.
**Verification can run without mutating canonical state.**
- Source Authority: Milestone 13 acceptance criteria.
- Enforcement Point: Mutation guard tests comparing pre/post database and export hashes.
- Failure Behavior: Verification mutates state.
- Test Strategy: Read-only verification tests.
**Verification detects specified stale/missing/unresolved/nondeterministic/archive failures.**
- Source Authority: Milestone 13 acceptance criteria.
- Enforcement Point: Invalid fixture suite.
- Failure Behavior: False success blocks exit.
- Test Strategy: One fixture per required detection class.

## 19. Testing Strategy

- Unit tests for verification result aggregation and rule categories.
- Integration tests for valid SQLite-canonical workspace and legacy filesystem workspace after import.
- Failure fixture tests for stale exports, missing exports, unresolved paths, nondeterministic round trips, unrecoverable archives, corrupt domains, and unsupported versions.
- Read-only mutation guard tests.
- CLI/API behavior tests for exit/result categories and diagnostics.
- Performance smoke tests over large migrated workspace.

## 20. Fixtures and Test Data

- Valid SQLite-canonical workspace with fresh exports.
- Valid legacy filesystem workspace that imports cleanly.
- Stale export workspace.
- Missing exported file workspace.
- Unresolved logical path workspace.
- Nondeterministic serializer fixture.
- Broken completed epic archive fixture.
- Unsupported schema/export version fixture.

## 21. Acceptance Demonstration

- Run verification on a valid SQLite-canonical workspace with fresh exports and expect success.
- Run verification on a legacy filesystem workspace after import and expect success.
- Modify one exported file without updating SQLite and verify stale export failure.
- Delete one required exported migrated file and verify missing export failure.
- Break one journal/evidence reference and verify unresolved path failure.
- Confirm pre/post hashes of canonical database and export tree are unchanged by default verification.

## 22. Certification Evidence

- Verification success transcript for valid SQLite workspace.
- Verification success transcript for imported legacy workspace.
- Failure transcripts for every required detection class.
- Read-only mutation guard output.
- Round-trip equivalence output from temp workspace.

## 23. Implementation Plan

**Compose verification checks**
- Purpose: Reuse existing domain validators and sync/integrity services.
- Deliverables: Verification service and rule registry.
- Dependencies: Milestones 11 and 12 validators.
- Completion: Valid fixture verifies.
**Implement round-trip verification**
- Purpose: Detect nondeterministic export/import behavior.
- Deliverables: Temp workspace export/import/equality check.
- Dependencies: Workspace sync service.
- Completion: Round-trip fixtures pass/fail correctly.
**Expose read-only operation**
- Purpose: Make verification executable by users/tests.
- Deliverables: CLI/API adapter and result mapping if exposed.
- Dependencies: Verification service.
- Completion: Read-only mutation guard passes.
**Build invalid fixture suite**
- Purpose: Ensure required failures are detected.
- Deliverables: Failure fixtures and tests.
- Dependencies: Verification rules.
- Completion: Each required detection class fails deterministically.

## 24. Parallel Work Opportunities

**Verification service**
- Owner: Persistence/verification engineer
- Dependencies: Validators available.
- Sync: Result category vocabulary.
- Risk: Duplicating domain logic.
**CLI/API adapter**
- Owner: CLI engineer
- Dependencies: Service result model.
- Sync: Exit/result mapping.
- Risk: Command mutates state by default.
**Failure fixtures**
- Owner: Test engineer
- Dependencies: Rule categories.
- Sync: Expected diagnostics.
- Risk: Fixtures become brittle to harmless serialization differences.

## 25. Risks and Mitigations

**Verification duplicates implementation logic**
- Class: maintainability
- Impact: False confidence or drift.
- Likelihood: medium
- Detection: Code review and conformance mismatch.
- Mitigation: Compose domain validators and sync services instead of re-parsing domain rules.
- Fallback: Move duplicated checks into domain validators.
**Read-only mode accidentally mutates state**
- Class: safety
- Impact: Verification changes canonical/export state.
- Likelihood: low
- Detection: Mutation guard tests.
- Mitigation: Use read-only database connections and temp directories.
- Fallback: Disable repair/re-export unless explicit option is passed.
**Verification too slow for daily use**
- Class: performance
- Impact: Users skip checks.
- Likelihood: medium
- Detection: Large fixture smoke tests.
- Mitigation: Separate quick integrity checks from full round-trip verification if needed.
- Fallback: Provide scoped verification modes.

## 26. Observability and Diagnostics

- Verification output includes rule category, severity, domain, identity/path, expected/current state, and recommended executable recovery action.
- Metrics or timing summaries include check durations and record/file counts.
- Health check mode can report database/export freshness without full round-trip when requested.

## 27. Performance and Scalability Considerations

- Baseline full verification may be slower than normal workflow but should be practical for CI or explicit checks.
- Likely bottlenecks are full export/import round trip, evidence bodies, and journal scans.
- Measure per-check timing and allow scoped verification if needed.
- Deferred optimization: incremental verification using domain version hashes.

## 28. Security and Safety Considerations

- Open database read-only by default.
- Run temp export/import in isolated temporary directories.
- Avoid logging full artifact bodies by default.
- Validate imported legacy/export files before any optional repair mode.
- Do not follow paths outside workspace/export scope.

## 29. Documentation Updates

- No documentation-only deliverable is required.
- Executable command help/result text is part of the command surface if a CLI verification command is exposed.

## 30. Exit Criteria

- Verification succeeds for valid SQLite-canonical workspace with fresh exports.
- Verification succeeds for valid legacy filesystem workspace after import.
- Verification detects stale exports, missing exported files, unresolved logical paths, nondeterministic round trips, unrecoverable archives, and domain corruption.
- Verification is read-only by default and mutation guard tests pass.
- All inherited invariants from prior milestones remain valid.
- No future milestone capability is claimed; this is final roadmap verification.

## 31. Transition to Next Milestone

- This final milestone hands off a verified SQLite-canonical persistence architecture with deterministic filesystem interoperability, archive recoverability, transaction-aware recovery, and regression protection.
- Stable capabilities include migrated domain stores, sync, verification, logical path resolution, and read-only consistency diagnostics.
- Remaining limitations are only those explicitly left unresolved by roadmap open questions or future roadmap changes.

<!-- GENERATED:END -->

<!-- MANUAL:START -->
<!-- Add manually maintained notes here. This block is preserved by the generator. -->
<!-- MANUAL:END -->
