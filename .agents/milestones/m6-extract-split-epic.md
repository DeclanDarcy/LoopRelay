# Milestone 6: Extract Split Epic

## Handler Method

```csharp
Task<ArtifactPromotionResult> ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

## Preserve

- [ ] Phase `Split epic`.
- [ ] Fresh active selection validation.
- [ ] Prompt `SplitEpic`.
- [ ] State `SplitEpicProposed -> SplitChildSelection`.
- [ ] Projection `.agents/projections/split-epic.md`.
- [ ] Output path `.agents/splits`.
- [ ] Bundle extraction policy `RepositorySafe`.
- [ ] Invalid extraction converted to split-bundle interpretation failure.
- [ ] Split interpreter requiring `.agents/epic-N.md` paths.
- [ ] Whole-bundle rejection before any child file is written.
- [ ] Selected child remains the first validated child by numeric child order.
- [ ] Child writes after complete validation only.
- [ ] `.agents/bundle-manifest.md` shape and validation result `Valid`.
- [ ] Child lifecycle `Draft` with note `Validated split child epic.`.
- [ ] HITL capture for each child.
- [ ] Split family JSON under `.agents/splits/split-family-{id}.json`.
- [ ] Family id shape `Guid.NewGuid().ToString("N")[..8]`.
- [ ] Child promotion uses selected child content, not raw prompt output.
- [ ] Original prompt correlation id, timing, and input snapshot reused for promotion journal records.
- [ ] Successful promotion writes `.agents/epic.md`, appends `ArtifactPromoted`, and saves `ActiveEpicReady`.

## Split-Output Blocker

- [ ] Evidence stem `split-epic-output`.
- [ ] Decision `Split Epic Blocked` for blocked interpretation.
- [ ] Decision `Split Bundle Rejected` otherwise.
- [ ] Event `SplitBundleRejected`.
- [ ] Lifecycle `Blocked`.
- [ ] State `EvidenceBlocked` / `Paused`.
- [ ] Intent `ResolveSplitEpicBlocker`.

## Active-Epic Promotion Rejection

- [ ] Event `ArtifactPromotionBlocked`.
- [ ] State `EvidenceBlocked` / `Paused`.
- [ ] Intent `ResolveArtifactPromotionBlocker`.

## Runtime Prompt Failure

- [ ] State `EvidenceBlocked` / `Failed`.
- [ ] Intent `ResolveTransitionFailure`.

## Integration

- [ ] Caller behavior remains unchanged: promoted split proceeds to milestone generation.
- [ ] Caller behavior remains unchanged: not promoted pauses.
