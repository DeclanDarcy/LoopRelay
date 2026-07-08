# Milestone 7: Extract Milestone Deep Dive Generation

## Handler Method

```csharp
Task ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

## Boundary

- [ ] Handler returns after final `MilestoneSpecsReady` state is persisted.
- [ ] Caller returns paused.

## Prompt And Projection

- [ ] Phase `Generate milestone deep dives`.
- [ ] Runtime prompt `GenerateMilestoneDeepDivesForEpic`.
- [ ] Projection prompt `ProjectionForGenerateMilestoneDeepDivesForEpic`.
- [ ] State `ActiveEpicReady -> MilestoneSpecsReady`.
- [ ] Projection path `.agents/projections/milestone-deep-dive.md`.
- [ ] Required input `.agents/epic.md`.
- [ ] Secondary input is empty string.
- [ ] Prompt output path before materialization `.agents/specs`.
- [ ] Prompt envelope uses the promotion-candidate prompt envelope, followed by
  custom materialization finalization.

## Milestone Context Section Order

1. `Projection Content`
2. `Active Epic`

## Success Order

1. Ensure projection.
2. Build milestone context.
3. Run promotion-candidate prompt envelope.
4. Extract `# FILE:` bundle.
5. Reject blocked or zero-file bundle.
6. Write extracted files.
7. Write `.agents/specs/bundle-manifest.md`.
8. Mark each extracted spec lifecycle `Ready`.
9. Capture HITL requests for each spec.
10. Record execution-preparation provenance.
11. Run invariant validation for `MilestoneSpecsReady`.
12. Append `MilestoneSpecsMaterialized`.
13. Save final `MilestoneSpecsReady` / `Completed`, decision
    `Milestone Specs Ready`, output `.agents/specs`, replacement blockers `[]`,
    and empty transition intent.
14. Caller returns `RoadmapOutcome.Paused`.

## Important Success Details

- [ ] Successful prompt path uses `TransitionStarted` and `PromptCompleted`, not
  `TransitionCompleted`.
- [ ] Raw prompt output is not stored directly on success.
- [ ] Extracted milestone files are under `.agents/specs/*.md`.
- [ ] Execution-preparation generator id is
  `GenerateMilestoneDeepDivesForEpic:v1`.
- [ ] Final state has no next valid transitions.
- [ ] No decision-ledger entry is appended.
- [ ] No operational context, execution prompt, or execution plan is generated
  here.

## Bundle Or Post-Processing Failure After Prompt Completion

- [ ] Prompt-completed journal and state already exist.
- [ ] Write `.agents/evidence/blockers/milestone-spec-generation-failed.NNNN.md`
  with raw prompt output embedded in the failure evidence.
- [ ] Append `MilestoneSpecGenerationFailed`.
- [ ] Save `EvidenceBlocked` / `Paused`.
- [ ] Decision `Milestone Spec Generation Failed`.
- [ ] Intent `ResolveMilestoneSpecGenerationFailure`.
- [ ] Throw already-persisted failure.
- [ ] Do not roll back files, manifest, lifecycle, or provenance already written
  before the failure.

## Invariant Failure

- [ ] Specs, bundle manifest, lifecycle, and execution-preparation manifest may
  already exist.
- [ ] Use validator-owned evidence path when available, otherwise fallback
  evidence.
- [ ] Append `InvariantFailed`.
- [ ] Save using prompt `PostMilestoneInvariantValidation`.
- [ ] Intent `ResolveInvariantViolation`.
- [ ] Throw already-persisted failure.
- [ ] Do not append `MilestoneSpecsMaterialized`.

## Runtime Prompt Failure

- [ ] Append `TransitionFailed`.
- [ ] Save `EvidenceBlocked` / `Failed`.
- [ ] Decision `Runtime Failure`.
- [ ] Output `.agents/specs`.
- [ ] Intent `ResolveTransitionFailure`.

## Input Snapshot

- [ ] Required projection.
- [ ] Required active epic.
- [ ] Secondary hash of empty string.
