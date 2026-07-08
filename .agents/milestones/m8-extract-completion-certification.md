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

- [x] Handler returns `RoadmapOutcome`.
- [x] Startup/resume selection of the call remains outside.

## Call Paths

- [x] Preserve normal execution completion, where the completion claim state is
  persisted before evaluation.
- [x] Preserve resume from persisted `EpicCompletionDetected`, where
  `persistCompletionClaim` is false and the evidence path was recovered before
  entry.
- [x] Resume planner sees persisted `EpicCompletionDetected`.
- [x] Caller recovers execution evidence from transition intent evidence paths
  and output paths, filtering to `.agents/evidence/execution`.
- [x] First present candidate is used.
- [x] If no execution evidence exists, throw
  `RoadmapStepException("Cannot resume completion certification because execution evidence is missing.")`.
- [x] Live resume calls completion certification with
  `persistCompletionClaim: false`.

## Evaluation Prompt And Projection

- [x] Runtime prompt `EvaluateEpicCompletionAndDrift`.
- [x] Phase `Evaluate epic completion and drift`.
- [x] Projection prompt `ProjectionForEvaluateEpicCompletionAndDrift`.
- [x] Projection path `.agents/projections/epic-completion-evaluation.md`.
- [x] State `EpicCompletionDetected -> CompletionEvaluationAndContextUpdate`.
- [x] Output during prompt envelope `.agents/evidence/evaluations`.
- [x] Numbered evidence stem `epic-completion-and-drift`.
- [x] Secondary input is empty string.
- [x] Envelope is the normal prompt transition.

## Optional Non-Implementation Review Gate

- [x] Runs before evaluation phase and projection ensure.
- [x] If blocked, write
  `.agents/evidence/blockers/non-implementation-completion-review-blocked.NNNN.md`.
- [x] Output list is review evidence paths plus blocker path.
- [x] Save `EvidenceBlocked` / `Paused`.
- [x] From `EpicCompletionDetected` to `EvidenceBlocked`.
- [x] Prompt `NonImplementationCompletionReview`.
- [x] Projection `None`.
- [x] Decision `Pending non-implementation HITL review`.
- [x] Intent `ResolveNonImplementationCompletionReview`.
- [x] Next transition text points at the non-implementation decisions file.
- [x] Return `RoadmapOutcome.Paused`.

## Completion Evaluation Context Inputs

- [x] Projection content.
- [x] `.agents/epic.md`.
- [x] Execution evidence path.
- [x] Fresh milestone spec paths from execution-preparation provenance.
- [x] Repository inspection instructions.
- [x] Optional non-implementation review evidence sections when present.

## Required Transition Input Roles

- [x] Projection.
- [x] Active epic.
- [x] Execution evidence.
- [x] Fresh milestone specs.

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

- [x] Parser reads `## Evaluation Summary`.
- [x] Parser requires `Overall Completion Status`,
  `Overall Drift Classification`, and `Closure Recommendation`.
- [x] Parse failure happens after evaluation evidence and HITL capture and is
  not converted into invalid-certification blocker state.
- [x] Policy failure after a successful parse is converted into durable invalid
  certification blocker state.

## Evaluation Decision Ledger Entry

- [x] State `CompletionEvaluationAndContextUpdate`.
- [x] Transition `EvaluateEpicCompletionAndDrift`.
- [x] Projection `.agents/projections/epic-completion-evaluation.md`.
- [x] Output: evaluation evidence path.
- [x] Decision: parsed closure recommendation.
- [x] Confidence `Unclear`.
- [x] Rationale: parsed overall completion status.

## Invalid Certification

- [x] Blocker evidence stem `invalid-completion-certification`.
- [x] Required next step:
  `Review {evaluationPath}, preserve the certification evidence, correct the certification decision, and rerun the roadmap CLI.`
- [x] Input snapshot prompt `CompletionCertificationRouting`.
- [x] Event `CompletionCertificationRejected`.
- [x] Prompt contract key `CompletionCertificationPolicy`.
- [x] Outputs: evaluation path and blocker path.
- [x] State `EvidenceBlocked` / `Paused`.
- [x] From/to `CompletionEvaluationAndContextUpdate -> EvidenceBlocked`.
- [x] Prompt `CompletionCertificationRouting`.
- [x] Decision `Invalid Completion Certification`.
- [x] Intent `ResolveInvalidCompletionCertification`.
- [x] Next transition `Resolve invalid completion certification and rerun`.
- [x] Return `RoadmapOutcome.Paused`.

## Valid Route Mapping

- [x] `Close Epic` and `Close With Follow-Up`: target
  `SelectNextStrategicInitiative`, status `Completed`, CLI outcome
  `Completed`, active epic lifecycle `Completed`, next transition
  `SelectNextEpic`, and requires roadmap completion context update.
- [x] `Continue Epic`: target `ExecutionLoop`, status `Paused`, CLI outcome
  `Paused`, active epic lifecycle `Executing`, next transition
  `ContinueExecution`.
- [x] `Reopen Epic`: target `EpicPreparationAudit`, status `Paused`, CLI
  outcome `Paused`, active epic lifecycle `Ready`, next transition
  `EpicPreparationAudit`.
- [x] `Gather More Evidence`: target `EvidenceGathering`, status `Paused`, CLI
  outcome `Paused`, active epic lifecycle `Ready`, next transitions
  `GatherAdditionalEvidence` and `EvaluateEpicCompletionAndDrift`.

## Final Route Persistence

- [x] Routing input snapshot uses prompt `CompletionCertificationRouting` and
  the evaluation evidence path as required completion-evaluation input.
- [x] Append `TransitionCompleted` from
  `CompletionEvaluationAndContextUpdate` to route target state.
- [x] Prompt `CompletionCertificationRouting`.
- [x] Prompt contract key `CompletionCertificationRouter`.
- [x] Save route target state, route transition status, route outputs, route
  decision, route transition intent, and route next transitions.
- [x] Final route persistence occurs after lifecycle and close-route update
  effects.

## RoadmapCompletionContextUpdateTransition

This helper runs only from close routes and should not become an independent CLI
route.

### Helper Boundary

- [x] Called only by completion close routes.

### Prerequisite Close-Route Effects

- [x] Archive completed execution workspace first.
- [x] Synthesize completed epic.
- [x] Archive directory is `.agents/archive/epics/{index}`.
- [x] Synthesis path is `.agents/archive/epics/{index}.md`.
- [x] Archive index is existing archive directory count plus one.
- [x] Archive service reports phases `Archive completed execution workspace`
  and `Synthesize completed epic`.

### Update Prompt

- [x] Phase `Update roadmap completion context`.
- [x] Runtime prompt `UpdateRoadmapCompletionContext`.
- [x] Projection prompt `ProjectionForUpdateRoadmapCompletionContext`.
- [x] Projection path `.agents/projections/roadmap-completion-update.md`.
- [x] From/to `CompletionEvaluationAndContextUpdate -> SelectNextStrategicInitiative`.
- [x] Output `.agents/core/roadmap-completion-context.md`.
- [x] Secondary input is completed-epic synthesis content.
- [x] Input context is completion evaluation evidence path.
- [x] Envelope is the normal prompt transition.

### Update Context Inputs

- [x] Projection content.
- [x] Current `.agents/core/roadmap-completion-context.md`.
- [x] Completed-epic synthesis content and path.
- [x] Latest completion evaluation evidence.
- [x] Repository inspection instructions.
- [x] Optional non-implementation review evidence sections when present.

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

- [x] State `CompletionEvaluationAndContextUpdate`.
- [x] Transition/prompt `UpdateRoadmapCompletionContext`.
- [x] Projection `.agents/projections/roadmap-completion-update.md`.
- [x] Output `.agents/core/roadmap-completion-context.md`.
- [x] Decision `Roadmap Completion Context Updated`.
- [x] Confidence `Unclear`.
- [x] Rationale `Completion context updated after certification.`.

### Close-Route Output List

- [x] Final close-route output list includes evaluation path,
  `.agents/core/roadmap-completion-context.md`, and completed-epic synthesis
  path.
- [x] Final close-route output list does not include numbered
  `roadmap-completion-update.NNNN.md` evidence.

## Active Epic Lifecycle

- [x] Update active epic lifecycle according to route.

## Failure Handling

- [x] Prompt runtime failure remains already persisted.
- [x] Archive and synthesis failures are not converted into
  invalid-certification blockers.

## Input Snapshot

- [x] `EvaluateEpicCompletionAndDrift`: required projection, active epic,
  execution evidence path, and fresh milestone specs; secondary hash of empty
  string.
- [x] `UpdateRoadmapCompletionContext`: required projection, roadmap completion
  context, active epic, and completion evaluation evidence; secondary hash of
  completed-epic synthesis content.
- [x] `CompletionCertificationRouting`: completion evaluation evidence path is
  the required input used to anchor route and invalid-certification journal
  records.

## Integration

- [x] Move `UpdateRoadmapCompletionContextAsync` into
  `RoadmapCompletionContextUpdateTransition`.
- [x] Call `RoadmapCompletionContextUpdateTransition` only from the completion
  handler.
