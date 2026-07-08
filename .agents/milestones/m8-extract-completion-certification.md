# Milestone 8: Extract Completion Certification

## Completion Handler Method

```csharp
Task<RoadmapOutcome> ExecuteAsync(
    ProjectContext projectContext,
    DateTimeOffset executionStarted,
    string executionEvidencePath,
    CancellationToken cancellationToken,
    bool persistCompletionClaim = true,
    ExecutionDispositionRoute? completionRoute = null)
```

## Completion Handler Boundary

- [ ] Handler returns `RoadmapOutcome`.
- [ ] Startup/resume selection of the call remains outside.

## Call Paths

- [ ] Preserve normal execution completion, where the completion claim state is
  persisted before evaluation.
- [ ] Preserve resume from persisted `EpicCompletionDetected`, where
  `persistCompletionClaim` is false and the evidence path was recovered before
  entry.
- [ ] Resume planner sees persisted `EpicCompletionDetected`.
- [ ] Caller recovers execution evidence from transition intent evidence paths
  and output paths, filtering to `.agents/evidence/execution`.
- [ ] First present candidate is used.
- [ ] If no execution evidence exists, throw
  `RoadmapStepException("Cannot resume completion certification because execution evidence is missing.")`.
- [ ] Live resume calls completion certification with
  `persistCompletionClaim: false`.

## Evaluation Prompt And Projection

- [ ] Runtime prompt `EvaluateEpicCompletionAndDrift`.
- [ ] Phase `Evaluate epic completion and drift`.
- [ ] Projection prompt `ProjectionForEvaluateEpicCompletionAndDrift`.
- [ ] Projection path `.agents/projections/epic-completion-evaluation.md`.
- [ ] State `EpicCompletionDetected -> CompletionEvaluationAndContextUpdate`.
- [ ] Output during prompt envelope `.agents/evidence/evaluations`.
- [ ] Numbered evidence stem `epic-completion-and-drift`.
- [ ] Secondary input is empty string.
- [ ] Envelope is the normal prompt transition.

## Optional Non-Implementation Review Gate

- [ ] Runs before evaluation phase and projection ensure.
- [ ] If blocked, write
  `.agents/evidence/blockers/non-implementation-completion-review-blocked.NNNN.md`.
- [ ] Output list is review evidence paths plus blocker path.
- [ ] Save `EvidenceBlocked` / `Paused`.
- [ ] From `EpicCompletionDetected` to `EvidenceBlocked`.
- [ ] Prompt `NonImplementationCompletionReview`.
- [ ] Projection `None`.
- [ ] Decision `Pending non-implementation HITL review`.
- [ ] Intent `ResolveNonImplementationCompletionReview`.
- [ ] Next transition text points at the non-implementation decisions file.
- [ ] Return `RoadmapOutcome.Paused`.

## Completion Evaluation Context Inputs

- [ ] Projection content.
- [ ] `.agents/epic.md`.
- [ ] Execution evidence path.
- [ ] Fresh milestone spec paths from execution-preparation provenance.
- [ ] Repository inspection instructions.
- [ ] Optional non-implementation review evidence sections when present.

## Required Transition Input Roles

- [ ] Projection.
- [ ] Active epic.
- [ ] Execution evidence.
- [ ] Fresh milestone specs.

## Evaluation Success Order

1. Run optional review gate.
2. Ensure evaluation projection.
3. Build evaluation context.
4. Run normal prompt envelope.
5. Write `.agents/evidence/evaluations/epic-completion-and-drift.NNNN.md`.
6. Capture HITL requests from evaluation evidence.
7. Parse completion evaluation.
8. Validate with completion certification policy.
9. Append evaluation decision-ledger entry.
10. If invalid, persist invalid-certification blocker and return paused.
11. Route valid certification.
12. Run close-route side effects when required.
13. Update active epic lifecycle according to route.
14. Persist final completion route.
15. Return mapped `RoadmapOutcome`.

## Parser And Policy Behavior

- [ ] Parser reads `## Evaluation Summary`.
- [ ] Parser requires `Overall Completion Status`,
  `Overall Drift Classification`, and `Closure Recommendation`.
- [ ] Parse failure happens after evaluation evidence and HITL capture and is
  not converted into invalid-certification blocker state.
- [ ] Policy failure after a successful parse is converted into durable invalid
  certification blocker state.

## Evaluation Decision Ledger Entry

- [ ] State `CompletionEvaluationAndContextUpdate`.
- [ ] Transition `EvaluateEpicCompletionAndDrift`.
- [ ] Projection `.agents/projections/epic-completion-evaluation.md`.
- [ ] Output: evaluation evidence path.
- [ ] Decision: parsed closure recommendation.
- [ ] Confidence `Unclear`.
- [ ] Rationale: parsed overall completion status.

## Invalid Certification

- [ ] Blocker evidence stem `invalid-completion-certification`.
- [ ] Required next step:
  `Review {evaluationPath}, preserve the certification evidence, correct the certification decision, and rerun the roadmap CLI.`
- [ ] Input snapshot prompt `CompletionCertificationRouting`.
- [ ] Event `CompletionCertificationRejected`.
- [ ] Prompt contract key `CompletionCertificationPolicy`.
- [ ] Outputs: evaluation path and blocker path.
- [ ] State `EvidenceBlocked` / `Paused`.
- [ ] From/to `CompletionEvaluationAndContextUpdate -> EvidenceBlocked`.
- [ ] Prompt `CompletionCertificationRouting`.
- [ ] Decision `Invalid Completion Certification`.
- [ ] Intent `ResolveInvalidCompletionCertification`.
- [ ] Next transition `Resolve invalid completion certification and rerun`.
- [ ] Return `RoadmapOutcome.Paused`.

## Valid Route Mapping

- [ ] `Close Epic` and `Close With Follow-Up`: target
  `SelectNextStrategicInitiative`, status `Completed`, CLI outcome
  `Completed`, active epic lifecycle `Completed`, next transition
  `SelectNextEpic`, and requires roadmap completion context update.
- [ ] `Continue Epic`: target `ExecutionLoop`, status `Paused`, CLI outcome
  `Paused`, active epic lifecycle `Executing`, next transition
  `ContinueExecution`.
- [ ] `Reopen Epic`: target `EpicPreparationAudit`, status `Paused`, CLI
  outcome `Paused`, active epic lifecycle `Ready`, next transition
  `EpicPreparationAudit`.
- [ ] `Gather More Evidence`: target `EvidenceGathering`, status `Paused`, CLI
  outcome `Paused`, active epic lifecycle `Ready`, next transitions
  `GatherAdditionalEvidence` and `EvaluateEpicCompletionAndDrift`.

## Final Route Persistence

- [ ] Routing input snapshot uses prompt `CompletionCertificationRouting` and
  the evaluation evidence path as required completion-evaluation input.
- [ ] Append `TransitionCompleted` from
  `CompletionEvaluationAndContextUpdate` to route target state.
- [ ] Prompt `CompletionCertificationRouting`.
- [ ] Prompt contract key `CompletionCertificationRouter`.
- [ ] Save route target state, route transition status, route outputs, route
  decision, route transition intent, and route next transitions.
- [ ] Final route persistence occurs after lifecycle and close-route update
  effects.

## RoadmapCompletionContextUpdateTransition

This helper runs only from close routes and should not become an independent CLI
route.

### Helper Boundary

- [ ] Called only by completion close routes.

### Prerequisite Close-Route Effects

- [ ] Archive completed execution workspace first.
- [ ] Synthesize completed epic.
- [ ] Archive directory is `.agents/archive/epics/{index}`.
- [ ] Synthesis path is `.agents/archive/epics/{index}.md`.
- [ ] Archive index is existing archive directory count plus one.
- [ ] Archive service reports phases `Archive completed execution workspace`
  and `Synthesize completed epic`.

### Update Prompt

- [ ] Phase `Update roadmap completion context`.
- [ ] Runtime prompt `UpdateRoadmapCompletionContext`.
- [ ] Projection prompt `ProjectionForUpdateRoadmapCompletionContext`.
- [ ] Projection path `.agents/projections/roadmap-completion-update.md`.
- [ ] From/to `CompletionEvaluationAndContextUpdate -> SelectNextStrategicInitiative`.
- [ ] Output `.agents/core/roadmap-completion-context.md`.
- [ ] Secondary input is completed-epic synthesis content.
- [ ] Input context is completion evaluation evidence path.
- [ ] Envelope is the normal prompt transition.

### Update Context Inputs

- [ ] Projection content.
- [ ] Current `.agents/core/roadmap-completion-context.md`.
- [ ] Completed-epic synthesis content and path.
- [ ] Latest completion evaluation evidence.
- [ ] Repository inspection instructions.
- [ ] Optional non-implementation review evidence sections when present.

### Update Success Order

1. Ensure projection.
2. Build completion-update context.
3. Run normal prompt envelope.
4. Write `.agents/core/roadmap-completion-context.md`.
5. Capture HITL requests from rewritten completion context.
6. Write `.agents/evidence/evaluations/roadmap-completion-update.NNNN.md`.
7. Supersede active selection with `RoadmapCompletionContextDrift`.
8. Append decision `Roadmap Completion Context Updated`.

### Update Decision Ledger Entry

- [ ] State `CompletionEvaluationAndContextUpdate`.
- [ ] Transition/prompt `UpdateRoadmapCompletionContext`.
- [ ] Projection `.agents/projections/roadmap-completion-update.md`.
- [ ] Output `.agents/core/roadmap-completion-context.md`.
- [ ] Decision `Roadmap Completion Context Updated`.
- [ ] Confidence `Unclear`.
- [ ] Rationale `Completion context updated after certification.`.

### Close-Route Output List

- [ ] Final close-route output list includes evaluation path,
  `.agents/core/roadmap-completion-context.md`, and completed-epic synthesis
  path.
- [ ] Final close-route output list does not include numbered
  `roadmap-completion-update.NNNN.md` evidence.

## Active Epic Lifecycle

- [ ] Update active epic lifecycle according to route.

## Failure Handling

- [ ] Prompt runtime failure remains already persisted.
- [ ] Archive and synthesis failures are not converted into
  invalid-certification blockers.

## Input Snapshot

- [ ] `EvaluateEpicCompletionAndDrift`: required projection, active epic,
  execution evidence path, and fresh milestone specs; secondary hash of empty
  string.
- [ ] `UpdateRoadmapCompletionContext`: required projection, roadmap completion
  context, active epic, and completion evaluation evidence; secondary hash of
  completed-epic synthesis content.
- [ ] `CompletionCertificationRouting`: completion evaluation evidence path is
  the required input used to anchor route and invalid-certification journal
  records.

## Integration

- [ ] Move `UpdateRoadmapCompletionContextAsync` into
  `RoadmapCompletionContextUpdateTransition`.
- [ ] Call `RoadmapCompletionContextUpdateTransition` only from the completion
  handler.
