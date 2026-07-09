# Milestone 1 — File-Backed Domain Persistence Surface Deep Dive

<!-- GENERATED:START -->
Generated from `.agents/specs/roadmap.md` and `.agents/specs/audit.md`.

## 1. Milestone Summary

- Milestone Identifier: Milestone 1
- Milestone Name: File-Backed Domain Persistence Surface
- Short Description: Introduce semantic persistence operations for all migrated domains while preserving the current filesystem backing store.
- Implementation Role: Creates the executable domain boundary that later storage engines must implement.
- Roadmap Position: First milestone; no SQLite canonical state is introduced yet.
- Primary Outcomes:
- Workflow code can use domain operations instead of direct globs for migrated persistence behavior.
- Current file-backed behavior remains externally unchanged.
- Conformance tests define parity between existing filesystem behavior and the new domain surface.

## 2. Normative Basis

**Roadmap Authority**
- Roadmap authority: `.agents/specs/roadmap.md` `# Project Goal`, `# Guiding Principles`, and this milestone's `## Milestone` section.
- `.agents/specs/roadmap.md` `## Milestone 1 — File-Backed Domain Persistence Surface`.
**Architectural Authority**
- Architectural authority: `.agents/specs/audit.md` sections `2. Persistence Authority`, `4. Persistence Semantics`, `5. Filesystem-Coupled Behaviors`, `8. Cross-Boundary Dependencies`, `9. Compatibility and Migration Constraints`, `10. Round-Trip Import/Export Requirements`, `14. Persistence Domain Inventory`, `16. Persistence Dependency Graph`, `17. Synchronization Model`, `19. Persistence Platform Capabilities`, and `20. Roadmap-Relevant Findings`.
**Implementation Authority**
- Implementation authority: existing LoopRelay persistence code under `src/LoopRelay.Roadmap.Cli/Services`, `src/LoopRelay.Cli/Services/Execution`, `src/LoopRelay.Completion/Services/ArtifactStorage`, `src/LoopRelay.Projections/Services`, `src/LoopRelay.Core/Abstractions/Artifacts/IArtifactStore.cs`, and `src/LoopRelay.Infrastructure/Services/Artifacts`.
**Supporting Context**
- Supporting context: current tests under `tests/LoopRelay.Roadmap.Cli.Tests`, `tests/LoopRelay.Cli.Tests`, `tests/LoopRelay.Completion.Tests`, `tests/LoopRelay.Projections.Tests`, and `tests/LoopRelay.Infrastructure.Tests`.
- No undocumented authority is introduced here; unresolved audit mismatches remain open questions rather than implementation permission.

## 3. Objective

Implement file-backed semantic persistence stores for the migrated artifact categories so current read, write, append, rotate, upsert, list, and latest/fallback behavior is available without callers directly depending on backing-directory globs.

## 4. Non-Goals

- Do not create SQLite schema, database files, import commands, or export commands.
- Do not change canonical storage; files remain authoritative in this milestone.
- Do not migrate live markdown files such as `.agents/decisions/decisions.md`, `.agents/handoffs/handoff.md`, or `.agents/operational_delta.md`.
- Do not alter legacy markdown import semantics for stores that already support them.
- Do not normalize malformed provenance behavior; execution and selection provenance still load empty where that is today's contract.

## 5. Runtime / System State Before

- Persistence semantics are embedded in `LoopArtifacts`, `RoadmapArtifacts`, `CompletionArtifacts`, structured stores, and workflow helpers.
- `IArtifactStore` exposes file-shaped operations, and callers use path constants, globs, filename parsing, and JSON serialization directly.
- Sequence allocation depends on scanning filenames for `DNNNN` or `NNNN` identity.
- Live-first decision and handoff reads are implemented by loop artifact helpers.
- SQLite, workspace synchronization, and storage conformance across engines are unavailable.

## 6. Runtime / System State After

- Each migrated domain has a semantic file-backed store that owns current behavior.
- Sequence allocation, append ordering, live/history fallback, and path identity are exposed as domain behavior.
- Workflow code no longer needs direct directory globbing for migrated domains to obtain current semantics.
- Conformance tests capture today's filesystem behavior as the baseline for later SQLite implementations.
- The filesystem remains the only canonical persistence layer.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| Decision ledger append surface | Decision domain store | Append entries and allocate `DNNNN` from existing ledger state. | Prompt/projection metadata, input paths, output paths, existing ledger. | Updated ledger document and next decision identity. | Current `DecisionLedgerStore` behavior. | IDs remain sorted, unique, and identical to current next-ID allocation. | Decision recorder, roadmap transition persistence, execution preparation freshness. |
| Loop history surface | Loop history store | Read latest, rotate live files, write numbered decisions, handoffs, and deltas. | Live markdown files and existing numbered histories. | Historical markdown paths with preserved `NNNN` allocation. | `LoopArtifacts` live/history behavior. | Live-first reads and highest-number fallback match current behavior. | Loop runner, decision session, completion archive. |
| Evidence write surface | Execution evidence store | Write numbered evidence by stem and expose path-compatible reads. | Evidence directory, stem, body, existing evidence files. | Stable `.agents/evidence/execution/{stem}.NNNN.md` path and body. | `RoadmapArtifacts.WriteNumberedEvidenceAsync` and completion evidence helpers. | Next suffix matches max existing suffix plus one and collisions remain impossible. | Execution bridge, completion certification, prompt context, unblock planner. |
| Structured snapshot/upsert surface | Roadmap structured domain stores | Roadmap state save/load, lifecycle upsert, projection/provenance manifest upsert, split lookup. | Current structured JSON and legacy markdown where supported. | Deterministic structured documents and domain snapshots. | Existing strict and non-strict structured stores. | Current serialization, ordering, legacy import, and malformed-state behavior are preserved. | State machine, projections, selection, preparation, lifecycle validation. |
| Transition journal append surface | Transition journal store | Append ordered transition events and read legacy records. | Transition event, existing JSONL, optional input snapshot. | Ordered JSONL-backed journal state. | `TransitionJournalStore` current append/read behavior. | Started/completed/failed correlation and legacy no-snapshot records remain valid. | Prompt transition runner, state machine, operational diagnostics. |

## 8. Architectural Responsibilities

- Domain stores own behavior; workflow orchestration invokes domain operations and does not reimplement path scans.
- File-backed implementations own filesystem mechanics through `IArtifactStore`; callers do not depend on direct `Directory` access.
- Domain validation remains with the domain that owns the persisted identity.
- Failure authority stays domain-specific: strict stores throw, provenance stores preserve current empty-on-malformed behavior.

**Cross-Milestone Constraints**
- SQLite becomes canonical only for machine-managed migrated domains.
- Retained markdown prompt/workflow artifacts remain filesystem-backed.
- Filesystem exports are deterministic, importable serializations of migrated domain state.
- Repo-relative path strings remain durable logical identities.
- `DNNNN`, `NNNN`, family IDs, runtime prompt names, and correlation IDs preserve imported identity.
- Behavior moves behind semantic domain operations before callers stop using filesystem representations.
- Freshness semantics remain trustworthy across retained files and migrated records.

## 9. Components and Modules

**Migrated domain contracts**
- Purpose: Define behavior required by later file-backed and SQLite-backed stores.
- Responsibilities: Append, snapshot, upsert, rotate, list, latest, sequence, and logical path operations.
- Owned State: No state; contracts only.
- Consumed State: Existing domain models and path identities.
- Public Contracts: Internal service interfaces consumed by roadmap/loop/completion code.
- Internal Contracts: Each operation returns domain results, not raw file lists.
- Dependencies: Existing model classes and artifact path constants.
- Tests Required: Contract tests with file-backed implementations.
**File-backed domain implementations**
- Purpose: Preserve current behavior behind semantic operations.
- Responsibilities: Delegate to existing stores/helpers while centralizing sequence, live fallback, and validation behavior.
- Owned State: Existing files remain canonical.
- Consumed State: Filesystem artifacts under `.agents`.
- Public Contracts: Domain operations registered in current CLI composition.
- Internal Contracts: No caller-visible storage-engine switch.
- Dependencies: `IArtifactStore`, `RepositoryArtifactStore`, current structured stores.
- Tests Required: Parity tests against current workflow fixtures.
**Conformance fixture harness**
- Purpose: Capture behavior that SQLite must later match.
- Responsibilities: Run domain operations over seeded `.agents` trees and compare paths, IDs, documents, and errors.
- Owned State: Temporary test repositories only.
- Consumed State: Current fixture trees and generated test stores.
- Public Contracts: Test-only helpers.
- Internal Contracts: Expected outputs are deterministic.
- Dependencies: Existing unit test projects.
- Tests Required: Conformance tests for all migrated domains.

## 10. Repository and File Impact

- `src/LoopRelay.Roadmap.Cli/Services/Artifacts` and `RoadmapArtifactPaths` remain the source of existing roadmap artifact path constants until a shared path layer replaces them.
- `src/LoopRelay.Roadmap.Cli/Services/State`, `TransitionState`, `TransitionCoordination`, `Decisions`, `ExecutionPreparation`, `Projections`, `Splits`, and `ArtifactManagement` contain the current roadmap domain stores.
- `src/LoopRelay.Cli/Services/Execution/LoopArtifacts.cs` owns live decision, handoff, and operational delta loop-file behavior today.
- `src/LoopRelay.Completion/Services/ArtifactStorage` owns completed-epic archive and evidence access used by completion workflows.
- `src/LoopRelay.Infrastructure/Services/Artifacts` contains repository-relative path bridging and numbered artifact sequence helpers.
- Expected additions under existing roadmap service namespaces for domain contracts and file-backed adapters.
- Tests extend current domain test directories rather than creating an unrelated test hierarchy.
- No database files, migrations, or generated exports are introduced.

## 11. Public Contracts

- No new end-user command is required.
- Internal public surface is the semantic domain store contract set used by current CLIs.
- Existing `.agents` file paths, JSON schema names, markdown paths, and JSONL shape remain externally unchanged.

## 12. Internal Contracts

- Domain operations must be idempotent where current behavior is idempotent and must throw where current collision behavior throws.
- List/latest operations return deterministic ordering independent of underlying store ordering.
- Live-first reads must check retained live files before historical stores.
- Legacy migration paths remain reachable only through domains that already support them.

## 13. Data and State Model

**Existing filesystem artifacts**
- Owner: File-backed domain implementations
- Lifecycle: Unchanged from current code.
- Durability: Filesystem and `.agents` submodule behavior.
- Mutability: Mutable or append-only according to current domain.
- Identity: Repo-relative path plus domain IDs.
- Validation: Current structured-store and filename validation.
- Recovery: Current file presence and legacy import behavior.
- Consumers: All existing workflows.
**Domain operation results**
- Owner: Calling services
- Lifecycle: Transient runtime values.
- Durability: Not persisted separately.
- Mutability: Immutable result objects.
- Identity: Carries logical path, ID, hash, or sequence as applicable.
- Validation: Produced only after domain validation.
- Recovery: Recomputed from filesystem state.
- Consumers: Workflow orchestration and tests.

## 14. Lifecycle and State Transitions

- Append: request -> load current domain state -> allocate identity if needed -> validate -> write through current filesystem store.
- Rotate: live file present -> allocate next historical identity -> write historical record -> delete/retain live file exactly as current workflow requires.
- Snapshot: collect current inputs -> validate deterministic ordering -> overwrite structured document.
- Failure mode: validation or collision stops the operation before claiming success; no SQLite rollback exists in this milestone.

## 15. Execution Flow

- Startup initializes file-backed domain services in the same CLI composition that currently wires `IArtifactStore`.
- Normal workflow calls domain methods; adapters read/write the same files current code would use.
- Failures propagate as current domain exceptions or current empty-state behavior.
- Recovery is unchanged: rerun workflows against filesystem state and legacy import behavior.
- Shutdown has no additional persistence step.

## 16. Dependency Closure

- Hard prerequisite: current filesystem-backed LoopRelay workflows and tests.
- Hard prerequisite: existing path constants and domain models.
- Supporting infrastructure: `IArtifactStore`, `RepositoryArtifactStore`, `NumberedArtifactSequence`, structured JSON stores.
- Explicitly unavailable dependency: SQLite database state and import/export engines.
- Enables Milestones 2, 4, 5, 6, 7, 8, and 9 by defining behavior each later store must match.

## 17. Failure Modes

**Sequence collision**
- Description: A target numbered history path already exists.
- Detection: Domain checks existence before write.
- Behavior: Throw the same domain/workflow error as current code.
- Recovery: Inspect existing histories and rerun after conflict is resolved.
- Diagnostics: Include logical path and attempted sequence.
- Tests: Seed conflicting `NNNN` file and assert failure.
**Malformed strict JSON**
- Description: State, ledger, lifecycle, projection, or split JSON has invalid schema.
- Detection: Structured store validation.
- Behavior: Preserve current strict failure.
- Recovery: Restore valid file or legacy import source.
- Diagnostics: Domain-specific schema/path error.
- Tests: Existing malformed JSON tests plus domain adapter coverage.
**Malformed provenance manifest**
- Description: Execution or selection provenance JSON cannot parse.
- Detection: Provenance manifest loader.
- Behavior: Load empty if that is current domain behavior.
- Recovery: Regenerate provenance through existing workflow.
- Diagnostics: No new hard failure in this milestone.
- Tests: Malformed manifest loads empty.

## 18. Validation and Invariants

**No migrated domain requires callers to glob a backing directory for current behavior.**
- Source Authority: Roadmap Milestone 1 acceptance criteria.
- Enforcement Point: Code review and conformance tests around domain services.
- Failure Behavior: Reject implementation or fail tests.
- Test Strategy: Search tests and production code for direct glob dependency in migrated domains.
**Path identity and sequence identity are preserved.**
- Source Authority: Roadmap guiding principles and audit sections 5 and 9.
- Enforcement Point: Domain allocation and import tests.
- Failure Behavior: Throw on collision or identity mismatch.
- Test Strategy: Fixture tests with existing numbered files and path references.
**Filesystem behavior remains canonical and unchanged.**
- Source Authority: Milestone 1 objective.
- Enforcement Point: Existing workflow test suite.
- Failure Behavior: Regression failure blocks milestone exit.
- Test Strategy: Run current workflow tests after adapter insertion.

## 19. Testing Strategy

- Unit tests for every domain contract operation using file-backed stores.
- Integration tests that run existing workflows after replacing direct persistence helpers with domain stores.
- Contract tests for sequence allocation, live-first fallback, JSON ordering, and legacy import behavior.
- Regression tests for malformed strict stores and malformed provenance manifests.
- Performance smoke tests proving domain wrappers do not introduce repeated large directory scans beyond current behavior.

## 20. Fixtures and Test Data

- Valid `.agents` tree with ledger, state, lifecycle, split, manifests, histories, evidence, and journal.
- Empty optional domains for execution and selection provenance.
- Existing numbered histories ending at nontrivial values such as `0003`.
- Malformed strict JSON fixtures and malformed provenance fixtures.
- Legacy markdown-only fixtures for stores that currently migrate markdown to JSON.
- Duplicate case-variant lifecycle paths and duplicate decision IDs.

## 21. Acceptance Demonstration

- Setup a temporary repository with representative `.agents` files and no SQLite database.
- Execute existing roadmap, decision, loop, evidence, split, projection, and journal operations through domain services.
- Verify generated files match the paths, IDs, ordering, and JSON/JSONL bodies produced by current code.
- Verify no migrated-domain caller needed a direct directory glob outside the domain surface.

## 22. Certification Evidence

- Passing existing test suites for roadmap, CLI loop, completion, projections, core artifact store, and infrastructure artifact helpers.
- New conformance test output for all migrated domains.
- Diff or fixture comparison proving file-backed domain output equals current filesystem output.
- Search evidence showing direct glob access for migrated behavior has moved behind domain stores where required.

## 23. Implementation Plan

**Define domain contracts**
- Purpose: Make current persistence behavior explicit.
- Deliverables: Internal interfaces/result models for migrated domains.
- Dependencies: Existing models and path constants.
- Completion: Contracts cover every Milestone 1 capability without storage-engine terminology.
**Implement file-backed adapters**
- Purpose: Preserve current behavior behind domain methods.
- Deliverables: Adapters delegating to current stores/helpers.
- Dependencies: Domain contracts.
- Completion: Adapters pass operation-level tests.
**Route workflow callers**
- Purpose: Remove direct persistence semantics from workflow code where needed.
- Deliverables: Callers use domain services for migrated behavior.
- Dependencies: Adapters registered in composition.
- Completion: No acceptance behavior regresses.
**Add conformance tests**
- Purpose: Freeze file-backed behavior for later SQLite stores.
- Deliverables: Reusable fixture harness and parity assertions.
- Dependencies: Adapters and routed callers.
- Completion: All migrated domains have baseline coverage.

## 24. Parallel Work Opportunities

**Structured JSON domain adapters**
- Owner: Roadmap persistence engineer
- Dependencies: Contracts settled.
- Sync: Shared validation and serialization conventions.
- Risk: Divergent strict/non-strict corruption behavior.
**Loop history and evidence adapters**
- Owner: CLI/completion engineer
- Dependencies: Sequence contract settled.
- Sync: Shared logical path result model.
- Risk: Live file lifecycle accidentally changes.
**Conformance fixtures**
- Owner: Test engineer
- Dependencies: Initial contract shape.
- Sync: Expected outputs reviewed against current tests.
- Risk: Fixtures encode implementation details instead of behavior.

## 25. Risks and Mitigations

**Hidden behavior remains in callers**
- Class: architectural
- Impact: SQLite stores later miss required semantics.
- Likelihood: medium
- Detection: Code search and conformance failures.
- Mitigation: Move only migrated persistence behavior behind domain contracts.
- Fallback: Keep caller helper as adapter until contract is complete.
**Malformed-state semantics are normalized accidentally**
- Class: implementation
- Impact: Compatibility regression for existing workspaces.
- Likelihood: medium
- Detection: Malformed fixture tests.
- Mitigation: Model strict and empty-on-malformed behavior explicitly per domain.
- Fallback: Restore per-domain legacy behavior.
**Wrapper layer slows file-backed workflows**
- Class: performance
- Impact: CLI latency increases.
- Likelihood: low
- Detection: Performance smoke tests.
- Mitigation: Reuse existing caches and avoid duplicate scans.
- Fallback: Cache domain reads at the same granularity as current stores.

## 26. Observability and Diagnostics

- Domain errors include domain name, logical path or identity, and operation name.
- No new metrics are required, but diagnostics should identify adapter failures separately from workflow failures.
- Debug output remains file-path compatible with current tooling.

## 27. Performance and Scalability Considerations

- Baseline expectation is no worse than current filesystem behavior.
- Likely bottlenecks are repeated list/scan operations for histories and split families.
- Measure by running existing workflow tests and focused sequence-allocation smoke tests over large fixture directories.
- Deferred optimization: persistent indexes or SQLite-backed sequence allocation.

## 28. Security and Safety Considerations

- Validate repo-relative paths before resolving through `RepositoryArtifactStore`.
- Do not widen write authority beyond paths current workflows can mutate.
- Preserve atomic single-file writes from `FileSystemArtifactStore`.
- Avoid deleting retained live files except through existing rotation/retirement operations.

## 29. Documentation Updates

- No documentation-only deliverable is required for this executable milestone.
- CLI help and schema descriptions change only if executable command or schema surfaces change; none are expected.

## 30. Exit Criteria

- All required domain contracts and file-backed implementations exist.
- All migrated-domain callers that need semantic behavior use the new domain surface.
- Existing workflows pass with file-backed persistence.
- Conformance tests cover sequence allocation, live fallback, deterministic serialization, journal append, and legacy import behavior.
- No SQLite or future-milestone capability is claimed.

## 31. Transition to Next Milestone

- Milestone 2 receives semantic domain snapshots and file-backed behavior suitable for import/export serializers.
- Milestone 4 receives a clear behavior contract for SQLite-backed stores.
- Remaining limitation: filesystem is still canonical, and multi-domain transactions remain weak.

<!-- GENERATED:END -->

<!-- MANUAL:START -->
<!-- Add manually maintained notes here. This block is preserved by the generator. -->
<!-- MANUAL:END -->
