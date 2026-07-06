# Roadmap CLI invariant evidence dropped

## Severity

Medium

Confidence: High.

## Audit status

Verified against the current codebase.

The issue is real for the two `RoadmapStateMachine` call sites that invoke
`InvariantValidator.ValidateAsync`. `InvariantValidator` writes detailed invariant
failure evidence under `.agents/evidence/orchestration`, returns that path on
`InvariantValidationResult.EvidencePath`, and also classifies the target failure
state as either `EvidenceBlocked` or `Failed`. The state-machine callers throw only
`invariant.Error`, so the returned evidence path and failure classification are both
discarded. The top-level generic failure handler then writes a separate
`.agents/evidence/blockers/roadmap-transition-blocked-*.md` artifact and persists
that generic blocker as authoritative state.

Scope correction: `ValidateSplitChildPromotionAsync` has similar evidence-returning
shape, but it is not called by `RoadmapStateMachine` today. The split path currently
uses `SplitEpicBundleInterpreter` and `ArtifactPromotionService`, both of which
already persist their own evidence into state. This issue should stay focused on
`ValidateAsync` invariant failures.

## Verified behavior

Affected code:

- `src/CommandCenter.Roadmap.CLI/InvariantValidator.cs`
- `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs`
- `src/CommandCenter.Roadmap.CLI/RoadmapStepException.cs`
- `src/CommandCenter.Roadmap.CLI/RoadmapStateStore.cs`

`InvariantValidator.FailAsync` renders a `RoadmapBlockedArtifact`, writes it to
`RoadmapArtifactPaths.OrchestrationEvidenceDirectory` with the
`invariant-failure` prefix, and returns `InvariantValidationResult.Invalid` with
`FailureState`, `Error`, and `EvidencePath`.

The validator can classify failures as `Failed` for harder invariant breaks:

- Project Context hash changed during a run.
- Projection artifact exists without a manifest entry.
- More than one epic is marked `Ready` or `Executing`.

It classifies other failures as `EvidenceBlocked`:

- Projection manifest entry is invalid.
- Projection provenance is stale.
- Active epic is missing or structurally invalid.
- Execution preparation provenance is stale.
- Milestone spec declares a different epic path.
- Execution bridge prerequisites are incomplete.

The two state-machine call sites are:

- After `GenerateMilestoneDeepDivesForEpic` extracts milestone specs and records
  execution-preparation provenance, `GenerateMilestoneSpecsAsync` calls
  `ValidateAsync(RoadmapState.MilestoneSpecsReady, ...)`. On failure it throws
  `new RoadmapStepException(invariant.Error ...)`.
- Immediately before entering `ExecutionLoop`, `RunExecutionAndCertificationAsync`
  calls `ValidateAsync(RoadmapState.ExecutionPromptReady, ...)`. On failure it
  throws `new RoadmapStepException(invariant.Error ...)`.

Neither call site uses `invariant.FailureState` or `invariant.EvidencePath`.

The top-level `RoadmapStateMachine.RunAsync` catch block treats those exceptions as
unpersisted generic failures:

- It calls `WriteBlockedStateAsync(RoadmapState.EvidenceBlocked,
  "RoadmapStateMachine", exception.Message)`.
- `WriteBlockedStateAsync` creates a new blocker artifact under
  `.agents/evidence/blockers`.
- It saves state with `CurrentState = EvidenceBlocked`, `LastTransition.From =
  CoreReady`, `LastTransition.To = EvidenceBlocked`, `Prompt =
  RoadmapStateMachine`, `Projection = None`, `Decision = Blocked`, and
  `TransitionIntent.EvidencePaths = [generic blocker path]`.

That means an invariant that asked for `Failed` is downgraded to `EvidenceBlocked`,
the actual workflow point is replaced with `CoreReady -> EvidenceBlocked`, and the
validator evidence path is left orphaned on disk.

The transition journal also loses the invariant failure. Prompt-transition failures
inside `RunPromptTransitionWithCompletionAsync` append `TransitionFailed` and throw
`RoadmapStepException.AlreadyPersisted`, preventing generic overwrite. Promotion,
split, malformed execution, runtime execution, and invalid completion-certification
failures have similar specialized persistence paths. Invariant failures have no
equivalent path, so no `InvariantFailed` or `TransitionFailed` journal record points
to the validator evidence.

Existing tests verify pieces of the system but not this end-to-end behavior:

- `InvariantValidatorTests` and `ExecutionPreparationProvenanceTests` assert that
  `ValidateAsync` returns invalid results for several invariant failures.
- `RoadmapFailurePersistenceTests` asserts that prompt-transition and
  artifact-promotion failures avoid generic blocker overwrite.
- There is no state-machine test asserting that invariant failures preserve
  `EvidencePath`, preserve `FailureState`, or write journal evidence.

## Impact

The authoritative state points at the least useful artifact. The detailed invariant
diagnostic remains on disk, but `.agents/state.md` links only the generic blocker
through `LastTransition.Output` and `TransitionIntent.EvidencePaths`.

This weakens recovery and auditability for:

- Stale execution preparation before execution.
- Missing execution prerequisites.
- Multiple active epics.
- Projection manifest and provenance violations.
- Active epic/spec mismatch.
- Project Context drift during a run.

It also creates two artifacts for one failure while making only the second, less
specific artifact discoverable through state. Automated recovery or status tooling
would naturally inspect `TransitionIntent.EvidencePaths`, miss the invariant
evidence, and lack the exact invariant category that should drive repair.

The failure-state downgrade matters. `InvariantValidator` intentionally returns
`Failed` for some integrity violations, but the generic catch always persists
`EvidenceBlocked`. That changes both the operational severity and the follow-up
behavior expected by startup/resume planning.

## Meaningful solution options

### Option 1: Persist invariant failures directly in `RoadmapStateMachine`

Add a state-machine helper such as:

`PersistInvariantFailureAsync(InvariantValidationResult result, RoadmapState from, string transition, string projection)`

Recommended semantics:

- Require `result.IsValid == false`.
- Require a non-empty `result.EvidencePath`; if absent, write a small fallback
  blocker that says invariant evidence was missing.
- Save `CurrentState = result.FailureState`.
- Save `LastTransition.From` as the state being validated, not `CoreReady`.
- Save `LastTransition.To = result.FailureState`.
- Save `LastTransition.Prompt` as a stable transition name such as
  `InvariantValidation` or a contextual name such as
  `PostMilestoneInvariantValidation` / `PreExecutionInvariantValidation`.
- Save `LastTransition.Output = result.EvidencePath`.
- Save `TransitionIntent.EvidencePaths = [result.EvidencePath]`.
- Save blocker text from `result.Error`.
- Append a journal event such as `InvariantFailed` with output path
  `[result.EvidencePath]`.
- Throw `RoadmapStepException.AlreadyPersisted(...)` or return a terminal outcome so
  the top-level generic handler does not create a second blocker.

Pros:

- Closely matches existing patterns for prompt-transition, promotion, split, and
  execution failures.
- Preserves validator ownership of diagnostic evidence and state-machine ownership
  of workflow persistence.
- Keeps the fix narrow.

Cons:

- Adds another specialized persistence branch inside an already large state machine.
- Requires choosing stable names for invariant validation transitions.

This is the recommended fix.

### Option 2: Add evidence and failure metadata to `RoadmapStepException`

Extend `RoadmapStepException` with optional fields:

- `RoadmapState? PersistedState`
- `RoadmapState? FromState`
- `string? EvidencePath`
- `string? Transition`
- `string? Projection`
- `string? RecoveryIntent`

The invariant call sites would throw a metadata-rich exception. The top-level catch
would check for evidence metadata and persist a structured failure instead of
calling `WriteBlockedStateAsync`.

Pros:

- Reusable for future failures outside prompt-transition try/catch blocks.
- Keeps call sites short.
- Centralizes generic fallback vs evidence-preserving behavior.

Cons:

- Turns an exception type into a persistence transport.
- Easy for future callers to provide incomplete or inconsistent metadata.
- Still needs a state-machine helper internally to write journal and state.

This is useful only if several non-prompt validation failures need the same pattern.

### Option 3: Introduce a small transition-failure persistence service

Extract a service or helper responsible for writing classified workflow failures:

- Input: failure kind, previous state, attempted state, transition name, projection,
  evidence paths, blocker text, recovery intent, and journal event name.
- Output: persisted state plus journal record.

Then route invariant failures, promotion failures, split failures, malformed
execution, runtime execution failures, and invalid completion certification through
the same helper over time.

Pros:

- Reduces duplicated state/journal persistence rules.
- Makes evidence preservation a standard contract.
- Creates a natural place to enforce "no generic overwrite after specialized
  persistence".

Cons:

- Larger refactor than this issue requires.
- Existing failure paths are not fully uniform, so extracting the abstraction may
  reveal design mismatches.

This is a good follow-up if failure persistence keeps expanding, but it is too broad
for the first fix.

### Option 4: Minimal link-only fix

At the two call sites, if `ValidateAsync` returns invalid, call a small helper that
saves `TransitionIntent.EvidencePaths = [invariant.EvidencePath]` and
`LastTransition.Output = invariant.EvidencePath`, then throw `AlreadyPersisted`.
Skip a new journal event.

Pros:

- Smallest code change that fixes state discoverability.
- Avoids creating duplicate generic blocker artifacts.

Cons:

- Leaves the journal blind to invariant failures.
- Easy to under-specify `LastTransition.From`, `To`, and `Decision`.
- Does not establish a durable pattern for future invariant or validation failures.

This is acceptable only as a short-lived patch.

### Option 5: Move invariant evidence creation out of `InvariantValidator`

Change `InvariantValidator` to return a structured failure reason without writing an
artifact. Let `RoadmapStateMachine` render and persist all invariant evidence.

Pros:

- Puts all persistence in one owner.
- Avoids the current "evidence written but not linked" split-brain behavior.

Cons:

- Larger change to the validator contract and tests.
- Loses a useful separation: validator owns diagnostic content, state machine owns
  workflow state.
- More churn for little benefit unless the project is standardizing all failure
  rendering in the state machine.

This is not recommended for the near-term fix.

## Recommended approach

Implement Option 1.

Keep `InvariantValidator` responsible for generating invariant evidence, but make
`RoadmapStateMachine` persist invalid results as first-class workflow failures.
Model the behavior after the existing prompt-transition and artifact-promotion
failure paths: write state and journal with the specific evidence path, then prevent
the top-level generic safety net from overwriting that persisted failure.

Use two contextual transition names so journal history is easy to interpret:

- `PostMilestoneInvariantValidation` for validation after milestone spec generation.
- `PreExecutionInvariantValidation` for validation before execution.

## Implementation details

The helper should preserve the validator result exactly:

- `CurrentState`: `result.FailureState`.
- `LastTransition.Output`: `result.EvidencePath`.
- `TransitionIntent.Intent`: `ResolveInvariantViolation`.
- `TransitionIntent.DispatchState`: `result.FailureState`.
- `TransitionIntent.EvidencePaths`: `[result.EvidencePath]`.
- Blocker required next step: `Review <evidence path>, repair the invariant
  violation, and rerun the roadmap CLI.`

For the two call sites:

- After milestone generation, use `from = RoadmapState.MilestoneSpecsReady`,
  `to = result.FailureState`, `transition = PostMilestoneInvariantValidation`, and
  `projection = None` unless a more specific projection is deliberately chosen.
- Before execution, use `from = RoadmapState.ExecutionPromptReady`,
  `to = result.FailureState`, `transition = PreExecutionInvariantValidation`, and
  `projection = None`.

The journal record should include:

- Event: `InvariantFailed`.
- Previous state: the validated state.
- Attempted state: `result.FailureState`.
- Prompt or transition: the contextual transition name.
- Prompt contract key: `InvariantValidator`.
- Output paths: `[result.EvidencePath]`.
- Result: `Failed` for `result.FailureState == Failed`, otherwise `EvidenceBlocked`
  or `Blocked`.
- Error message: `result.Error`.

If `result.EvidencePath` is unexpectedly null or blank, treat that as a secondary
bug: write fallback blocker evidence and include both the missing-evidence condition
and the original invariant error in the blocker text.

## Acceptance criteria

- Invariant validation failures leave `.agents/state.md` pointing to the validator
  evidence path through both `LastTransition.Output` and
  `TransitionIntent.EvidencePaths`.
- Invariant validation failures preserve `InvariantValidationResult.FailureState`;
  `Failed` invariant results are not downgraded to `EvidenceBlocked`.
- No duplicate `roadmap-transition-blocked-*.md` artifact is created for an
  invariant failure that was already persisted.
- The transition journal records an `InvariantFailed` event with the invariant
  evidence path and error message.
- Tests cover stale execution preparation before execution end to end through
  `RoadmapStateMachine`.
- Tests cover a projection manifest/provenance invariant failure end to end through
  `RoadmapStateMachine`.
- Tests cover a `Failed` invariant classification, such as multiple active epics or
  missing projection manifest entry, and assert that the persisted current state is
  `Failed`.
