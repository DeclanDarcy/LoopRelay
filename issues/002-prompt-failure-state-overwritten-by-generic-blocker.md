# Prompt Failure State Is Overwritten By Generic Blocker

## Audit Status

Verified against the current codebase on 2026-07-05.

Only one file matched `issues/002*.md`: this issue. The finding is valid, and the current tests do not assert the state fields that would expose the regression.

## Finding

Prompt transition failures are saved with useful transition context, then immediately overwritten by the top-level `RoadmapStateMachine` catch block.

The lower-level prompt helpers already persist a contextual failure state:

- `RunPromptTransitionAsync`
- `RunPromptForPromotionAsync`

They write:

- a `TransitionFailed` journal record
- `EvidenceBlocked`
- the actual source transition
- the prompt name
- the projection path
- the intended output paths
- `ResolveTransitionFailure` as transition intent

After saving that state, they rethrow. `RunAsync` catches the same `RoadmapStepException` and calls `WriteBlockedStateAsync(RoadmapState.EvidenceBlocked, "RoadmapStateMachine", exception.Message)`. That second write replaces the useful transition failure with a generic blocker. `WriteBlockedStateAsync` also records the transition as `CoreReady -> EvidenceBlocked`, even when the actual failure happened deep in selection, promotion, milestone generation, completion certification, or roadmap completion context update.

Relevant code:

- `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs`
  - `RunAsync`
  - `RunPromptTransitionAsync`
  - `RunPromptForPromotionAsync`
  - `WriteBlockedStateAsync`
- `src/CommandCenter.Roadmap.CLI/RoadmapPromptRunner.cs`
- `src/CommandCenter.Roadmap.CLI/RoadmapStateStore.cs`
- `src/CommandCenter.Roadmap.CLI/RoadmapStepException.cs`

## Verified Control Flow

1. `RoadmapPromptRunner.RunOneShotAsync` throws `RoadmapStepException` when an agent turn does not complete.

   Code evidence: `RoadmapPromptRunner.cs`, where non-`Completed` `AgentTurnState` becomes `RoadmapStepException`.

2. `RunPromptTransitionAsync` catches non-cancellation exceptions, appends `TransitionFailed`, saves `EvidenceBlocked`, sets `LastTransition` to the real prompt transition, sets `TransitionIntent` to `ResolveTransitionFailure`, then rethrows.

   Confirmed saved fields include:

   - `CurrentState = EvidenceBlocked`
   - `Status = Failed`
   - `From = from`
   - `To = EvidenceBlocked`
   - `Prompt = prompt`
   - `Projection = projectionPath`
   - `Output = string.Join(", ", outputs)`
   - `TransitionIntent = ResolveTransitionFailure`
   - `EvidencePaths = outputs`

3. `RunPromptForPromotionAsync` has the same failure ownership pattern for promotion prompts. It records the prompt failure before artifact promotion happens, saves `EvidenceBlocked`, sets `TransitionIntent` to `ResolveTransitionFailure`, and rethrows.

4. `RunAsync` catches all `RoadmapStepException`s from the execution block and writes a second blocked state:

   - `Prompt = RoadmapStateMachine`
   - `Projection = None`
   - `Output = <new roadmap-transition-blocked evidence path>`
   - `Decision = Blocked`
   - `From = CoreReady`
   - `To = EvidenceBlocked`
   - `TransitionIntent = ResolveBlocker`

5. `RoadmapStateStore.SaveAsync` serializes one `## Last Transition` table and one `## Transition Intent` table. The second save is therefore destructive for the state document. The journal still has the real `TransitionFailed` record, but `.agents/state.md` no longer agrees with it.

## Concrete Reproduction Path

Existing test doubles can reproduce this without new infrastructure:

1. Seed project context, roadmap completion context, and roadmap.
2. Return a valid `SelectNextEpic` projection.
3. Make the `SelectNextEpic` runtime prompt fail.
4. Run `StateMachineFactory.Create(repo, runtime).RunAsync(...)`.

The lower-level helper first persists a failure for:

- `From = RoadmapCompletionContextReady`
- `To = EvidenceBlocked`
- attempted prompt target in the journal: `SelectNextStrategicInitiative`
- `Prompt = SelectNextEpic`
- `Projection = .agents/projections/select-next-epic.md`
- `Output = .agents/selection.md`
- `Intent = ResolveTransitionFailure`

The top-level catch then overwrites it with:

- `From = CoreReady`
- `Prompt = RoadmapStateMachine`
- `Projection = None`
- `Output = .agents/evidence/blockers/roadmap-transition-blocked-*.md`
- `Intent = ResolveBlocker`

The same pattern applies to promotion prompt failures such as `CreateNewEpic`, `RealignEpic`, and `ReimagineEpic`; their intended output path should remain `.agents/epic.md`, but the generic writer replaces it with the generic blocker evidence path.

## Confirmed Affected Prompt Paths

Any runtime prompt executed through `RunPromptTransitionAsync` or `RunPromptForPromotionAsync` is affected when the runtime turn fails:

- `CreateRoadmapCompletionContext`
- `SelectNextEpic`
- `EpicPreparationAudit`
- `CreateNewEpic`
- `RealignEpic`
- `ReimagineEpic`
- `SplitEpic`
- `GenerateMilestoneDeepDivesForEpic`
- `EvaluateEpicCompletionAndDrift`
- `UpdateRoadmapCompletionContext`

## Non-Affected Or Different Paths

- Artifact promotion classification failures after a completed authoring prompt are not the same bug. `PromoteActiveEpicAsync` persists `ArtifactPromotionBlocked` and returns a paused outcome without throwing.
- Completion certification routing is not the same bug. `PersistCompletionRouteAsync` writes the final completion route and does not throw in the normal path.
- Cancellation is handled separately. The prompt helpers do not catch `OperationCanceledException`, and `RunAsync` writes cancelled state through `WriteCancelledStateAsync`.
- Projection cache failures are related but different. `ProjectionCache` writes a blocker artifact for invalid or stale projections, but it does not write roadmap state. The top-level generic catch still writes state, which may be necessary today, but it creates duplicate blocker evidence and loses the projection-blocked evidence path as structured state.
- Invariant validation failures are related context-loss cases. `InvariantValidator` writes an invariant-failure artifact and returns its path, but callers throw only the message. The top-level generic writer then saves a generic blocker and does not preserve the invariant evidence path.

## Impact

The CLI loses the exact recovery boundary after prompt failures. A human sees a generic state-machine blocker instead of the failed prompt transition. A resume planner or repair tool cannot reliably determine which output paths were expected or which source state should be retried.

Observable state damage:

- `LastTransition.Prompt` becomes `RoadmapStateMachine`.
- `LastTransition.Projection` becomes `None`.
- `LastTransition.Output` becomes the generic blocker artifact path.
- `LastTransition.From` becomes `CoreReady`.
- `TransitionIntent.Intent` becomes `ResolveBlocker`.
- `TransitionIntent.EvidencePaths` become the generic blocker artifact path.

This also weakens the transition journal/state pair: the journal has the real `TransitionFailed` record, but `.agents/state.md` no longer agrees with it.

## Root Cause

Failure persistence is split across two layers without ownership rules:

- prompt transition helpers persist domain-specific failure state
- the top-level runner persists generic failure state for every `RoadmapStepException`

The top-level catch cannot distinguish an exception that has already been persisted from an exception that still needs a blocked state.

There is one additional implementation constraint: `RoadmapStepException` is currently `sealed` and only carries a message. The original subclass proposal below will not compile unless the base exception is unsealed, replaced, or extended with metadata.

## Solution Options

### Option 1: Skip Generic Rewrite When State Already Shows A Persisted Transition Failure

In the top-level `catch (RoadmapStepException exception)`, reload `stateStore` and detect whether the current state already represents a persisted transition failure from this run:

- `CurrentState == EvidenceBlocked`
- `LastTransition.Status == Failed`
- `TransitionIntent.Intent == ResolveTransitionFailure`
- `LastTransition.Prompt != RoadmapStateMachine`
- blocker text contains or matches the exception message

If those checks pass, emit the console error and return `RoadmapOutcome.Failed` without calling `WriteBlockedStateAsync`.

Pros:

- Smallest code change.
- No exception hierarchy changes.
- Protects both regular prompt and promotion prompt failures.

Cons:

- The ownership rule is implicit and state-based.
- It can be fooled by stale state if future code throws after a prior persisted failure.
- It does not solve duplicate or generic state for projection and invariant blocker paths.

Best use: short-term containment if the team wants a narrow, low-risk patch.

### Option 2: Add Explicit Already-Persisted Failure Metadata

Make persistence ownership explicit in `RoadmapStepException`.

Two viable shapes:

1. Unseal `RoadmapStepException` and add `PersistedRoadmapStepException : RoadmapStepException`.
2. Keep one exception type and add metadata such as `bool StateAlreadyPersisted` plus an optional `RoadmapFailurePersistenceKind`.

Then:

- prompt helpers save their transition-specific failure state
- prompt helpers throw or wrap an exception marked `StateAlreadyPersisted`
- `RunAsync` handles already-persisted failures without writing another blocked state
- unpersisted `RoadmapStepException`s still go through the generic writer

Example shape:

```csharp
internal class RoadmapStepException : Exception
{
    public RoadmapStepException(
        string message,
        bool stateAlreadyPersisted = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StateAlreadyPersisted = stateAlreadyPersisted;
    }

    public bool StateAlreadyPersisted { get; }
}
```

Top-level handling:

```csharp
catch (RoadmapStepException exception) when (exception.StateAlreadyPersisted)
{
    console.Error(exception.Message);
    return RoadmapOutcome.Failed;
}
catch (RoadmapStepException exception)
{
    await WriteBlockedStateAsync(RoadmapState.EvidenceBlocked, "RoadmapStateMachine", exception.Message);
    console.Error(exception.Message);
    return RoadmapOutcome.Failed;
}
```

Pros:

- Clear ownership contract.
- Low to moderate implementation cost.
- Avoids state inspection as control flow.

Cons:

- Wrapping exceptions after a save can lose the original throw stack unless the original exception is preserved as `InnerException`.
- Existing `throw;` sites need intentional replacement after the state write.
- Still leaves generic writer context quality unchanged for unpersisted failures.

Best use: recommended minimal fix if this issue is addressed directly without broader failure-state refactoring.

### Option 3: Move State Persistence Ownership To The Top-Level Catch With Structured Failure Context

Stop writing failed state inside prompt helpers. Instead, have them append the transition journal failure and throw a `RoadmapStepException` that carries a structured blocked context:

```csharp
internal sealed record BlockedTransitionContext(
    RoadmapState From,
    RoadmapState To,
    string Transition,
    string Projection,
    IReadOnlyList<string> EvidencePaths,
    string Decision,
    string Reason,
    string RequiredNextStep,
    RoadmapTransitionIntent Intent);
```

Then `RunAsync` is the single owner of failed state writes:

- if context exists, persist that context
- if no context exists, persist a generic safety-net blocker

Pros:

- Single owner for state writes.
- Preserves prompt-specific details without double-writing state.
- Can also preserve invariant and projection evidence paths if those throw structured context.

Cons:

- Higher refactor cost.
- The prompt helper currently has the best access to correlation ID, input snapshot, output paths, and timing. The context object must carry enough of that without making top-level code know prompt internals.
- Needs careful test coverage to avoid dropping existing journal behavior.

Best use: medium refactor when cleaning up failure semantics across roadmap orchestration.

### Option 4: Replace `WriteBlockedStateAsync` With A Structured Blocker Writer

Keep the top-level safety net, but remove hard-coded `CoreReady`, `RoadmapStateMachine`, `None`, and `ResolveBlocker` defaults from the shared writer. Introduce a required context object and use it for all blocker writes:

```csharp
internal sealed record RoadmapBlockedStateWrite(
    RoadmapState CurrentState,
    TransitionStatus Status,
    RoadmapState From,
    RoadmapState To,
    string Prompt,
    string Projection,
    IReadOnlyList<string> OutputPaths,
    string Decision,
    IReadOnlyList<BlockerRow> Blockers,
    RoadmapTransitionIntent Intent,
    IReadOnlyList<string> NextTransitions,
    string BlockedArtifactPrefix,
    string BlockedArtifactDetails);
```

Use this from:

- preflight blockers
- resume-planning blockers
- prompt transition failures
- projection blockers
- invariant blockers
- execution bridge blockers
- unhandled roadmap step failures

Pros:

- Fixes this issue and the broader `CoreReady -> EvidenceBlocked` misattribution.
- Makes tests assert one blocked-state contract across all failure paths.
- Preserves evidence paths for invariant and projection blockers.

Cons:

- Broadest blast radius among practical options.
- Requires deciding meaningful `From`, `To`, `Prompt`, and `Intent` for each non-prompt failure class.
- May overlap with `issues/005-preflight-overwrites-existing-blocked-state.md`; sequencing matters.

Best use: durable fix if roadmap blocked-state semantics are being made consistent now.

### Option 5: Return Typed Transition Results Instead Of Throwing For Expected Prompt Failures

Change prompt helpers to return a result such as:

```csharp
internal sealed record PromptTransitionResult(
    bool Succeeded,
    string? Output,
    RoadmapOutcome? FailureOutcome,
    RoadmapStateDocument? PersistedFailure);
```

Callers branch on failure instead of throwing into the top-level catch.

Pros:

- Removes expected prompt failures from exception control flow.
- Makes the state machine's success, pause, and fail outcomes explicit at call sites.

Cons:

- Large change across many call sites.
- Risks obscuring genuinely unexpected infrastructure failures.
- More boilerplate than needed for the current bug.

Best use: only if the roadmap state machine is being reworked toward explicit result types throughout.

## Recommended Implementation Path

Use Option 2 as the direct fix:

1. Extend `RoadmapStepException` with `StateAlreadyPersisted` metadata, or unseal it and add a persisted-failure subtype.
2. After `RunPromptTransitionAsync` and `RunPromptForPromotionAsync` successfully save failure state, throw a persisted-failure exception that preserves the original exception as `InnerException`.
3. Add a top-level catch/filter that returns `RoadmapOutcome.Failed` without calling `WriteBlockedStateAsync` for already-persisted failures.
4. Add focused regression tests for regular prompt and promotion prompt failures.

Then consider Option 4 as a follow-up to remove the hard-coded `CoreReady` generic blocker model and to preserve projection/invariant evidence paths.

## Acceptance Criteria

- A failed `SelectNextEpic` prompt leaves `.agents/state.md` with `Prompt = SelectNextEpic`, not `RoadmapStateMachine`.
- A failed `SelectNextEpic` prompt leaves `Projection = .agents/projections/select-next-epic.md`.
- A failed `SelectNextEpic` prompt leaves `Output = .agents/selection.md`.
- A failed `SelectNextEpic` prompt leaves `Intent = ResolveTransitionFailure`.
- A failed promotion prompt such as `CreateNewEpic` leaves `Prompt = CreateNewEpic`.
- A failed promotion prompt leaves `Output = .agents/epic.md`.
- A failed promotion prompt leaves `ResolveTransitionFailure` with the actual output paths.
- The final `.agents/state.md` does not contain `Prompt = RoadmapStateMachine` for already-persisted prompt failures.
- Generic preflight failure still writes a blocked state.
- Generic unpersisted `RoadmapStepException` still writes a blocked state.
- The state document and latest `TransitionFailed` journal record agree on prompt, source state, projection, and output paths.
- The distinction between journal attempted target and persisted blocked target is explicit: the journal may keep the original attempted target, while `.agents/state.md` may use `EvidenceBlocked` as the current failure target.

## Suggested Tests

- `Prompt_failure_preserves_transition_specific_blocked_state`
- `Promotion_prompt_failure_preserves_transition_specific_blocked_state`
- `Persisted_prompt_failure_is_not_rewritten_by_top_level_catch`
- `Unhandled_step_exception_writes_generic_blocker_once`
- `Preflight_failure_records_preflight_blocker`
- `Projection_validation_failure_still_persists_blocked_state`
- `Invariant_failure_preserves_invariant_evidence_path` if Option 4 is implemented

Existing tests to strengthen:

- `TransitionJournalTests.Prompt_failure_reuses_started_snapshot` should also assert final `RoadmapStateDocument`.
- `RoadmapStateMachineEpicPreparationTests.Runtime_prompt_failure_remains_failed_transition` should assert `Prompt`, `Projection`, `Output`, `TransitionIntent`, and absence of `RoadmapStateMachine`.

## Verification Performed

- Static audit of `RoadmapStateMachine.RunAsync`, prompt helpers, blocker writer, state store, prompt runner, projection cache, invariant validator, and resume planner.
- Focused test run: `dotnet test tests\CommandCenter.Roadmap.CLI.Tests\CommandCenter.Roadmap.CLI.Tests.csproj --filter FullyQualifiedName~TransitionJournalTests`.
- Result: passed, 4 tests. This confirms the current tests cover journal snapshot behavior but not the final-state overwrite.
