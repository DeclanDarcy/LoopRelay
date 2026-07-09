# Milestone 9: Execution Evidence Moves to SQLite with Path-Compatible Access

## Objective

Move `.agents/evidence/execution/*` to SQLite while preserving path-compatible evidence reads, sequence allocation, search, prompt consumption, completion evaluation, and export/import.

## Implementation

- [ ] Implement `IExecutionEvidenceStore` with write, read by logical path, search, allocation, import, export, and hash validation.
- [ ] Route `RoadmapArtifacts.WriteNumberedEvidenceAsync` and `CompletionArtifacts.WriteNumberedEvidenceAsync` through the evidence store only for `.agents/evidence/execution`.
- [ ] Keep non-execution evidence directories filesystem-backed.
- [ ] Update consumers:
  - [ ] `RoadmapExecutionBridge`
  - [ ] `CompletionCertificationService`
  - [ ] `TransitionInputResolver`
  - [ ] `RoadmapPromptContextBuilder`
  - [ ] `RoadmapUnblockPlanner`
  - [ ] completion context builders
- [ ] Ensure consumers pass when exported evidence files are deleted but SQLite rows exist.

## Tests

- [ ] Existing stem `execution-trust-posture.0003.md` imports and next write allocates `0004`.
- [ ] Prompt context reads SQLite-backed execution evidence.
- [ ] Unblock planner searches or hashes SQLite-backed evidence.
- [ ] Completion evaluation consumes SQLite-backed claim evidence.
- [ ] Missing referenced evidence maps to existing stale/invalid/blocked behavior.
- [ ] Export/import preserves body, path, stem, sequence, and hash.

## Exit Criteria

- [ ] Execution evidence is SQLite-canonical.
- [ ] All path-compatible consumers work without physical evidence export files.
