# Milestone 9: Execution Evidence Moves to SQLite with Path-Compatible Access

## Objective

Move `.agents/evidence/execution/*` to SQLite while preserving path-compatible evidence reads, sequence allocation, search, prompt consumption, completion evaluation, and export/import.

## Implementation

- [x] Implement `IExecutionEvidenceStore` with write, read by logical path, search, allocation, import, export, and hash validation.
- [x] Route `RoadmapArtifacts.WriteNumberedEvidenceAsync` and `CompletionArtifacts.WriteNumberedEvidenceAsync` through the evidence store only for `.agents/evidence/execution`.
- [x] Keep non-execution evidence directories filesystem-backed.
- [x] Update consumers:
  - [x] `RoadmapExecutionBridge`
  - [x] `CompletionCertificationService`
  - [x] `TransitionInputResolver`
  - [x] `RoadmapPromptContextBuilder`
  - [x] `RoadmapUnblockPlanner`
  - [x] completion context builders
- [x] Ensure consumers pass when exported evidence files are deleted but SQLite rows exist.

## Implementation Constraints

- Only `.agents/evidence/execution/*` migrates.
- Evidence body, logical path, stem, suffix, hash, and metadata are stored together.
- Roadmap and completion evidence writers use the same domain store.
- Prompt context, unblock planner, and completion evaluation pass with exported evidence removed.
- Archive recovery remains pending until M10.

## Tests

- [x] Existing stem `execution-trust-posture.0003.md` imports and next write allocates `0004`.
- [x] Prompt context reads SQLite-backed execution evidence.
- [x] Unblock planner searches or hashes SQLite-backed evidence.
- [x] Completion evaluation consumes SQLite-backed claim evidence.
- [x] Missing referenced evidence maps to existing stale/invalid/blocked behavior.
- [x] Export/import preserves body, path, stem, sequence, and hash.

## Exit Criteria

- [x] Execution evidence is SQLite-canonical.
- [x] All path-compatible consumers work without physical evidence export files.
