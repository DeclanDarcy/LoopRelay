# Milestone 2: Extract Shared Services Without Moving Transitions

## Work Items

- [ ] Move existing private helper behavior into the shared services listed in
  the plan.
- [ ] Keep `RoadmapStateMachine` calling the same operations through the new
  services.
- [ ] Do not move transition bodies yet.

## Acceptance

- [ ] `RoadmapStateMachine` behavior is unchanged.
- [x] Existing constructor wiring is updated in `RoadmapCliComposition` and
  `StateMachineFactory`.
- [x] No handler is introduced until shared helper tests pass.
- [x] Roadmap CLI tests pass after each helper extraction.

## RoadmapTransitionPersistence

This service owns the existing `SaveStateAsync` behavior, not just a thin store
write.

### Required Behavior

- [x] Load existing roadmap state before each save.
- [x] Load projection manifest and compute valid, stale, and invalid counts.
- [x] Read active artifact status rows for roadmap completion context,
  selection, and active epic.
- [x] Read the current last decision id.
- [x] Preserve existing retired epics unless replacements are provided.
- [x] Preserve existing blockers unless replacements are provided.
- [x] Count `.agents/splits/split-family-*.json`.
- [x] Preserve existing transition intent unless a replacement intent is
  provided.
- [x] Compute default next transitions when an explicit list is not supplied.
- [x] Preserve output formatting exactly as callers provide it, including joined
  comma-separated output lists.
- [ ] Workflow-failure helpers persist the same current state, status, prompt,
  output path, blocker rows, recovery intent, evidence paths, and
  next-transition text as the current state machine.

### Boundary

- [x] Do not move transition-specific decisions into this service.
- [x] Callers pass the exact state, status, from/to state, prompt, projection,
  output, decision, timestamps, blocker rows, transition intent, and next
  transitions.

## RoadmapPromptTransitionRunner

The plan names this service, but the extraction must keep the existing envelope
variants distinct.

### Normal Prompt Transition Envelope

Used by bootstrap, selection, epic preparation audit, completion evaluation, and
completion-context update.

- [ ] Resolve transition input snapshot first.
- [ ] Save started state before `TransitionStarted`.
- [ ] Started state uses current state equal to the target state, status
  `Started`, decision `Pending`, and the runtime prompt output path.
- [ ] Append `TransitionStarted`.
- [ ] Run runtime prompt.
- [ ] Append `TransitionCompleted`.
- [ ] Save completed state after the completion journal record.
- [ ] Completed state uses status `Completed` and decision `Completed`.
- [ ] On non-cancellation runtime failure, append `TransitionFailed`, save
  `EvidenceBlocked` with status `Failed`, intent `ResolveTransitionFailure`,
  next transition `Resolve blocker and rerun`, and throw
  `RoadmapStepException.AlreadyPersisted`.

### Promotion-Candidate Prompt Envelope

Used by `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`, and by milestone prompt
generation before post-processing.

- [ ] Resolve transition input snapshot first.
- [ ] Save started state before `TransitionStarted`.
- [ ] Current state remains the source state while the prompt is running.
- [ ] Started state uses decision `Prompt Started`.
- [ ] Append `TransitionStarted`.
- [ ] Run runtime prompt.
- [ ] Append `PromptCompleted` with parser decision `Output produced`.
- [ ] Save prompt-completed state with status `PromptCompleted` and decision
  `Prompt Completed`.
- [ ] Return `PromptTransitionCompletion` with correlation id,
  started/completed timestamps, elapsed milliseconds, raw output, and the
  original input snapshot.
- [ ] On non-cancellation runtime failure, append `TransitionFailed`, save
  `EvidenceBlocked` with status `Failed`, decision `Runtime Failure`, intent
  `ResolveTransitionFailure`, next transition `Resolve blocker and rerun`, and
  throw `RoadmapStepException.AlreadyPersisted`.

### Milestone Materialization

- [ ] Milestone materialization uses the promotion-candidate prompt envelope for
  prompt start and prompt completion.
- [ ] Success finalization is not `ArtifactPromoted`.
- [ ] Append `MilestoneSpecsMaterialized` and save `MilestoneSpecsReady` only
  after bundle extraction, spec writes, lifecycle/HITL,
  execution-preparation provenance, and invariant validation.

## ActiveSelectionReader

This service must do more than read `.agents/selection.md`.

### Required Sequence

1. Check cancellation.
2. Read required `.agents/selection.md`.
3. Read `.agents/projections/select-next-epic.md`.
4. If the selection projection is missing or blank, throw
   `RoadmapStepException("Active selection cannot be used because its SelectNextEpic projection is missing.")`.
5. Load persisted roadmap state.
6. Rebuild the current `SelectNextEpic` cycle from selection projection,
   roadmap completion context, roadmap source references, retired epics, empty
   secondary input, and transition input hashes.
7. Evaluate active selection freshness against
   `.agents/selection-provenance-manifest.json` and the current selection hash.
8. Throw a `RoadmapStepException` with stale reasons when freshness is not
   fresh.
9. Return selection content only after freshness is fresh.

### Consumers

- [ ] `CreateNewEpic`
- [ ] `SplitEpic`
- [ ] `EpicPreparationAudit`
- [ ] `RealignEpic`/`ReimagineEpic` fallback path when `.agents/epic.md` is
  absent

## DecisionRecorder

This service preserves the current `AppendDecisionAsync` fields and id
allocation.

- [ ] Allocate ids through `DecisionLedgerStore.NextDecisionIdAsync`.
- [ ] Append through the existing decision ledger store.
- [ ] Do not add decision entries to `CreateNewEpic`, `RealignEpic`,
  `ReimagineEpic`, `SplitEpic`, or successful milestone generation, because
  they currently do not append their own decision-ledger entries.
- [ ] Keep output path lists exactly as the current call site supplies them.

## HitlArtifactCapture

This wrapper must:

- [ ] Return immediately if the optional capture service is absent.
- [ ] Return immediately if content is blank.
- [ ] Otherwise scan the named artifact content for explicit
  non-implementation HITL request markers and let the existing capture service
  update its ledger.

## ActiveEpicPromotionCoordinator

This coordinator preserves `PromoteActiveEpicAsync` and
`ArtifactPromotionService` behavior.

### Input Rules

- [ ] Target is always `.agents/epic.md`.
- [ ] Evidence directory is `.agents/evidence/blockers`.
- [ ] Evidence stem is `active-epic-promotion`.
- [ ] Artifact name is `active epic`.
- [ ] Classifier is `EpicAuthoringOutputClassifier`.
- [ ] Validator is `EpicArtifactValidator`.
- [ ] Promoted lifecycle state is `Ready`.

### Classification Rules

- [ ] No top-level markdown heading: `Ambiguous`.
- [ ] First top-level heading contains `Blocked`: `Blocked`.
- [ ] First top-level heading matches `# Epic: ...`: `Promotable`.
- [ ] Heading resembles `# Epic` without the required colon form, or content
  contains `## Epic Metadata` while the first heading is wrong: `Malformed`.
- [ ] Otherwise: `Ambiguous`.

### Validation Rules For Promotable Output

- [ ] Reject blank content.
- [ ] Reject anything that reclassifies as non-promotable.
- [ ] Require headings `## Epic Metadata`, `## Desired Capability`,
  `## Acceptance Criteria`, and `## Milestone Roadmap`.
- [ ] Require either `## Strategic Purpose` or `## Strategic Continuity`.
- [ ] Require non-empty `Epic ID` and `Status` in the metadata field table.
- [ ] Require at least one milestone row with columns `Milestone ID`,
  `Milestone Name`, `Purpose`, `Outcome`, `Depends On`, and
  `Completion Signal`.
- [ ] Require non-empty values for `Milestone ID`, `Milestone Name`, `Purpose`,
  `Outcome`, and `Completion Signal`.

### Promotion Success

- [ ] Write `.agents/epic.md`.
- [ ] Mark `.agents/epic.md` lifecycle `Ready` with the caller-supplied note.
- [ ] Capture HITL requests from `.agents/epic.md`.
- [ ] Append `ArtifactPromoted` with prompt contract key
  `ArtifactPromotionService`, result `Promoted`, parser decision
  `Active epic promoted`, and the original prompt input snapshot.
- [ ] Save state `ActiveEpicReady` / `Completed`, decision
  `Artifact Promoted`, output `.agents/epic.md`.

### Promotion Rejection

- [ ] Write exact rejected output to
  `.agents/evidence/blockers/active-epic-promotion.NNNN.md`.
- [ ] Mark evidence lifecycle `Blocked` with the rejection reason.
- [ ] Append `ArtifactPromotionBlocked`.
- [ ] Save `EvidenceBlocked` / `Paused`.
- [ ] Use transition intent `ResolveArtifactPromotionBlocker`.
- [ ] Use next transition `Resolve blocker and rerun`.
- [ ] Map promotion status to decision text:
  - blocked: `Artifact Promotion Blocked`
  - ambiguous: `Artifact Promotion Ambiguous`
  - structurally invalid: `Artifact Promotion Invalid`
  - other rejected status: `Artifact Promotion Rejected`

## SelectionSuperseder

This service must update both provenance and lifecycle.

### Retire Branch

- [ ] Supersede active trusted selection provenance with
  `RetiredEpicStateDrift`.
- [ ] Mark `.agents/selection.md` lifecycle `Superseded`.
- [ ] Lifecycle note:
  `Retired epic state changed after EpicPreparationAudit.`

### Completion-Context Update

- [ ] Supersede active trusted selection provenance with
  `RoadmapCompletionContextDrift`.
- [ ] Mark `.agents/selection.md` lifecycle `Superseded`.
- [ ] Lifecycle note:
  `Roadmap completion context changed after completion certification.`

## Equivalence Checks

- [x] `SaveStateAsync` still preserves existing retired epics, blockers,
  transition intent, active artifacts, projection manifest counts, split-family
  count, and last decision id.
- [x] Default `NextTransitions` values remain identical.
- [ ] Runtime prompt failures still bypass generic failure overwrite by throwing
  `RoadmapStepException.AlreadyPersisted`.
- [ ] `OperationCanceledException` is not caught by prompt failure blocks.
