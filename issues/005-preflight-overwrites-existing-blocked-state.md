# Preflight Overwrites Existing Blocked State

## Status

Verified against the current roadmap CLI implementation.

## Finding

`RoadmapStateMachine.RunAsync` loads persisted state before Project Context preflight, but it does not classify that state before preflight can mutate `.agents/state.md`.

Current startup order:

1. Load persisted `.agents/state.md` with `stateStore.LoadAsync`.
2. Run Project Context preflight with `projectContextLoader.LoadAsync`.
3. Emit the prompt contract snapshot.
4. If preflight throws `RoadmapStepException`, write a new `EvidenceBlocked` state for `Preflight`.
5. Only after preflight succeeds, call `RoadmapResumePlanner.PlanAsync`.

Relevant code:

- `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs`
  - `RunAsync` loads state, then immediately runs Project Context preflight before resume planning.
  - The preflight catch path calls `WriteBlockedStateAsync(RoadmapState.EvidenceBlocked, "Preflight", exception.Message)` and returns `RoadmapOutcome.PreflightBlocked`.
  - `ExecuteResumePlanAsync` would return terminal outcomes without mutation for `RoadmapResumeAction.Terminal`, but it is unreachable when preflight fails.
  - `WriteBlockedStateAsync` writes new blocker evidence and calls `SaveStateAsync` with `From = CoreReady`, `Prompt = Preflight`, a new single blocker row, and `TransitionIntent = ResolveBlocker`.
  - `SaveStateAsync` preserves existing blockers and transition intent only when the caller passes `null`; `WriteBlockedStateAsync` passes replacements, so the previous blocker and recovery intent are overwritten.
- `src/CommandCenter.Roadmap.CLI/ProjectContextLoader.cs`
  - Requires the exact Project Context source file set `.agents/core/01-purpose.md` through `.agents/core/08-vocabulary.md`.
  - Missing required files or unexpected numbered files throw `RoadmapStepException("Project Context source contract violation...")`.
- `src/CommandCenter.Roadmap.CLI/RoadmapResumePlanner.cs`
  - Already treats `EvidenceBlocked`, terminal paused states, `Completed`, and `Failed` as terminal outcomes.
  - That protection is ineffective when Project Context preflight fails before the planner is called.

## Reproduction Scenario

1. Persist a state file with `Current State = EvidenceBlocked`, for example a blocker from artifact promotion or transition failure.
2. Remove one required Project Context source file, such as `.agents/core/01-purpose.md`, or add an unexpected numbered file such as `.agents/core/09-extra.md`.
3. Run the roadmap CLI.
4. `ProjectContextLoader.LoadAsync` throws before `RoadmapResumePlanner.PlanAsync`.
5. `RoadmapStateMachine` writes a new preflight blocker.

Resulting state characteristics:

- `Current State` remains or becomes `EvidenceBlocked`.
- `Last Transition` is replaced with a synthetic preflight transition from `CoreReady` to `EvidenceBlocked`.
- `Prompt` becomes `Preflight`.
- The blocker table is replaced with the Project Context violation.
- `Transition Intent` becomes `ResolveBlocker` targeting the new preflight evidence file.
- The original recovery intent, blocker details, and last transition are no longer represented in `.agents/state.md`.

For terminal non-blocked states, the damage is broader: `StrategicInvestigationRequired`, `RoadmapRevisionRequired`, `NoSuitableInitiative`, `EvidenceGathering`, `ExecutionBlocked`, `Completed`, and `Failed` can all be replaced by a preflight `EvidenceBlocked` state before the resume planner can return the matching terminal outcome.

## Current Test Gap

There is partial coverage, but it does not exercise the failing path:

- `RoadmapStateMachineResumeTests.Existing_blocked_state_is_loaded_before_startup_can_overwrite_it` seeds Project Context before running the state machine. It proves a healthy preflight reaches resume planning and preserves an existing blocked state.
- `RoadmapResumePlannerTests.Evidence_blocked_state_remains_paused` proves the planner classifies `EvidenceBlocked` correctly when it is called.
- `RoadmapResumePlannerTests.Terminal_paused_selection_states_do_not_auto_resume` proves planner-level terminal pause behavior.

Missing coverage is the critical case: existing terminal or blocked state plus failing Project Context preflight.

## Impact

An existing blocked or terminal state is preserved only while Project Context is healthy. If a repository is already blocked for a specific transition and a Project Context file is later missing, renamed, or temporarily invalid, rerunning the CLI replaces the original state narrative with a generic preflight blocker.

This weakens recovery because `.agents/state.md` is the operator-facing summary of what to do next. The previous evidence artifacts may still exist, but the top-level state no longer points at them or explains the original recovery route.

It also changes the observable outcome:

- Existing `EvidenceBlocked` should return `Paused`; failing preflight currently returns `PreflightBlocked`.
- Existing `Completed` should return `Completed`; failing preflight can replace it with `EvidenceBlocked`.
- Existing `Failed` should return `Failed`; failing preflight can replace it with `EvidenceBlocked`.
- Existing paused selection or execution-blocked states should return `Paused`; failing preflight can replace them with `EvidenceBlocked`.

## Root Cause

Startup has two separate gates with the wrong mutability boundary:

- Project Context preflight validates fresh or resumable work and may write a blocker.
- Resume planning classifies persisted state and knows which states are terminal.

Because preflight runs first, it can mutate state before the state-aware planner decides whether the existing state should be reported as terminal, paused, blocked, completed, failed, recovered, or resumed.

## Solution Options

### Option A: Add an early terminal-state guard in `RunAsync`

Before Project Context preflight, inspect `persistedState.CurrentState`. If it is `EvidenceBlocked`, a terminal pause state, `Completed`, or `Failed`, return the same terminal outcome that `RoadmapResumePlanner` would return. Do not load Project Context and do not write preflight blocker evidence.

Pros:

- Smallest implementation.
- Directly fixes state loss for terminal states.
- Low risk to active resume behavior.

Cons:

- Duplicates terminal-state classification already present in `RoadmapResumePlanner`.
- Easy for future roadmap states to diverge between startup and planner logic.
- Does not improve preservation when active resume fails Project Context preflight.

### Option B: Introduce a preflight-aware startup planner

Add a lightweight planner that classifies persisted state before Project Context is loaded:

```csharp
internal sealed class RoadmapStartupPlanner
{
    public RoadmapStartupPlan PlanBeforePreflight(RoadmapStateDocument? persistedState);
}

internal sealed record RoadmapStartupPlan(
    RoadmapStartupAction Action,
    RoadmapPreflightRequirement PreflightRequirement,
    RoadmapOutcome? TerminalOutcome);
```

Possible `RoadmapPreflightRequirement` values:

- `None`: existing terminal states do not need Project Context just to report their status.
- `RequiredForInitialize`: no state exists, so fresh initialization needs Project Context.
- `RequiredForResume`: active or recoverable workflow states need Project Context before the next transition.

Pros:

- Keeps startup ordering explicit and testable.
- Avoids spreading terminal-state checks through `RunAsync`.
- Leaves `RoadmapResumePlanner` focused on post-preflight resume decisions.
- Creates a clear place to handle `Cancelled` separately from terminal and active states.

Cons:

- Adds a small new concept to the roadmap CLI.
- Must keep startup terminal classification consistent with resume planner terminal classification, preferably through shared helpers.

### Option C: Make `RoadmapResumePlanner` own preflight requirements

Refactor the planner into two phases:

1. `PlanBeforePreflight(RoadmapStateDocument?)`
2. `PlanAfterPreflight(RoadmapStateDocument?, ProjectContext)`

The first phase returns terminal outcomes without Project Context and asks the state machine to load Project Context only when the selected route needs it.

Pros:

- Avoids duplicate planning concepts.
- Keeps all state classification in one component.
- Makes it hard for startup and resume planning to drift.

Cons:

- Larger refactor than Option A or B.
- Existing planner methods assume a non-null `ProjectContext`, so the internal API needs careful separation.

### Option D: Preserve previous state context when writing preflight blockers

Keep preflight before resume planning, but change the preflight failure path to preserve the previous state:

- Include previous `CurrentState`, `LastTransition`, and `TransitionIntent` in the new blocker evidence.
- Append or merge the preflight blocker instead of replacing the existing blocker table.
- Preserve the existing `TransitionIntent` unless the previous state was active and the preflight blocker is the actionable next step.
- Consider a separate Project Context health artifact instead of rewriting roadmap state for terminal states.

Pros:

- Reduces information loss for active resumable states.
- Useful even if Option A, B, or C handles terminal states.
- Gives operators enough context to repair both the original blocker and the preflight blocker.

Cons:

- Does not by itself fix incorrect outcomes for terminal states.
- Can make `.agents/state.md` noisier if transient preflight failures accumulate.
- Requires clear precedence rules when both the old blocker and preflight blocker are present.

## Recommended Direction

Use Option B or C for startup classification, plus the preservation part of Option D for active resume failures.

The key invariant should be:

> A persisted terminal state must be reportable without Project Context, and a failed preflight must not erase the recovery context for the state it interrupted.

Suggested startup flow:

1. Load persisted state.
2. Classify startup before Project Context preflight.
3. If the startup plan is terminal, return the terminal outcome without mutating state.
4. If Project Context is required, run Project Context preflight and emit the prompt contract snapshot.
5. If preflight fails while no state exists, write a fresh preflight blocker as today.
6. If preflight fails while resuming an active or cancelled state, write blocker evidence that preserves the previous state, transition intent, previous blockers, and required recovery step.
7. If preflight succeeds, invoke `RoadmapResumePlanner` and execute the resulting plan as today.

## Acceptance Criteria

- Existing `EvidenceBlocked` returns `Paused` without Project Context preflight.
- Existing terminal paused states return `Paused` without Project Context preflight.
- Existing `Completed` returns `Completed` without Project Context preflight.
- Existing `Failed` returns `Failed` without Project Context preflight.
- Fresh repositories still fail preflight when required Project Context files are missing.
- Active resumable states still validate Project Context before running the next transition.
- If Project Context fails during active resume, the new blocker preserves the previous current state, last transition, transition intent, blockers, preflight failure details, required next step, and evidence path.
- `Cancelled` recovery either validates Project Context before recovery or records a preflight blocker that preserves the cancelled transition recovery intent.

## Suggested Tests

- `Existing_blocked_state_survives_project_context_preflight_failure`
- `Existing_terminal_paused_state_skips_project_context_preflight_failure`
- `Existing_completed_state_skips_project_context_preflight_failure`
- `Existing_failed_state_skips_project_context_preflight_failure`
- `Fresh_repository_missing_project_context_writes_preflight_blocker`
- `Active_resume_project_context_failure_preserves_previous_state_context`
- `Cancelled_resume_project_context_failure_preserves_recovery_intent`
