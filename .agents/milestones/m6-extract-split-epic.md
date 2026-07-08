# Milestone 6: Extract Split Epic

## Handler Method

```csharp
Task<ArtifactPromotionResult> ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

## Boundary

- [x] Handler returns `ArtifactPromotionResult`.
- [x] Caller controls milestone continuation.

## Prompt And Projection

- [x] Phase `Split epic`.
- [x] Runtime prompt `SplitEpic`.
- [x] Projection prompt `ProjectionForSplitEpic`.
- [x] From/to for prompt `SplitEpicProposed -> SplitChildSelection`.
- [x] Projection path `.agents/projections/split-epic.md`.
- [x] Prompt output path in state/journal `.agents/splits`.
- [x] Secondary input is active selection content.
- [x] Envelope is the normal prompt transition.

## Required Precondition

- [x] Read and validate fresh active selection through `ActiveSelectionReader`.

## Runtime Context

- [x] Runtime context is `BuildCreateOrSplitContext` with projection content,
  selection proposal, and repository inspection instructions.

## Bundle Extraction And Interpretation

- [x] Use `BundleExtractionPolicy.RepositorySafe`.
- [x] Parse `# FILE:` markers.
- [x] Normalize separators.
- [x] Reject rooted paths and parent traversal.
- [x] Reject duplicate targets by throwing `RoadmapStepException`, then convert
  that exception to invalid split interpretation.
- [x] Trim only leading and trailing separator noise around file bodies.
- [x] Hash each extracted file body.
- [x] Interpreter requires paths matching `.agents/epic-N.md`.
- [x] Child content classification and validation use the same epic classifier
  and validator as active epic promotion.
- [x] Any rejected file rejects the whole bundle.
- [x] No child files are written unless the whole interpreted bundle is valid.
- [x] Selected child remains the first valid child by numeric order, then path.

## Valid Split Success Order

1. Write all validated child epic files.
2. Write `.agents/bundle-manifest.md` with source prompt `SplitEpic`,
   projection path, expected file count, validation result `Valid`, and sorted
   file hashes.
3. Mark each child lifecycle `Draft` with note `Validated split child epic.`.
4. Capture HITL requests for each child.
5. Build split family with id `Guid.NewGuid().ToString("N")[..8]`.
6. Write `.agents/splits/split-family-{id}.json`.
7. Replace the prompt completion output with selected child content while
   preserving prompt correlation id, timing, and input snapshot.
8. Promote selected child as `.agents/epic.md` with lifecycle note
   `Promoted split child {selectedChild.Path} by SplitEpic.`.
9. Return promotion result.

## Split-Output Blocker

- [x] Invalid or blocked split output writes no child files and no split family.
- [x] Previous `.agents/epic.md` remains unchanged.
- [x] Evidence stem `split-epic-output`.
- [x] Evidence content includes reason, rejected files, and raw prompt output.
- [x] Lifecycle for evidence path `Blocked`.
- [x] Journal event `SplitBundleRejected`.
- [x] Decision `Split Epic Blocked` for blocked interpretation.
- [x] Decision `Split Bundle Rejected` otherwise.
- [x] State `EvidenceBlocked` / `Paused`.
- [x] Intent `ResolveSplitEpicBlocker`.
- [x] Next transition `Resolve blocker and rerun`.
- [x] Returned result is `ArtifactPromotionResult.NotPromoted(...)`.

## Active-Epic Promotion Rejection

- [x] Child files and split family may already exist.
- [x] Promotion rejection uses active epic promotion blocker behavior.
- [x] Event `ArtifactPromotionBlocked`.
- [x] State `EvidenceBlocked` / `Paused`.
- [x] Intent `ResolveArtifactPromotionBlocker`.
- [x] Caller pauses.

## Runtime Prompt Failure

- [x] State `EvidenceBlocked` / `Failed`.
- [x] Output `.agents/splits`.
- [x] Decision `Failed`.
- [x] Intent `ResolveTransitionFailure`.
- [x] Throw already-persisted failure.

## Input Snapshot

- [x] Required projection.
- [x] Required selection.
- [x] Secondary hash of selection content.

## Integration

- [x] Caller behavior remains unchanged: promoted split proceeds to milestone
  generation.
- [x] Caller behavior remains unchanged: not promoted pauses.
