# Milestone 8: Extract Completion Certification

## Handler Method

```csharp
Task<RoadmapOutcome> ExecuteAsync(
    ProjectContext projectContext,
    DateTimeOffset executionStarted,
    string executionEvidencePath,
    CancellationToken cancellationToken,
    bool persistCompletionClaim = true,
    ExecutionDispositionRoute? completionRoute = null)
```

## Call Paths

- [ ] Preserve normal execution completion, where the completion claim state is persisted before evaluation.
- [ ] Preserve resume from persisted completion claim, where `persistCompletionClaim` is false and the evidence path was recovered before entry.

## Preserve

- [ ] Optional non-implementation completion-review gate before evaluation prompt.
- [ ] Gate blocker evidence stem `non-implementation-completion-review-blocked`.
- [ ] Prompt `EvaluateEpicCompletionAndDrift`.
- [ ] State `EpicCompletionDetected -> CompletionEvaluationAndContextUpdate`.
- [ ] Projection `.agents/projections/epic-completion-evaluation.md`.
- [ ] Evaluation evidence stem `epic-completion-and-drift`.
- [ ] HITL capture from evaluation evidence before parsing.
- [ ] Parse failure after evidence write is not converted to invalid-certification blocker.
- [ ] Completion policy validation after parse.
- [ ] Evaluation decision ledger append.

## Invalid Certification

- [ ] Blocker evidence stem `invalid-completion-certification`.
- [ ] Event `CompletionCertificationRejected`.
- [ ] Prompt `CompletionCertificationRouting`.
- [ ] Contract key `CompletionCertificationPolicy`.
- [ ] State `EvidenceBlocked` / `Paused`.
- [ ] Decision `Invalid Completion Certification`.
- [ ] Intent `ResolveInvalidCompletionCertification`.
- [ ] Return `RoadmapOutcome.Paused`.

## Valid Route Mapping

- [ ] Map valid routes through `CompletionCertificationRouter` and `RoadmapCompletionRouteMapper`.

## Close Routes

- [ ] Archive completed execution workspace before completion-context update.
- [ ] Synthesize completed epic.
- [ ] Prompt `UpdateRoadmapCompletionContext`.
- [ ] Projection `.agents/projections/roadmap-completion-update.md`.
- [ ] State `CompletionEvaluationAndContextUpdate -> SelectNextStrategicInitiative`.
- [ ] Rewrite `.agents/core/roadmap-completion-context.md`.
- [ ] HITL capture from rewritten completion context.
- [ ] Write numbered `roadmap-completion-update` evidence.
- [ ] Supersede active selection because completion context changed.
- [ ] Append `Roadmap Completion Context Updated` decision.
- [ ] Final route output list includes evaluation path, `.agents/core/roadmap-completion-context.md`, and synthesis path, but not the numbered update evidence.

## Active Epic Lifecycle

- [ ] Update active epic lifecycle according to route.

## Final Route Persistence

- [ ] Close routes target `SelectNextStrategicInitiative`, status `Completed`, next `SelectNextEpic`.
- [ ] Continue route target `ExecutionLoop`, status `Paused`, next `ContinueExecution`.
- [ ] Reopen route target `EpicPreparationAudit`, status `Paused`, next `EpicPreparationAudit`.
- [ ] Gather-more-evidence route target `EvidenceGathering`, status `Paused`, next `GatherAdditionalEvidence` and `EvaluateEpicCompletionAndDrift`.

## Failure Handling

- [ ] Prompt runtime failure remains already persisted.
- [ ] Archive and synthesis failures are not converted into invalid-certification blockers.

## Integration

- [ ] Move `UpdateRoadmapCompletionContextAsync` into `RoadmapCompletionContextUpdateTransition`.
- [ ] Call `RoadmapCompletionContextUpdateTransition` only from the completion handler.
