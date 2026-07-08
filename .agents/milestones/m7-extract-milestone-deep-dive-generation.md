# Milestone 7: Extract Milestone Deep Dive Generation

## Handler Method

```csharp
Task ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

## Boundary

- [x] Handler returns after final `MilestoneSpecsReady` state is persisted.
- [x] Caller returns paused.

## Prompt And Projection

- [x] Phase `Generate milestone deep dives`.
- [x] Runtime prompt `GenerateMilestoneDeepDivesForEpic`.
- [x] Projection prompt `ProjectionForGenerateMilestoneDeepDivesForEpic`.
- [x] State `ActiveEpicReady -> MilestoneSpecsReady`.
- [x] Projection path `.agents/projections/milestone-deep-dive.md`.
- [x] Required input `.agents/epic.md`.
- [x] Secondary input is empty string.
- [x] Prompt output path before materialization `.agents/specs`.
- [x] Prompt envelope uses the promotion-candidate prompt envelope, followed by
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

- [x] Successful prompt path uses `TransitionStarted` and `PromptCompleted`, not
  `TransitionCompleted`.
- [x] Raw prompt output is not stored directly on success.
- [x] Extracted milestone files are under `.agents/specs/*.md`.
- [x] Execution-preparation generator id is
  `GenerateMilestoneDeepDivesForEpic:v1`.
- [x] Final state has no next valid transitions.
- [x] No decision-ledger entry is appended.
- [x] No operational context, execution prompt, or execution plan is generated
  here.

## Bundle Or Post-Processing Failure After Prompt Completion

- [x] Prompt-completed journal and state already exist.
- [x] Write `.agents/evidence/blockers/milestone-spec-generation-failed.NNNN.md`
  with raw prompt output embedded in the failure evidence.
- [x] Append `MilestoneSpecGenerationFailed`.
- [x] Save `EvidenceBlocked` / `Paused`.
- [x] Decision `Milestone Spec Generation Failed`.
- [x] Intent `ResolveMilestoneSpecGenerationFailure`.
- [x] Throw already-persisted failure.
- [x] Do not roll back files, manifest, lifecycle, or provenance already written
  before the failure.

## Invariant Failure

- [x] Specs, bundle manifest, lifecycle, and execution-preparation manifest may
  already exist.
- [x] Use validator-owned evidence path when available, otherwise fallback
  evidence.
- [x] Append `InvariantFailed`.
- [x] Save using prompt `PostMilestoneInvariantValidation`.
- [x] Intent `ResolveInvariantViolation`.
- [x] Throw already-persisted failure.
- [x] Do not append `MilestoneSpecsMaterialized`.

## Runtime Prompt Failure

- [x] Append `TransitionFailed`.
- [x] Save `EvidenceBlocked` / `Failed`.
- [x] Decision `Runtime Failure`.
- [x] Output `.agents/specs`.
- [x] Intent `ResolveTransitionFailure`.

## Input Snapshot

- [x] Required projection.
- [x] Required active epic.
- [x] Secondary hash of empty string.
