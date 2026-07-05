# Preflight Overwrites Existing Blocked State

## Finding

`RoadmapStateMachine.RunAsync` loads persisted state before preflight, but it still performs Project Context preflight before invoking the resume planner. If preflight fails, the existing state is overwritten with a new generic preflight blocker.

Relevant code:

- `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs`
- `src/CommandCenter.Roadmap.CLI/RoadmapResumePlanner.cs`

Current order:

1. Load `.agents/state.md`.
2. Load Project Context.
3. Emit prompt contract snapshot.
4. If preflight fails, write `EvidenceBlocked` for `Preflight`.
5. Only after preflight succeeds, invoke `RoadmapResumePlanner`.

`RoadmapResumePlanner` does know how to stop on an existing `EvidenceBlocked` state, but it never gets called when Project Context preflight fails.

## Impact

An existing blocked state is preserved only while Project Context is healthy. If the repository is already blocked for a specific transition and a Project Context file is later missing, renamed, or temporarily corrupt, rerunning the CLI replaces the original blocker with a preflight blocker.

The original recovery intent, evidence paths, and last transition can be lost from `.agents/state.md`.

## Root Cause

Startup has two separate gates:

- preflight validation
- resume planning

The preflight gate can mutate state before the resume planner has a chance to decide whether the persisted state should be terminal, paused, blocked, resumed, or restarted.

## Proposal

Make startup planning state-aware before any mutating preflight action.

Suggested order:

1. Load persisted state.
2. If persisted state is terminal paused, blocked, failed, or completed, return the matching outcome without Project Context preflight.
3. If persisted state is `Cancelled`, decide whether Project Context is required for the selected recovery route before mutating state.
4. For active resumable workflow states, run Project Context preflight because the next transition needs it.
5. If preflight fails for an active workflow, write a preflight blocker that preserves the previous state as recovery context.
6. If no state exists, run fresh preflight and initialize as today.

This can be expressed as a lightweight startup classifier:

```csharp
internal sealed class RoadmapStartupPlanner
{
    public RoadmapStartupPlan PlanBeforePreflight(RoadmapStateDocument? persistedState);
}
```

Possible preflight requirements:

- `None`: terminal/blocked states do not need Project Context just to report status.
- `RequiredForResume`: active states need Project Context to validate projections and inputs.
- `RequiredForInitialize`: fresh repositories need Project Context to initialize.

When preflight fails after an active state is selected, persist a blocker that includes:

- previous current state
- previous transition intent
- preflight failure details
- required next step
- evidence path

Do not replace an existing terminal blocked state with a new preflight blocker unless the user explicitly asks for a restart or repair action.

## Acceptance Criteria

- Existing `EvidenceBlocked` state returns `Paused` without Project Context preflight.
- Existing terminal paused states return `Paused` without Project Context preflight.
- Existing `Completed` state returns `Completed` without Project Context preflight.
- Fresh repositories still fail preflight when required Project Context files are missing.
- Active resumable states still validate Project Context before running the next transition.
- If Project Context fails during active resume, the new blocker preserves the prior state and transition intent.

## Suggested Tests

- `Existing_blocked_state_survives_project_context_preflight_failure`
- `Existing_terminal_paused_state_skips_project_context_preflight`
- `Existing_completed_state_skips_project_context_preflight`
- `Fresh_repository_missing_project_context_writes_preflight_blocker`
- `Active_resume_project_context_failure_preserves_previous_state_context`
