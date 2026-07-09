# Milestone 11 — Bidirectional Workspace Synchronization Deep Dive

<!-- GENERATED:START -->
Generated from `.agents/specs/roadmap.md` and `.agents/specs/audit.md`.

## 1. Milestone Summary

- Milestone Identifier: Milestone 11
- Milestone Name: Bidirectional Workspace Synchronization
- Short Description: Provide workspace-level synchronization between canonical SQLite state and filesystem export state, including full/scoped import/export and conflict detection.
- Implementation Role: Turns per-domain import/export into an intentional workspace interoperability capability.
- Roadmap Position: Eleventh milestone; follows migration of all requested domains and archive compatibility.
- Primary Outcomes:
- Full workspace export regenerates all migrated filesystem equivalents.
- Full workspace import from generated exports produces an equivalent SQLite database.
- Selective domain sync and stale/conflict detection prevent accidental overwrites.

## 2. Normative Basis

**Roadmap Authority**
- Roadmap authority: `.agents/specs/roadmap.md` `# Project Goal`, `# Guiding Principles`, and this milestone's `## Milestone` section.
- `.agents/specs/roadmap.md` `## Milestone 11 — Bidirectional Workspace Synchronization`.
**Architectural Authority**
- Architectural authority: `.agents/specs/audit.md` sections `2. Persistence Authority`, `4. Persistence Semantics`, `5. Filesystem-Coupled Behaviors`, `8. Cross-Boundary Dependencies`, `9. Compatibility and Migration Constraints`, `10. Round-Trip Import/Export Requirements`, `14. Persistence Domain Inventory`, `16. Persistence Dependency Graph`, `17. Synchronization Model`, `19. Persistence Platform Capabilities`, and `20. Roadmap-Relevant Findings`.
**Implementation Authority**
- Implementation authority: existing LoopRelay persistence code under `src/LoopRelay.Roadmap.Cli/Services`, `src/LoopRelay.Cli/Services/Execution`, `src/LoopRelay.Completion/Services/ArtifactStorage`, `src/LoopRelay.Projections/Services`, `src/LoopRelay.Core/Abstractions/Artifacts/IArtifactStore.cs`, and `src/LoopRelay.Infrastructure/Services/Artifacts`.
**Supporting Context**
- Supporting context: current tests under `tests/LoopRelay.Roadmap.Cli.Tests`, `tests/LoopRelay.Cli.Tests`, `tests/LoopRelay.Completion.Tests`, `tests/LoopRelay.Projections.Tests`, and `tests/LoopRelay.Infrastructure.Tests`.
- No undocumented authority is introduced here; unresolved audit mismatches remain open questions rather than implementation permission.

## 3. Objective

Implement workspace-level full and domain-scoped import/export between SQLite-canonical state and filesystem-equivalent exports, with conflict detection, freshness markers or equivalent validation, mixed-version safeguards, and `.agents` submodule publish integration for exported state.

## 4. Non-Goals

- Do not change domain semantics established by prior milestones.
- Do not implement workflow-level transaction recovery; Milestone 12 owns that.
- Do not allow stale filesystem exports to overwrite newer canonical database state silently.
- Do not require retained human-facing files to be database-managed.

## 5. Runtime / System State Before

- Each migrated domain supports SQLite canonical storage and export/import.
- Archive compatibility exists for DB-backed historical state.
- No coherent workspace-level command/capability coordinates full/scoped sync or conflict detection.
- Stale exports can exist beside canonical database state without a unified reconciliation policy.

## 6. Runtime / System State After

- Full export regenerates all migrated filesystem equivalents.
- Full import from generated exports creates equivalent database state.
- Export -> import -> export is stable according to domain rules.
- Selective domain import/export works without corrupting unrelated domains.
- Conflicting/stale exports are detected before overwriting canonical state.
- Submodule publishing can publish intended filesystem export surface.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| Full workspace export | Workspace sync service | Regenerate filesystem equivalents for all migrated domains. | Canonical SQLite state and retained filesystem artifacts where referenced. | Complete export tree under `.agents`. | All prior domain exporters. | Full export regenerates all filesystem equivalents. | Git review, backup, legacy tools, submodule publishing. |
| Full workspace import | Workspace sync service | Load filesystem export state into SQLite database. | Exported `.agents` tree and retained files. | Equivalent SQLite database. | Milestone 4 importer and later domain importers. | Full import from generated exports produces equivalent state. | Migration, restore, verification. |
| Selective domain sync | Workspace sync service | Import/export individual persistence domains safely. | Domain selector, source/target state. | Scoped sync result. | Domain-scoped serializers. | Unrelated domains are not corrupted. | Repair, targeted Git publishing, tests. |
| Conflict and freshness detection | Sync validator | Detect stale/conflicting exports versus canonical database. | Export markers or computed hashes, database state, filesystem files. | Fresh, stale, conflict, or reconciliation-required result. | Logical hash service and export metadata. | Stale/conflicting filesystem exports fail safely or require explicit reconciliation. | CLI sync commands, publisher, verification. |
| Submodule publish integration | Agents submodule publisher integration | Publish intended filesystem export surface. | Fresh export tree and publish request. | Committed/pushed export state through existing publisher behavior. | Existing `AgentsSubmodulePublisher`. | Submodule publishing can publish intended export surface. | Plan/loop/roadmap workflows and Git review. |

## 8. Architectural Responsibilities

- Workspace sync service owns orchestration of domain import/export and conflict detection.
- Domain stores/serializers own domain-specific equality and canonical serialization.
- Publisher integration owns Git/submodule publication of exported state, not database canonical truth.
- Retained filesystem artifacts remain outside migrated domain sync except as referenced inputs.

**Cross-Milestone Constraints**
- SQLite becomes canonical only for machine-managed migrated domains.
- Retained markdown prompt/workflow artifacts remain filesystem-backed.
- Filesystem exports are deterministic, importable serializations of migrated domain state.
- Repo-relative path strings remain durable logical identities.
- `DNNNN`, `NNNN`, family IDs, runtime prompt names, and correlation IDs preserve imported identity.
- Behavior moves behind semantic domain operations before callers stop using filesystem representations.
- Freshness semantics remain trustworthy across retained files and migrated records.

## 9. Components and Modules

**Workspace sync service**
- Purpose: Coordinate full and domain-scoped import/export.
- Responsibilities: Invoke domain serializers, compare state, enforce scope boundaries, report results.
- Owned State: Sync metadata/freshness markers if required.
- Consumed State: SQLite stores and filesystem export tree.
- Public Contracts: Full export/import and domain-scoped sync APIs/commands.
- Internal Contracts: No overwrite of newer canonical state without explicit reconciliation.
- Dependencies: All migrated domain serializers/stores.
- Tests Required: Full/scoped sync tests.
**Conflict detector**
- Purpose: Classify export freshness and conflicting changes.
- Responsibilities: Compare canonical state, export markers/hashes, and filesystem files.
- Owned State: Freshness marker records if selected.
- Consumed State: Logical hashes and sync metadata.
- Public Contracts: Conflict/freshness result model.
- Internal Contracts: Deterministic and domain-specific conflict categories.
- Dependencies: Logical resolver and export metadata.
- Tests Required: Stale/conflict fixtures.
**Publish adapter**
- Purpose: Publish regenerated filesystem export through existing `.agents` submodule flow.
- Responsibilities: Ensure fresh export before publish and invoke publisher integration.
- Owned State: No domain state.
- Consumed State: Export tree and Git workspace state.
- Public Contracts: Publish exported state action where existing workflows require it.
- Internal Contracts: Publisher never treats stale exports as fresh.
- Dependencies: `AgentsSubmodulePublisher`.
- Tests Required: Publish integration tests with fake publisher.

## 10. Repository and File Impact

- `src/LoopRelay.Roadmap.Cli/Services/Artifacts` and `RoadmapArtifactPaths` remain the source of existing roadmap artifact path constants until a shared path layer replaces them.
- `src/LoopRelay.Roadmap.Cli/Services/State`, `TransitionState`, `TransitionCoordination`, `Decisions`, `ExecutionPreparation`, `Projections`, `Splits`, and `ArtifactManagement` contain the current roadmap domain stores.
- `src/LoopRelay.Cli/Services/Execution/LoopArtifacts.cs` owns live decision, handoff, and operational delta loop-file behavior today.
- `src/LoopRelay.Completion/Services/ArtifactStorage` owns completed-epic archive and evidence access used by completion workflows.
- `src/LoopRelay.Infrastructure/Services/Artifacts` contains repository-relative path bridging and numbered artifact sequence helpers.
- Add workspace synchronization service near SQLite workspace store or roadmap persistence composition.
- Integrate with existing CLI composition and `AgentsSubmodulePublisher` where workflow publication currently occurs.
- Tests add full/scoped sync fixtures and fake publisher integration.

## 11. Public Contracts

- Full import and full export command/API behavior.
- Domain-scoped import/export selectors.
- Sync result categories: exported, imported, unchanged, stale export, conflict, unsupported mixed version, validation failure.
- Publisher integration requires fresh export before publishing.

## 12. Internal Contracts

- Full export is generated from canonical SQLite state for migrated domains.
- Full import validates filesystem export before mutating canonical database state.
- Scoped sync operates only on selected domains and verifies cross-domain references where needed.
- Conflict detection runs before overwrite in either direction.

## 13. Data and State Model

**Workspace sync metadata**
- Owner: Workspace sync service
- Lifecycle: Created/updated on import/export if freshness markers are used.
- Durability: SQLite and/or filesystem export marker.
- Mutability: Updated per sync.
- Identity: Workspace and export generation identity.
- Validation: Compare against canonical state hash/version.
- Recovery: Regenerate export or re-import after reconciliation.
- Consumers: Conflict detector, publisher, verification.
**Filesystem export surface**
- Owner: Workspace sync service
- Lifecycle: Regenerated from canonical database; imported into database.
- Durability: Filesystem/submodule.
- Mutability: Mutable by explicit export or external edits.
- Identity: Repo-relative paths and sync marker.
- Validation: Domain import validation and conflict checks.
- Recovery: Regenerate from SQLite or reconcile external edits.
- Consumers: Git, legacy tools, backup/restore.
**Canonical SQLite workspace state**
- Owner: Domain stores
- Lifecycle: Mutated by workflows and import sync.
- Durability: SQLite database.
- Mutability: Mutable through domain stores/sync.
- Identity: Workspace database identity and domain keys.
- Validation: Integrity and sync equality checks.
- Recovery: Import from valid export/backup.
- Consumers: All workflows.

## 14. Lifecycle and State Transitions

- Export: Validate database -> check target export freshness/conflicts -> render domain exports -> write marker/result -> optionally publish.
- Import: Read export -> validate domain snapshots -> detect conflicts with database -> apply scoped/full import in transaction -> update sync metadata.
- Conflict: Detected stale/export drift -> block automatic overwrite -> require explicit reconciliation path.

## 15. Execution Flow

- Startup/sync command validates database and export marker state.
- Normal full export regenerates all migrated domain files and leaves retained files untouched.
- Normal full import creates/equates SQLite state from generated exports.
- Failure flow reports stale/conflicting exports before destructive writes.
- Publish flow exports, validates freshness, then uses submodule publisher integration.

## 16. Dependency Closure

- Hard prerequisite: Milestones 5-10 migrated domains and archive compatibility.
- Hard prerequisite: Milestone 2 serialization and Milestone 3 logical hashing.
- Supporting infrastructure: SQLite workspace store, domain stores, `AgentsSubmodulePublisher`.
- Future dependency: Milestone 13 verification mode.
- Enables Milestones 12 and 13.

## 17. Failure Modes

**Stale export overwrite attempt**
- Description: Filesystem export is older than canonical database state.
- Detection: Sync metadata/hash comparison.
- Behavior: Block import/export overwrite or require explicit reconciliation.
- Recovery: Regenerate export or reconcile changes.
- Diagnostics: Domain, export marker, database marker/hash.
- Tests: Stale export fixture.
**External filesystem edit conflict**
- Description: Exported files changed since last export and database also changed.
- Detection: Conflict detector compares markers and current hashes.
- Behavior: Fail safely; no automatic overwrite.
- Recovery: Explicit domain import, export overwrite, or manual reconciliation command.
- Diagnostics: Changed paths and domains.
- Tests: Divergent edit fixture.
**Scoped sync breaks cross-domain reference**
- Description: Import/export selected domain leaves references unresolved.
- Detection: Reference validation after scoped operation.
- Behavior: Fail scoped sync or mark requiring dependent domain sync.
- Recovery: Include required dependent domains or repair references.
- Diagnostics: Domain and unresolved logical path.
- Tests: Scoped evidence/state reference fixture.

## 18. Validation and Invariants

**Full export/import/export is stable according to each domain's canonical serialization rules.**
- Source Authority: Milestone 11 acceptance criteria.
- Enforcement Point: Workspace round-trip tests.
- Failure Behavior: Stability mismatch blocks exit.
- Test Strategy: Full workspace golden/equality tests.
**Selective sync does not corrupt unrelated domains.**
- Source Authority: Milestone 11 acceptance criteria.
- Enforcement Point: Scoped sync tests and pre/post domain snapshots.
- Failure Behavior: Unselected domain changes unexpectedly.
- Test Strategy: Snapshot all domains before/after scoped sync.
**Stale/conflicting exports are not silently trusted.**
- Source Authority: Milestone 11 acceptance criteria.
- Enforcement Point: Conflict detector.
- Failure Behavior: Overwrite without reconciliation.
- Test Strategy: Divergent database/export fixtures.

## 19. Testing Strategy

- Full workspace export/import/export tests.
- Full import from older filesystem-only state tests.
- Selective domain import/export tests with unaffected-domain assertions.
- Conflict detection tests for stale exports, external edits, mixed-version workspaces, and scoped reference breakage.
- Publisher integration tests with a fake `.agents` submodule publisher.
- Regression tests for all domain serializers under workspace orchestration.
- Performance smoke tests for large full export/import.

## 20. Fixtures and Test Data

- Complete SQLite-canonical workspace.
- Generated export tree from prior milestones.
- Older filesystem-only workspace.
- Stale export marker fixture.
- Divergent database and export edit fixture.
- Scoped domain fixtures with cross-domain references.
- Mixed-version schema/export fixture.

## 21. Acceptance Demonstration

- Start from a SQLite-canonical workspace with all migrated domains populated.
- Run full export and verify expected filesystem equivalents exist.
- Import that export into a clean database and compare workspace equality.
- Modify both database state and exported file state to create a conflict and verify sync blocks overwrite.
- Run publish integration against fresh export using fake or real approved publisher path as appropriate.

## 22. Certification Evidence

- Full workspace export/import/export stability report.
- Scoped sync before/after domain snapshot report.
- Conflict detection diagnostic output.
- Publisher integration test output.
- Older filesystem-only import test output.

## 23. Implementation Plan

**Implement workspace sync orchestration**
- Purpose: Coordinate domain import/export.
- Deliverables: Full and scoped sync service.
- Dependencies: All domain serializers.
- Completion: Full export/import tests pass.
**Implement conflict detection**
- Purpose: Prevent stale or conflicting overwrites.
- Deliverables: Sync metadata/hash comparison and result categories.
- Dependencies: Logical hashes and export markers.
- Completion: Conflict fixtures fail safely.
**Integrate selective sync**
- Purpose: Support domain-scoped repair/publish flows.
- Deliverables: Domain selector and scoped validation.
- Dependencies: Sync service.
- Completion: Unrelated domains unchanged in tests.
**Integrate publisher**
- Purpose: Publish intended export surface.
- Deliverables: Fresh-export preflight and publisher adapter.
- Dependencies: Sync service and existing publisher.
- Completion: Publisher integration tests pass.

## 24. Parallel Work Opportunities

**Sync orchestration**
- Owner: Persistence engineer
- Dependencies: Domain serializers complete.
- Sync: Result categories and transaction boundaries.
- Risk: Domain ordering introduces hidden dependencies.
**Conflict detection**
- Owner: Freshness/verification engineer
- Dependencies: Hash/marker policy.
- Sync: Workspace metadata shape.
- Risk: False conflicts block normal use.
**Publisher integration**
- Owner: CLI/infrastructure engineer
- Dependencies: Fresh export command/API.
- Sync: Submodule publish lifecycle.
- Risk: Publisher commits stale files.

## 25. Risks and Mitigations

**Conflict model too weak**
- Class: data
- Impact: New database state overwritten by stale export.
- Likelihood: medium
- Detection: Divergent edit tests.
- Mitigation: Use explicit freshness markers and content hashes before overwrite.
- Fallback: Require explicit force/reconcile command for all imports over existing DB state.
**Scoped sync violates dependencies**
- Class: integration
- Impact: Unresolved cross-domain references.
- Likelihood: medium
- Detection: Reference validation tests.
- Mitigation: Classify dependency closure for scoped domains and fail unsafe scopes.
- Fallback: Require full sync for dependent domains.
**Mixed-version workspace mishandled**
- Class: operational
- Impact: Older/newer CLIs corrupt state.
- Likelihood: medium
- Detection: Mixed-version fixtures.
- Mitigation: Detect unsupported schema/export versions and fail safely.
- Fallback: Compatibility export for older tools only.

## 26. Observability and Diagnostics

- Sync reports include operation type, domain scope, changed file count, changed row count, conflict list, and marker/hash values.
- Publisher diagnostics include export freshness status before publish.
- Health checks can report database/export freshness without mutating state.

## 27. Performance and Scalability Considerations

- Full sync scales with all migrated records and export file count.
- Likely bottlenecks are evidence export/import and journal export/import.
- Measure full sync duration, bytes read/written, and domain timings.
- Deferred optimization: incremental sync based on domain version hashes.

## 28. Security and Safety Considerations

- Block path traversal during import/export.
- Do not overwrite filesystem exports or database rows until conflict detection passes.
- Validate submodule publish target and avoid publishing private database internals unless explicitly intended.
- Treat filesystem imports as untrusted until validation succeeds.

## 29. Documentation Updates

- No documentation-only deliverable is required.
- User-visible sync/publish command help must accurately describe conflict and stale-export behavior if commands are exposed.

## 30. Exit Criteria

- Full workspace export/import works and is stable.
- Selective domain sync works safely.
- Stale/conflicting exports are detected before overwrite.
- Submodule publishing can publish fresh export surface.
- Older filesystem-only state imports without losing legacy-supported data.
- No workflow-transaction or verification-mode claim is made beyond sync validation.

## 31. Transition to Next Milestone

- Milestone 12 receives a coherent database/export state model for transactional workflow recovery.
- Milestone 13 receives sync metadata and equivalence checks for verification mode.
- Remaining limitation: workflow transitions may still update related domains non-atomically.

## Open Implementation Questions

- The exact conflict resolution commands are not specified; this milestone must at least detect conflicts and require explicit reconciliation before overwrite.
- Whether the SQLite database file itself is published is outside this milestone unless the roadmap clarifies it; the required publish surface is filesystem export state.

<!-- GENERATED:END -->

<!-- MANUAL:START -->
<!-- Add manually maintained notes here. This block is preserved by the generator. -->
<!-- MANUAL:END -->
