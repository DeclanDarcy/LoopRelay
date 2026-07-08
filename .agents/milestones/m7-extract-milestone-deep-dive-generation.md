# Milestone 7: Extract Milestone Deep Dive Generation

## Handler Method

```csharp
Task ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
```

## Preserve

- [ ] Phase `Generate milestone deep dives`.
- [ ] Prompt `GenerateMilestoneDeepDivesForEpic`.
- [ ] State `ActiveEpicReady -> MilestoneSpecsReady`.
- [ ] Projection `.agents/projections/milestone-deep-dive.md`.
- [ ] Required input `.agents/epic.md`.
- [ ] Empty secondary input.
- [ ] Prompt envelope uses `TransitionStarted` and `PromptCompleted`, not `TransitionCompleted`.
- [ ] Output path before materialization `.agents/specs`.
- [ ] Raw prompt output not stored on success.
- [ ] Extracted milestone files under `.agents/specs/*.md`.
- [ ] `.agents/specs/bundle-manifest.md`.
- [ ] Lifecycle `Ready` for each spec.
- [ ] Optional HITL capture for each spec.
- [ ] Execution-preparation generator id `GenerateMilestoneDeepDivesForEpic:v1`.
- [ ] Invariant validation after spec materialization and execution-preparation provenance.
- [ ] Final event `MilestoneSpecsMaterialized`.
- [ ] Final state `MilestoneSpecsReady` / `Completed`, decision `Milestone Specs Ready`.
- [ ] No decision-ledger entry on success.
- [ ] No operational context or execution prompt generated here.

## Failure Branches

- [ ] Runtime prompt failure remains `EvidenceBlocked` / `Failed`, decision `Runtime Failure`, intent `ResolveTransitionFailure`.
- [ ] Bundle extraction or materialization failure writes `.agents/evidence/blockers/milestone-spec-generation-failed.NNNN.md`, event `MilestoneSpecGenerationFailed`, state `EvidenceBlocked` / `Paused`, intent `ResolveMilestoneSpecGenerationFailure`.
- [ ] Invariant failure uses validator evidence or fallback evidence, event `InvariantFailed`, prompt `PostMilestoneInvariantValidation`, intent `ResolveInvariantViolation`.
- [ ] No rollback of files already written before post-prompt or invariant failures.
