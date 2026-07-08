# Milestone 6: Extract Split Epic

## Handler Method

```csharp
Task<ArtifactPromotionResult> ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

## Boundary

- [ ] Handler returns `ArtifactPromotionResult`.
- [ ] Caller controls milestone continuation.

## Prompt And Projection

- [ ] Phase `Split epic`.
- [ ] Runtime prompt `SplitEpic`.
- [ ] Projection prompt `ProjectionForSplitEpic`.
- [ ] From/to for prompt `SplitEpicProposed -> SplitChildSelection`.
- [ ] Projection path `.agents/projections/split-epic.md`.
- [ ] Prompt output path in state/journal `.agents/splits`.
- [ ] Secondary input is active selection content.
- [ ] Envelope is the normal prompt transition.

## Required Precondition

- [ ] Read and validate fresh active selection through `ActiveSelectionReader`.

## Runtime Context

- [ ] Runtime context is `BuildCreateOrSplitContext` with projection content,
  selection proposal, and repository inspection instructions.

## Bundle Extraction And Interpretation

- [ ] Use `BundleExtractionPolicy.RepositorySafe`.
- [ ] Parse `# FILE:` markers.
- [ ] Normalize separators.
- [ ] Reject rooted paths and parent traversal.
- [ ] Reject duplicate targets by throwing `RoadmapStepException`, then convert
  that exception to invalid split interpretation.
- [ ] Trim only leading and trailing separator noise around file bodies.
- [ ] Hash each extracted file body.
- [ ] Interpreter requires paths matching `.agents/epic-N.md`.
- [ ] Child content classification and validation use the same epic classifier
  and validator as active epic promotion.
- [ ] Any rejected file rejects the whole bundle.
- [ ] No child files are written unless the whole interpreted bundle is valid.
- [ ] Selected child remains the first valid child by numeric order, then path.

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

- [ ] Invalid or blocked split output writes no child files and no split family.
- [ ] Previous `.agents/epic.md` remains unchanged.
- [ ] Evidence stem `split-epic-output`.
- [ ] Evidence content includes reason, rejected files, and raw prompt output.
- [ ] Lifecycle for evidence path `Blocked`.
- [ ] Journal event `SplitBundleRejected`.
- [ ] Decision `Split Epic Blocked` for blocked interpretation.
- [ ] Decision `Split Bundle Rejected` otherwise.
- [ ] State `EvidenceBlocked` / `Paused`.
- [ ] Intent `ResolveSplitEpicBlocker`.
- [ ] Next transition `Resolve blocker and rerun`.
- [ ] Returned result is `ArtifactPromotionResult.NotPromoted(...)`.

## Active-Epic Promotion Rejection

- [ ] Child files and split family may already exist.
- [ ] Promotion rejection uses active epic promotion blocker behavior.
- [ ] Event `ArtifactPromotionBlocked`.
- [ ] State `EvidenceBlocked` / `Paused`.
- [ ] Intent `ResolveArtifactPromotionBlocker`.
- [ ] Caller pauses.

## Runtime Prompt Failure

- [ ] State `EvidenceBlocked` / `Failed`.
- [ ] Output `.agents/splits`.
- [ ] Decision `Failed`.
- [ ] Intent `ResolveTransitionFailure`.
- [ ] Throw already-persisted failure.

## Input Snapshot

- [ ] Required projection.
- [ ] Required selection.
- [ ] Secondary hash of selection content.

## Integration

- [ ] Caller behavior remains unchanged: promoted split proceeds to milestone
  generation.
- [ ] Caller behavior remains unchanged: not promoted pauses.
