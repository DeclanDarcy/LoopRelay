# Milestone 6 — Provenance and Projection Metadata Run Canonically from SQLite Deep Dive

<!-- GENERATED:START -->
Generated from `.agents/specs/roadmap.md` and `.agents/specs/audit.md`.

## 1. Milestone Summary

- Milestone Identifier: Milestone 6
- Milestone Name: Provenance and Projection Metadata Run Canonically from SQLite
- Short Description: Move execution preparation provenance, selection provenance, and projection manifest metadata to SQLite while preserving freshness and export/import.
- Implementation Role: Migrates metadata domains that depend on retained files, core SQLite state, and logical hashes.
- Roadmap Position: Sixth milestone; depends on core SQLite state and logical artifact resolution.
- Primary Outcomes:
- Execution preparation and selection freshness evaluate from SQLite-backed metadata.
- Projection manifest metadata is canonical in SQLite and still describes filesystem projection bodies by stable path.
- Malformed/missing behavior is preserved according to domain compatibility rules.

## 2. Normative Basis

**Roadmap Authority**
- Roadmap authority: `.agents/specs/roadmap.md` `# Project Goal`, `# Guiding Principles`, and this milestone's `## Milestone` section.
- `.agents/specs/roadmap.md` `## Milestone 6 — Provenance and Projection Metadata Run Canonically from SQLite`.
**Architectural Authority**
- Architectural authority: `.agents/specs/audit.md` sections `2. Persistence Authority`, `4. Persistence Semantics`, `5. Filesystem-Coupled Behaviors`, `8. Cross-Boundary Dependencies`, `9. Compatibility and Migration Constraints`, `10. Round-Trip Import/Export Requirements`, `14. Persistence Domain Inventory`, `16. Persistence Dependency Graph`, `17. Synchronization Model`, `19. Persistence Platform Capabilities`, and `20. Roadmap-Relevant Findings`.
**Implementation Authority**
- Implementation authority: existing LoopRelay persistence code under `src/LoopRelay.Roadmap.Cli/Services`, `src/LoopRelay.Cli/Services/Execution`, `src/LoopRelay.Completion/Services/ArtifactStorage`, `src/LoopRelay.Projections/Services`, `src/LoopRelay.Core/Abstractions/Artifacts/IArtifactStore.cs`, and `src/LoopRelay.Infrastructure/Services/Artifacts`.
**Supporting Context**
- Supporting context: current tests under `tests/LoopRelay.Roadmap.Cli.Tests`, `tests/LoopRelay.Cli.Tests`, `tests/LoopRelay.Completion.Tests`, `tests/LoopRelay.Projections.Tests`, and `tests/LoopRelay.Infrastructure.Tests`.
- No undocumented authority is introduced here; unresolved audit mismatches remain open questions rather than implementation permission.

## 3. Objective

Switch execution preparation provenance, selection provenance, and projection manifest metadata to SQLite-backed canonical stores, preserving current freshness semantics, path/hash references, malformed/empty compatibility, and deterministic export/import.

## 4. Non-Goals

- Do not migrate projection markdown bodies unless a later roadmap explicitly does so.
- Do not migrate transition journal, loop histories, evidence, or archives.
- Do not change retained roadmap source, project context, selection markdown, completion context, or prompt artifacts.
- Do not normalize execution/selection malformed behavior unless tests and roadmap authorize it.

## 5. Runtime / System State Before

- Core roadmap state, decision ledger, lifecycle, and split lineage are canonical in SQLite.
- Provenance and projection manifests are still filesystem JSON.
- Freshness uses logical artifact identity and canonical hashes.
- Projection manifest persistence is duplicated across roadmap and projections projects.

## 6. Runtime / System State After

- Execution preparation manifest behavior is SQLite canonical.
- Selection provenance manifest behavior is SQLite canonical.
- Projection manifest metadata is SQLite canonical and keyed by runtime prompt identity.
- Export/import preserves logical equality for all three metadata domains.
- Freshness detects the same retained-file and migrated-core-state drift as before.

## 7. Capabilities Delivered

| Capability | Owner | Scope | Inputs | Outputs | Dependencies | Acceptance Criteria | Downstream Consumers |
|---|---|---|---|---|---|---|---|
| SQLite execution preparation provenance | Execution preparation provenance store | Persist authoritative inputs and trusted derived artifacts. | Active epic/specs, operational context, execution prompt, plan, milestones, decision ledger hash. | Canonical provenance rows and export JSON. | Core SQLite decision ledger and logical hash service. | Freshness detects the same retained-file drift as before. | Execution preparation, roadmap artifact snapshot, readiness checks. |
| SQLite selection provenance | Selection provenance store | Persist trusted/superseded selection cycle metadata. | Selection artifact, roadmap sources, completion context, projection inputs, retired epic state. | Canonical selection provenance rows and export JSON. | Core SQLite state, projection metadata, logical resolver. | Selection freshness detects roadmap, projection, retired epic, and completion-context drift as before. | Selection freshness checks, transition routing. |
| SQLite projection manifest | Projection manifest store | Persist projection metadata keyed by runtime prompt. | Runtime prompt name, projection path/hash, validation/stale status, causal inputs. | Canonical projection manifest rows and export JSON. | Projection generation and validation services. | Rows preserve projection paths, hashes, stale status, validation status, and causal inputs. | Projection validators, selection provenance, prompt transitions. |
| Metadata freshness equivalence | Freshness evaluators | Evaluate drift using logical artifact identity across retained and SQLite-backed domains. | Stored metadata and current logical artifact hashes. | Fresh/stale/invalid results matching current behavior. | Milestone 3 resolver and Milestone 5 core stores. | File-backed and SQLite-backed metadata modes produce equivalent freshness results. | Verification mode, workflow gates. |

## 8. Architectural Responsibilities

- Provenance stores own canonical metadata and compatibility behavior for missing/malformed exports.
- Freshness evaluators own comparison logic, not raw file reads.
- Projection manifest store ownership must be consolidated or layered so roadmap and projections projects observe one canonical behavior.
- Projection bodies remain filesystem artifacts; metadata references them by logical path and hash.

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
- Purpose: Persist execution preparation provenance, selection provenance, and projection metadata in canonical database tables.
- Responsibilities: Implement the same domain operations as the file-backed store.
- Owned State: SQLite rows for execution preparation provenance, selection provenance, and projection metadata.
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
- Update `Services/ExecutionPreparation`, `Services/Decisions/SelectionProvenance*`, and `Services/Projections/ProjectionManifestStore`.
- Coordinate with `src/LoopRelay.Projections/Services/Manifests/ProjectionManifestStore.cs` so duplicate manifest persistence remains semantically aligned.
- Tests extend current execution preparation, selection provenance, and projection manifest suites.

## 11. Public Contracts

- Existing manifest export paths remain: `.agents/execution-preparation-manifest.json`, `.agents/selection-provenance-manifest.json`, `.agents/projections/manifest.json`.
- Projection metadata remains keyed by runtime prompt identity.
- Freshness result contracts and stale reason vocabulary remain behavior-compatible.
- Missing/malformed export import behavior is domain-specific and explicit.

## 12. Internal Contracts

- Metadata writes occur after referenced artifacts exist or are explicitly recorded as missing/stale according to current behavior.
- Projection manifest upsert by runtime prompt is atomic within the store.
- Execution/selection malformed exported manifests load empty only when in explicit import/compatibility mode.
- Freshness uses resolver-provided canonical hashes for SQLite-backed core domains.

## 13. Data and State Model

**Execution preparation provenance rows**
- Owner: Execution preparation store
- Lifecycle: Recorded after preparation, superseded by new preparation.
- Durability: SQLite canonical.
- Mutability: Mutable snapshot with active derived artifacts.
- Identity: Artifact kind plus identity and causal inputs.
- Validation: Schema, path/hash references, compatibility malformed policy.
- Recovery: Export/import or regenerate preparation.
- Consumers: Execution readiness and freshness checks.
**Selection provenance rows**
- Owner: Selection provenance store
- Lifecycle: Recorded per selection cycle, active entries can be superseded.
- Durability: SQLite canonical.
- Mutability: Mutable active/superseded metadata.
- Identity: Selection artifact identity and cycle metadata.
- Validation: Path/hash references, stale reason mapping.
- Recovery: Export/import or rerun selection.
- Consumers: Selection freshness and transition routing.
**Projection manifest rows**
- Owner: Projection manifest store
- Lifecycle: Upserted when projection bodies are generated/validated.
- Durability: SQLite canonical.
- Mutability: Mutable by runtime prompt.
- Identity: Runtime prompt name.
- Validation: Projection path/hash/status/causal inputs.
- Recovery: Export/import or regenerate projection.
- Consumers: Projection service, selection provenance, prompt context.

## 14. Lifecycle and State Transitions

- Execution preparation: Empty -> Recorded -> Fresh/Stale -> Superseded by new record/exported.
- Selection provenance: Empty -> Active trusted selection -> Superseded -> Exported/imported.
- Projection metadata: Missing prompt row -> Upserted valid/stale/invalid row -> Revalidated/upserted -> Exported.
- Failure: missing retained inputs produce stale/missing results; invalid SQLite metadata blocks trusted freshness.

## 15. Execution Flow

- Startup validates database and loads metadata stores after core domain stores.
- Normal operation records provenance/metadata in SQLite and uses logical resolver for referenced content.
- Freshness flow compares stored causal inputs against current retained files, core SQLite records, and projection bodies.
- Failure flow preserves domain-specific empty/malformed behavior only at import/export boundaries and blocks invalid canonical rows.
- Recovery flow regenerates metadata from current artifacts or imports valid exports.

## 16. Dependency Closure

- Hard prerequisite: Milestone 5 core SQLite state.
- Hard prerequisite: Milestone 3 logical artifact identity and freshness resolution.
- Inherited capability: Milestone 2 deterministic export/import.
- Supporting infrastructure: projection validation and current provenance services.
- Future dependency: transition journal input snapshots in Milestone 7.
- Enables Milestones 7, 9, 11, 12, and 13.

## 17. Failure Modes

**Referenced retained input drift**
- Description: A stored path/hash no longer matches retained file content.
- Detection: Freshness evaluator.
- Behavior: Report stale with existing stale reason.
- Recovery: Regenerate affected metadata or restore input.
- Diagnostics: Path, stored hash, current hash, stale reason.
- Tests: Existing drift tests in SQLite metadata mode.
**Projection body missing**
- Description: Manifest row references a projection markdown path that is absent.
- Detection: Logical resolver during freshness/validation.
- Behavior: Mark projection stale/invalid according to existing behavior.
- Recovery: Regenerate projection body and metadata.
- Diagnostics: Runtime prompt and projection path.
- Tests: Missing projection body fixture.
**Malformed exported provenance**
- Description: Imported filesystem export has malformed execution/selection manifest.
- Detection: Import serializer.
- Behavior: Load empty or fail according to preserved domain-specific compatibility mode.
- Recovery: Regenerate or repair export.
- Diagnostics: Domain and manifest path.
- Tests: Malformed export import tests.

## 18. Validation and Invariants

**Freshness behavior remains equivalent to current file-backed behavior.**
- Source Authority: Milestone 6 objective and acceptance criteria.
- Enforcement Point: Conformance tests across file-backed and SQLite metadata modes.
- Failure Behavior: Freshness mismatch blocks exit.
- Test Strategy: Run existing drift tests in both modes.
**Projection metadata is keyed by runtime prompt identity.**
- Source Authority: Milestone 6 acceptance criteria and audit.
- Enforcement Point: Unique constraints and upsert tests.
- Failure Behavior: Duplicate prompt rows or lost metadata.
- Test Strategy: Upsert and export/import tests.
**Projection body content remains filesystem-backed unless explicitly migrated later.**
- Source Authority: Roadmap Milestone 6 scope.
- Enforcement Point: Resolver and repository impact tests.
- Failure Behavior: Projection body stored as canonical SQLite content.
- Test Strategy: Tests verify manifest row references existing path/hash.

## 19. Testing Strategy

- Unit tests for SQLite execution preparation, selection provenance, and projection manifest stores.
- Freshness parity tests for retained-file drift, core-state drift, projection drift, and missing artifacts.
- Export/import logical equality tests for all three metadata domains.
- Regression tests for malformed/missing manifest compatibility.
- Integration tests through projection generation, selection, and execution preparation workflows.
- Failure-path tests for missing projection bodies, unresolved logical inputs, and invalid canonical rows.

## 20. Fixtures and Test Data

- Execution preparation manifest with active epic, specs, operational context, execution prompt, plan, milestones, and decision ledger hash.
- Selection provenance with roadmap sources, completion context, retired epics, projection inputs, and active/superseded entries.
- Projection manifest with valid, stale, and invalid entries across runtime prompts.
- Malformed export manifests and missing optional manifests.
- Drift fixtures for retained files and SQLite core decision/state data.

## 21. Acceptance Demonstration

- Import a workspace with core SQLite state and file-backed provenance/manifests.
- Switch metadata domains to SQLite.
- Modify a retained roadmap source or projection body and run selection freshness.
- Modify operational context or decision ledger and run execution preparation freshness.
- Export metadata manifests, import them into a clean database, and compare logical equality.

## 22. Certification Evidence

- Passing SQLite metadata store tests.
- Freshness parity report across file-backed and SQLite metadata modes.
- Export/import logical equality report.
- Diagnostics from malformed/missing manifest compatibility tests.

## 23. Implementation Plan

**Implement SQLite metadata stores**
- Purpose: Make provenance and projection metadata canonical in database.
- Deliverables: Execution preparation, selection, projection manifest stores.
- Dependencies: Core SQLite schema and resolver.
- Completion: Store tests pass.
**Integrate freshness evaluators**
- Purpose: Use SQLite metadata and logical hashes.
- Deliverables: Updated evaluators and stale reason preservation.
- Dependencies: Metadata stores.
- Completion: Freshness parity tests pass.
**Align duplicated projection manifest behavior**
- Purpose: Prevent roadmap/projections divergence.
- Deliverables: Shared or layered manifest store behavior.
- Dependencies: Projection metadata store.
- Completion: Both project test suites pass.
**Add export/import coverage**
- Purpose: Preserve filesystem interchange.
- Deliverables: Metadata export/import tests and fixtures.
- Dependencies: Stores and serializers.
- Completion: Clean import equality passes.

## 24. Parallel Work Opportunities

**Execution preparation store**
- Owner: Execution preparation engineer
- Dependencies: Core resolver.
- Sync: Freshness hash API.
- Risk: Decision ledger hash semantics change.
**Selection provenance store**
- Owner: Selection engineer
- Dependencies: Projection metadata shape.
- Sync: Stale reason mapping.
- Risk: Retired epic state hash drift.
**Projection manifest consolidation**
- Owner: Projection engineer
- Dependencies: Canonical store contract.
- Sync: Roadmap/projections project boundary.
- Risk: Duplicate implementations diverge.

## 25. Risks and Mitigations

**Freshness false positives/negatives**
- Class: architectural
- Impact: Workflows skip required regeneration or regenerate unnecessarily.
- Likelihood: medium
- Detection: Parity drift tests.
- Mitigation: Use logical resolver and preserve stale reason mapping.
- Fallback: Invalidate affected metadata after migration with explicit stale reason.
**Projection manifest ownership split**
- Class: maintainability
- Impact: Two stores serialize different state.
- Likelihood: high
- Detection: Cross-project tests.
- Mitigation: Share behavior or enforce conformance suite.
- Fallback: One project adapts to canonical service through interface.
**Malformed behavior accidentally hardened**
- Class: integration
- Impact: Existing workspaces that loaded empty now fail.
- Likelihood: medium
- Detection: Malformed fixture tests.
- Mitigation: Explicit compatibility import mode per domain.
- Fallback: Preserve empty-on-malformed at import boundary only.

## 26. Observability and Diagnostics

- Freshness diagnostics include metadata domain, logical input path, stored hash, current hash, and stale reason.
- Projection diagnostics include runtime prompt name, projection path, validation status, and stale status.
- Import/export diagnostics report metadata row counts and malformed compatibility decisions.

## 27. Performance and Scalability Considerations

- Metadata operations should avoid scanning export files during normal SQLite mode.
- Likely bottlenecks are resolving many causal inputs and projection entries.
- Measure freshness evaluation time and causal input count.
- Deferred optimization: cached input hash materialization keyed by logical artifact version.

## 28. Security and Safety Considerations

- Validate all referenced paths remain repository-relative.
- Do not expose prompt/projection body content in diagnostic logs by default.
- Use parameterized SQL for metadata writes.
- Preserve trust boundaries for retained files used as prompt inputs.

## 29. Documentation Updates

- No documentation-only deliverable is required.
- If projection/freshness command output changes, executable help text should distinguish canonical metadata from filesystem projection bodies.

## 30. Exit Criteria

- Execution preparation provenance, selection provenance, and projection metadata are canonical in SQLite.
- Freshness behavior matches current behavior across retained and migrated inputs.
- Exports/imports are logically equivalent.
- Malformed/missing compatibility is explicitly tested.
- No journal/history/evidence/archive/sync capability is claimed.

## 31. Transition to Next Milestone

- Milestone 7 receives SQLite-backed input snapshots and projection/provenance metadata for transition journal records.
- Milestone 9 receives resolver-compatible metadata for evidence references.
- Remaining limitation: transition chronology and content-heavy histories are still file-backed.

## Open Implementation Questions

- Projection markdown body canonical storage remains outside this milestone; manifest rows continue to reference filesystem projection bodies by path/hash.
- The audit's question about `.agents/core/roadmap-completion-context.md` remains unresolved; this milestone treats it as retained filesystem input because the roadmap does not migrate it.

<!-- GENERATED:END -->

<!-- MANUAL:START -->
<!-- Add manually maintained notes here. This block is preserved by the generator. -->
<!-- MANUAL:END -->
