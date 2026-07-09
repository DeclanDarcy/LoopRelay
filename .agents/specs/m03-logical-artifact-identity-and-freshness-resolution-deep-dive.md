# Milestone 3 — Logical Artifact Identity and Freshness Resolution Deep Dive

<!-- GENERATED:START -->
Generated from `.agents/specs/roadmap.md` and `.agents/specs/audit.md`.

## 1. Milestone Summary

- Milestone Identifier: Milestone 3
- Milestone Name: Logical Artifact Identity and Freshness Resolution
- Short Description: Resolve artifact identity, content, and freshness across retained filesystem artifacts and future SQLite-backed artifacts.
- Implementation Role: Provides the path-compatible content and hash layer needed before any domain becomes SQLite-canonical.
- Roadmap Position: Third milestone; follows domain behavior and filesystem serialization, precedes database canonical domains.
- Primary Outcomes:
- Repo-relative paths resolve as logical artifact identities independent of backing storage.
- Freshness checks can compare retained files and migrated records consistently.
- Missing migrated artifacts produce domain-specific stale or invalid results instead of silent path failures.

## 2. Normative Basis

**Roadmap Authority**
- Roadmap authority: `.agents/specs/roadmap.md` `# Project Goal`, `# Guiding Principles`, and this milestone's `## Milestone` section.
- `.agents/specs/roadmap.md` `## Milestone 3 — Logical Artifact Identity and Freshness Resolution`.
**Architectural Authority**
- Architectural authority: `.agents/specs/audit.md` sections `2. Persistence Authority`, `4. Persistence Semantics`, `5. Filesystem-Coupled Behaviors`, `8. Cross-Boundary Dependencies`, `9. Compatibility and Migration Constraints`, `10. Round-Trip Import/Export Requirements`, `14. Persistence Domain Inventory`, `16. Persistence Dependency Graph`, `17. Synchronization Model`, `19. Persistence Platform Capabilities`, and `20. Roadmap-Relevant Findings`.
**Implementation Authority**
- Implementation authority: existing LoopRelay persistence code under `src/LoopRelay.Roadmap.Cli/Services`, `src/LoopRelay.Cli/Services/Execution`, `src/LoopRelay.Completion/Services/ArtifactStorage`, `src/LoopRelay.Projections/Services`, `src/LoopRelay.Core/Abstractions/Artifacts/IArtifactStore.cs`, and `src/LoopRelay.Infrastructure/Services/Artifacts`.
**Supporting Context**
- Supporting context: current tests under `tests/LoopRelay.Roadmap.Cli.Tests`, `tests/LoopRelay.Cli.Tests`, `tests/LoopRelay.Completion.Tests`, `tests/LoopRelay.Projections.Tests`, and `tests/LoopRelay.Infrastructure.Tests`.
- No undocumented authority is introduced here; unresolved audit mismatches remain open questions rather than implementation permission.

## 3. Objective

Implement logical artifact resolution that can retrieve canonical content, compute stable hashes, and evaluate freshness for retained filesystem artifacts, file-backed migrated artifacts, and future SQLite-backed migrated artifacts by repo-relative path.

## 4. Non-Goals

- Do not make SQLite canonical for any domain.
- Do not replace filesystem exports or import/export behavior from Milestone 2.
- Do not move projection markdown bodies, retained prompt files, live decisions, live handoff, or live delta.
- Do not invent path identities for artifacts not present in the roadmap or audit-supported implementation.
- Do not silently fabricate content for unresolved logical paths.

## 5. Runtime / System State Before

- Freshness logic hashes serialized files through path reads.
- Structured state, journal, provenance, lifecycle, split, and decision records store repo-relative paths.
- Migrated content is still filesystem-backed, but later SQLite rows would break direct file hash assumptions.
- Missing files can appear as path failures rather than domain-specific stale/invalid states.

## 6. Runtime / System State After

- A retained filesystem artifact resolves to its current file content and hash.
- A migrated artifact resolves to equivalent logical content through its domain store.
- Historical paths remain interpretable after import.
- Freshness comparison uses canonical logical content rather than assuming a physical file exists.
- Unresolved logical paths fail with deterministic domain-specific diagnostics.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| Logical path resolver | Artifact identity service | Map repo-relative paths to retained file content, migrated domain records, or exported projection content. | Repo-relative path and domain registry. | Resolved content, hash, storage classification, or failure. | Milestone 1 domain surface and Milestone 2 serialization rules. | Retained files and migrated artifacts resolve through the same logical path contract. | Freshness evaluators, prompt builders, journal/state validators, workspace sync. |
| Canonical migrated content hash | Freshness service | Compute stable hashes for migrated records using canonical serialization. | Domain records or filesystem exports. | Hash values comparable with current file-backed mode. | Canonical export rules. | File-backed mode produces the same freshness result as before. | Execution preparation, selection provenance, projection freshness, verification. |
| Historical path compatibility | Domain stores | Resolve numbered decisions, handoffs, deltas, evidence, and legacy structured paths after import. | Logical path strings from state, journal, provenance, and lifecycle. | Domain record content and metadata. | Sequence identity preservation. | Legacy historical paths resolve after filesystem import. | Completion archive, prompt context, transition input resolver. |
| Domain-specific missing behavior | Freshness and validation services | Convert unresolved migrated paths to stale, invalid, or blocked outcomes. | Resolution failures and domain context. | Deterministic freshness/validation result. | Current domain behavior. | Missing migrated artifacts do not become silent path failures. | Roadmap state machine, selection, execution preparation, verification. |

## 8. Architectural Responsibilities

- Identity service owns path-to-domain resolution; domain stores own record retrieval.
- Freshness service owns hash comparison semantics; callers do not compute ad hoc database hashes.
- Retained filesystem artifacts remain owned by filesystem-backed document flows.
- Validation authority remains domain-specific when a path resolves to the wrong kind of artifact.

**Cross-Milestone Constraints**
- SQLite becomes canonical only for machine-managed migrated domains.
- Retained markdown prompt/workflow artifacts remain filesystem-backed.
- Filesystem exports are deterministic, importable serializations of migrated domain state.
- Repo-relative path strings remain durable logical identities.
- `DNNNN`, `NNNN`, family IDs, runtime prompt names, and correlation IDs preserve imported identity.
- Behavior moves behind semantic domain operations before callers stop using filesystem representations.
- Freshness semantics remain trustworthy across retained files and migrated records.

## 9. Components and Modules

**Logical artifact resolver**
- Purpose: Resolve repo-relative path strings across storage classes.
- Responsibilities: Classify paths, dispatch to retained files or migrated domain stores, return content/hash metadata.
- Owned State: No durable state; optional in-memory registry.
- Consumed State: Path constants, domain stores, retained files.
- Public Contracts: Resolve by repo-relative path.
- Internal Contracts: No caller observes physical storage directly.
- Dependencies: Milestone 1 domain services.
- Tests Required: Resolution tests for every retained and migrated path class.
**Canonical hash service**
- Purpose: Produce stable freshness hashes.
- Responsibilities: Hash retained file bytes and migrated canonical representations consistently.
- Owned State: No durable state.
- Consumed State: Resolver content and Milestone 2 serialization.
- Public Contracts: Compute hash for logical artifact identity.
- Internal Contracts: Same logical content yields same hash independent of backing store.
- Dependencies: Milestone 2 canonical serialization.
- Tests Required: File-backed parity and migrated-record hash tests.
**Freshness integration adapters**
- Purpose: Update existing freshness evaluators to use logical resolution.
- Responsibilities: Replace direct file reads where migrated paths may later be database-backed.
- Owned State: None.
- Consumed State: Execution preparation, selection, projection, journal/state references.
- Public Contracts: Existing freshness result models.
- Internal Contracts: Domain-specific stale/invalid behavior preserved.
- Dependencies: Resolver and hash service.
- Tests Required: Freshness regression tests.

## 10. Repository and File Impact

- `src/LoopRelay.Roadmap.Cli/Services/Artifacts` and `RoadmapArtifactPaths` remain the source of existing roadmap artifact path constants until a shared path layer replaces them.
- `src/LoopRelay.Roadmap.Cli/Services/State`, `TransitionState`, `TransitionCoordination`, `Decisions`, `ExecutionPreparation`, `Projections`, `Splits`, and `ArtifactManagement` contain the current roadmap domain stores.
- `src/LoopRelay.Cli/Services/Execution/LoopArtifacts.cs` owns live decision, handoff, and operational delta loop-file behavior today.
- `src/LoopRelay.Completion/Services/ArtifactStorage` owns completed-epic archive and evidence access used by completion workflows.
- `src/LoopRelay.Infrastructure/Services/Artifacts` contains repository-relative path bridging and numbered artifact sequence helpers.
- Expected additions near roadmap artifact/freshness services, with minimal shared abstractions for completion/projection consumers.
- Tests extend existing freshness, transition input, prompt context, and evidence access test classes.
- No database implementation is required, but resolver contracts must allow later SQLite-backed domain providers.

## 11. Public Contracts

- Logical artifact resolution API by repo-relative path.
- Canonical content hash API used by freshness evaluators.
- Resolution failure contract that reports missing, wrong-domain, stale, invalid, or blocked status as applicable.

## 12. Internal Contracts

- Resolver classification must be deterministic and case rules must match domain identity rules.
- Hashing migrated records must use canonical export-equivalent content unless a domain declares a separate freshness token.
- Prompt builders receive content through the resolver when evidence or migrated histories may be database-backed.
- Unresolved references never fall back to unrelated filesystem paths.

## 13. Data and State Model

**Logical artifact descriptor**
- Owner: Resolver
- Lifecycle: Created per resolution request.
- Durability: Transient.
- Mutability: Immutable.
- Identity: Repo-relative path plus domain classification.
- Validation: Path normalization and domain lookup.
- Recovery: Re-resolve after state repair or import.
- Consumers: Freshness, prompt builders, validation, verification.
**Canonical hash**
- Owner: Hash service
- Lifecycle: Computed on demand and stored only by existing provenance/manifest domains.
- Durability: Persisted only where current manifests store hashes.
- Mutability: Recomputed when content changes.
- Identity: Hash algorithm plus logical path.
- Validation: Compare against stored hash.
- Recovery: Recompute from canonical content.
- Consumers: Execution preparation, selection, projection, journal verification.

## 14. Lifecycle and State Transitions

- Resolve: Normalize path -> classify retained/migrated/export -> retrieve content -> compute or return canonical hash -> return descriptor.
- Freshness check: Load stored path/hash -> resolve current path -> compare hash and domain status -> produce fresh/stale/invalid result.
- Failure: Missing or wrong-domain path -> domain-specific stale/invalid/blocked diagnostic; no silent null content.

## 15. Execution Flow

- Startup registers logical providers for retained filesystem artifacts and migrated domain stores.
- Normal operation asks resolver for every persisted path whose backing storage may vary.
- Failure flow reports unresolved paths with domain and referring record.
- Recovery flow imports/regenerates missing migrated state or restores retained files, then reruns freshness checks.
- Shutdown has no additional work.

## 16. Dependency Closure

- Hard prerequisite: Milestone 1 domain behavior.
- Hard prerequisite: Milestone 2 canonical serialization/hash equivalence policy.
- Supporting infrastructure: path constants, `RepositoryArtifactStore`, existing freshness evaluators.
- Future dependency: SQLite domain providers in Milestones 5 through 9.
- Enables Milestones 4, 5, 6, 7, 8, 9, 10, 11, 12, and 13.

## 17. Failure Modes

**Unresolved retained file**
- Description: A path that should remain filesystem-backed is missing.
- Detection: Resolver file existence/read.
- Behavior: Return missing retained artifact status.
- Recovery: Restore file or rerun producing workflow.
- Diagnostics: Path and referring domain.
- Tests: Missing retained roadmap/spec/context fixtures.
**Unresolved migrated path**
- Description: A path points to a migrated record absent from the domain store.
- Detection: Domain provider lookup.
- Behavior: Return stale/invalid/blocked according to consuming domain.
- Recovery: Import/export repair or regenerate record.
- Diagnostics: Logical path, domain, and identity parse result.
- Tests: Missing evidence/history/state references.
**Hash mismatch**
- Description: Current canonical content hash differs from stored freshness baseline.
- Detection: Hash comparison.
- Behavior: Report stale with reason tied to logical artifact.
- Recovery: Regenerate derived artifact or update provenance through workflow.
- Diagnostics: Stored hash, current hash, path.
- Tests: Retained-file drift and migrated-record drift tests.

## 18. Validation and Invariants

**Repo-relative paths remain stable artifact identities independent of backing storage.**
- Source Authority: Roadmap guiding principles and Milestone 3 objective.
- Enforcement Point: Resolver classification and path compatibility tests.
- Failure Behavior: Resolution fails or reports identity mismatch.
- Test Strategy: Fixtures containing persisted path references across state, journal, provenance, lifecycle, splits, decisions, histories, and evidence.
**File-backed freshness results match pre-migration behavior.**
- Source Authority: Milestone 3 acceptance criteria.
- Enforcement Point: Regression tests against current freshness scenarios.
- Failure Behavior: Freshness result mismatch blocks exit.
- Test Strategy: Run existing freshness tests through resolver-backed evaluators.
**Missing migrated artifacts fail explicitly.**
- Source Authority: Milestone 3 acceptance criteria.
- Enforcement Point: Domain-specific failure mapping.
- Failure Behavior: Silent null or unrelated file fallback is invalid.
- Test Strategy: Missing-path tests for all migrated path classes.

## 19. Testing Strategy

- Unit tests for path classification and normalization.
- Unit tests for canonical hashes over retained files and exported migrated content.
- Integration tests updating execution preparation, selection, projection, prompt context, transition input, unblock planner, and completion evidence access.
- Regression tests proving file-backed mode freshness parity.
- Failure-path tests for missing retained files, missing migrated records, wrong-domain paths, and hash drift.

## 20. Fixtures and Test Data

- Retained artifact fixtures for specs, roadmap source, plan, operational context, live decisions, live handoff, and live delta.
- Migrated artifact fixtures for structured JSON, JSONL, histories, split families, and evidence.
- Persisted references in state, journal, provenance, lifecycle, split lineage, and decision ledger.
- Hash drift fixtures with one changed retained file and one changed migrated snapshot.
- Missing path fixtures for every consumer behavior class.

## 21. Acceptance Demonstration

- Seed a workspace with retained files and imported migrated-domain snapshots.
- Resolve a retained spec path and a historical handoff path and print their canonical hashes.
- Run execution-preparation and selection freshness checks in file-backed mode and compare with existing expected results.
- Remove a referenced evidence artifact and verify the consumer reports stale/invalid/blocked rather than an unclassified file read failure.

## 22. Certification Evidence

- Resolver test output for all path classes.
- Freshness parity test output.
- Hash comparison transcript for retained and migrated artifacts.
- Failure diagnostics for missing migrated evidence/history references.

## 23. Implementation Plan

**Define logical artifact descriptor**
- Purpose: Give callers a storage-neutral content/hash contract.
- Deliverables: Descriptor and resolution result types.
- Dependencies: Path inventory from roadmap/audit.
- Completion: All retained and migrated path classes can be represented.
**Implement resolver providers**
- Purpose: Resolve retained files and migrated domain records.
- Deliverables: Filesystem provider and file-backed migrated provider registry.
- Dependencies: Domain stores.
- Completion: Resolution tests pass.
**Implement canonical hashing**
- Purpose: Make freshness storage-neutral.
- Deliverables: Hash service using canonical content.
- Dependencies: Milestone 2 serialization.
- Completion: File-backed parity tests pass.
**Integrate freshness consumers**
- Purpose: Remove direct file-hash assumptions for migrated paths.
- Deliverables: Updated evaluators/builders where needed.
- Dependencies: Resolver and hash service.
- Completion: Existing workflow tests pass.

## 24. Parallel Work Opportunities

**Resolver classification**
- Owner: Persistence engineer
- Dependencies: Path inventory.
- Sync: Provider registry shape.
- Risk: Ambiguous paths classified incorrectly.
**Freshness integrations**
- Owner: Roadmap/projection engineer
- Dependencies: Hash API.
- Sync: Shared stale reason mapping.
- Risk: Changed stale diagnostics.
**Prompt/evidence access**
- Owner: Completion/CLI engineer
- Dependencies: Evidence provider.
- Sync: Logical path result model.
- Risk: Prompt builders still assume physical files.

## 25. Risks and Mitigations

**Hash equivalence is under-specified**
- Class: architectural
- Impact: Freshness becomes unreliable after SQLite migration.
- Likelihood: medium
- Detection: Parity tests and drift fixtures.
- Mitigation: Base migrated hashes on canonical export bytes unless explicitly overridden.
- Fallback: Invalidate freshness baselines on first SQLite import with explicit diagnostics.
**Path resolver owns too much domain logic**
- Class: maintainability
- Impact: Domain behavior duplicates in resolver.
- Likelihood: medium
- Detection: Duplicated parsing and tests.
- Mitigation: Resolver classifies and dispatches; domain stores parse identities.
- Fallback: Move parsing back into domain providers.
**Missing path behavior changes user-visible flow**
- Class: integration
- Impact: Workflows fail differently than before.
- Likelihood: medium
- Detection: Failure-path regression tests.
- Mitigation: Map missing results per current domain semantics.
- Fallback: Preserve legacy exception shape for filesystem-only paths.

## 26. Observability and Diagnostics

- Resolution diagnostics include logical path, provider, storage classification, and referring domain when available.
- Freshness diagnostics include stored hash, current hash, and stale reason.
- Debug views can enumerate unresolved logical references without mutating state.

## 27. Performance and Scalability Considerations

- Baseline expectation is one content lookup per referenced artifact with existing filesystem caching preserved.
- Likely bottleneck is resolving many evidence paths or large journals.
- Measure resolver cache hit rate and freshness-check duration on large fixtures.
- Deferred optimization: provider-level content hash caching keyed by domain version.

## 28. Security and Safety Considerations

- Normalize and validate paths to prevent traversal outside repository scope.
- Do not expose SQLite row internals through logical path resolution.
- Treat imported/exported markdown as data, not executable content.
- Avoid leaking full artifact bodies in diagnostics unless existing behavior already does.

## 29. Documentation Updates

- No documentation-only deliverable is required.
- Any CLI diagnostic text introduced by executable verification paths must name logical paths and domain statuses accurately.

## 30. Exit Criteria

- Retained files and migrated file-backed artifacts resolve through the logical resolver.
- Freshness checks match current file-backed results.
- Historical logical paths resolve after import.
- Missing migrated paths produce deterministic stale/invalid/blocked outcomes.
- No SQLite canonical domain is claimed.

## 31. Transition to Next Milestone

- Milestone 4 can import database state without breaking path references.
- Milestones 5 through 9 can move domains to SQLite while preserving freshness and prompt consumption.
- Remaining limitation: actual SQLite providers are not implemented yet.

## Open Implementation Questions

- The hash policy for migrated records must decide whether to preserve current file JSON hashes exactly or intentionally establish canonical logical hashes during database import.
- The storage status of `.agents/core/roadmap-completion-context.md`, `.agents/selection.md`, and projection markdown bodies remains outside this milestone unless clarified by roadmap changes.

<!-- GENERATED:END -->

<!-- MANUAL:START -->
<!-- Add manually maintained notes here. This block is preserved by the generator. -->
<!-- MANUAL:END -->
