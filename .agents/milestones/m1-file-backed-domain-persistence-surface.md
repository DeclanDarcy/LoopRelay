# Milestone 1: File-Backed Domain Persistence Surface

## Objective

Move current behavior behind semantic domain contracts while files remain canonical.

## Implementation

- [ ] Add contracts for all migrated domains.
- [ ] Implement file-backed adapters that delegate to current stores/helpers.
- [ ] Replace direct persistence semantics in callers with domain operations where the behavior belongs to a migrated domain.
- [ ] Keep retained live file reads/writes as file operations.
- [ ] Add conformance tests that freeze current behavior.

## Implementation Constraints

- Files remain canonical; do not add SQLite schema, database behavior, import commands, or export commands in this milestone.
- Domain contracts return semantic results, not raw file lists.
- Preserve sequence collisions, live-first fallback, strict JSON failures, and provenance empty-on-malformed behavior.
- Conformance tests freeze file-backed behavior for all migrated domains.

## Code Impact

- [x] Wrap `DecisionLedgerStore`, `RoadmapStateStore`, `ArtifactLifecycleStore`, `SplitFamilyStore`, `ExecutionPreparationManifestStore`, `SelectionProvenanceManifestStore`, `ProjectionManifestStore`, and `TransitionJournalStore` behind interfaces.
- [x] Extract loop history behavior out of `LoopArtifacts` into a history store/facade while preserving live-file methods.
- [x] Extract numbered execution evidence behavior out of `RoadmapArtifacts.WriteNumberedEvidenceAsync` and `CompletionArtifacts.WriteNumberedEvidenceAsync`.
- [x] Update `RoadmapCliComposition` and Main CLI composition to construct contract-based services.
  - [x] Roadmap CLI composition constructs file-backed roadmap stores behind contract-typed variables.
  - [x] Main CLI composition constructs the file-backed loop history store behind a contract-typed variable.
  - [x] Main CLI composition constructs execution evidence stores behind contract-typed variables.

## Tests

- [x] Sequence allocation for decisions, handoffs, deltas, and evidence.
  - [x] Loop history sequence allocation for decisions, handoffs, and deltas.
  - [x] Execution evidence sequence allocation.
- [x] Live-first read for decisions and handoffs.
- [x] Strict JSON malformed behavior.
- [x] Empty-on-malformed execution/selection manifest behavior.
- [x] Split family legacy markdown migration.
- [ ] Journal started/completed/failed append compatibility.

## Exit Criteria

- [ ] Existing workflows pass with file-backed persistence.
- [ ] Migrated-domain behavior is available through semantic contracts.
- [ ] No SQLite schema or canonical database behavior is introduced.
