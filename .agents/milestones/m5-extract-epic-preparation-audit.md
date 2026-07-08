# Milestone 5: Extract Epic Preparation Audit

## Handler Method

```csharp
Task<EpicPreparationResult> ExecuteAsync(
    SelectionDecision selectionDecision,
    ProjectContext projectContext,
    CancellationToken cancellationToken)
```

## Preserve

- [ ] Phase `Audit selected epic`.
- [ ] Fresh active selection validation before audit prompt execution.
- [ ] Prompt `EpicPreparationAudit`.
- [ ] State `ExistingEpicSelected -> EpicPreparationAudit`.
- [ ] Projection `.agents/projections/epic-preparation-audit.md`.
- [ ] Audit evidence stem `epic-preparation-audit`.
- [ ] HITL capture from audit evidence.
- [ ] Audit decision ledger entry before branch routing.

## Retire Branch

- [ ] Create `RetiredEpic` from selection and audit.
- [ ] Upsert retired epics.
- [ ] Append retire decision.
- [ ] Save `RetireEpic` / `Completed`.
- [ ] Supersede selection provenance with `RetiredEpicStateDrift`.
- [ ] Mark `.agents/selection.md` `Superseded`.
- [ ] Return `EpicPreparationResult.Retired`.

## Insufficient-Evidence Branch

- [ ] Throw after audit evidence and audit decision persistence.
- [ ] Do not write a new durable blocker inside the branch.

## Realign And Reimagine Branches

- [ ] Delegate to `ActiveEpicRewriteTransition`.
- [ ] Return `ActiveEpicReady` when promoted.
- [ ] Return `Blocked` when not promoted.

## Integration

- [ ] Keep `ContinueAfterSelectionAsync` mapping `Retired` and `Blocked` to `RoadmapOutcome.Paused`.
- [ ] Keep `ActiveEpicReady` falling through to milestone generation.
