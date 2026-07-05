# Prompt Failure State Is Overwritten By Generic Blocker

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

After saving that state, they rethrow. `RunAsync` catches `RoadmapStepException` and calls `WriteBlockedStateAsync(RoadmapState.EvidenceBlocked, "RoadmapStateMachine", exception.Message)`. That second write replaces the useful transition failure with a generic blocker. `WriteBlockedStateAsync` also records the transition as `CoreReady -> EvidenceBlocked`, even when the actual failure happened deep in selection, promotion, milestone generation, or completion certification.

Relevant code:

- `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs`
- `RunAsync`
- `RunPromptTransitionAsync`
- `RunPromptForPromotionAsync`
- `WriteBlockedStateAsync`

## Impact

The CLI loses the exact recovery boundary after failures. A human sees a generic state-machine blocker instead of the failed prompt transition. A resume planner cannot reliably determine which output paths were expected or which source state should be retried.

This also weakens the transition journal/state pair: the journal has the real `TransitionFailed` record, but `.agents/state.md` no longer agrees with it.

## Root Cause

Failure persistence is split across two layers without ownership rules:

- prompt transition helpers persist domain-specific failure state
- the top-level runner persists generic failure state for all `RoadmapStepException`s

The top-level catch cannot distinguish an exception that has already been persisted from an exception that still needs a blocked state.

## Proposal

Make failure persistence explicit and single-owner.

Suggested approach:

1. Add a typed exception for persisted roadmap failures:

```csharp
internal sealed class PersistedRoadmapStepException : RoadmapStepException
{
    public PersistedRoadmapStepException(string message) : base(message) { }
}
```

2. When `RunPromptTransitionAsync` or `RunPromptForPromotionAsync` saves a failure state, rethrow `PersistedRoadmapStepException`.

3. In `RunAsync`, handle it without writing another blocked state:

```csharp
catch (PersistedRoadmapStepException exception)
{
    console.Error(exception.Message);
    return RoadmapOutcome.Failed;
}
catch (RoadmapStepException exception)
{
    await WriteBlockedStateAsync(...);
    console.Error(exception.Message);
    return RoadmapOutcome.Failed;
}
```

4. Replace `WriteBlockedStateAsync` with a method that accepts a structured context:

```csharp
internal sealed record BlockedTransitionContext(
    RoadmapState From,
    RoadmapState To,
    string Transition,
    string Projection,
    IReadOnlyList<string> EvidencePaths,
    string Reason,
    string RequiredNextStep);
```

5. Use the structured context for preflight, resume-planning blockers, invariant blockers, and unhandled roadmap step failures. Do not hard-code `CoreReady` unless the blocker truly belongs to startup initialization.

This keeps the top-level catch as a safety net while preserving precise failure state whenever a lower-level transition has already persisted it.

## Acceptance Criteria

- A failed `SelectNextEpic` prompt leaves `.agents/state.md` with `Prompt = SelectNextEpic`, not `RoadmapStateMachine`.
- A failed promotion prompt leaves `ResolveTransitionFailure` with the actual output paths.
- Generic preflight failure still writes a blocked state.
- Generic unpersisted `RoadmapStepException` still writes a blocked state.
- The state document and latest `TransitionFailed` journal record agree on prompt, source state, target state, and output paths.

## Suggested Tests

- `Prompt_failure_preserves_transition_specific_blocked_state`
- `Promotion_prompt_failure_preserves_transition_specific_blocked_state`
- `Unhandled_step_exception_writes_generic_blocker_once`
- `Preflight_failure_records_preflight_blocker`
- `Persisted_failure_is_not_rewritten_by_top_level_catch`
