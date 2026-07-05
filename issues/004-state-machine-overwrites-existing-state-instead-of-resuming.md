# State Machine Overwrites Existing State Instead Of Resuming

## Finding

Verified. `RoadmapStateMachine.RunAsync` always writes `CoreReady` immediately after core preflight:

- `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs`
- `RunAsync`, core preflight section

There is no `RoadmapStateStore.LoadAsync` before this write. The current startup order is:

1. Load north-star context.
2. Emit prompt contract snapshot.
3. Save `.agents/state.md` as `CoreReady` with empty blockers and empty retired exclusions.
4. If the roadmap completion context is missing, bootstrap it.
5. Always run `SelectNextEpic`.

The only `RoadmapStateStore.LoadAsync` call sites inside the state machine are later:

- `SelectNextInitiativeAsync`, where it reads `RetiredEpicExclusions` for selection context.
- `AuditAndPrepareExistingEpicAsync`, where the retire branch appends another exclusion.

Because startup already saved a fresh `CoreReady` document with `[]` exclusions, the selection-time load reads the just-overwritten state, not the previous run's state. That makes the resume bug broader than prompt duplication: ordinary startup can also erase runtime state carried in `.agents/state.md`.

`RoadmapStateStore.LoadAsync` is also too shallow for resume today. It parses only:

- `CurrentState`
- `RetiredEpicExclusions`

It reconstructs `LastTransition` with placeholder values and drops blockers, transition status, active artifact rows, last decision id, split-family count, projection counts, and next transitions. A resume planner cannot reliably recover started, failed, or cancelled transition details from the current loader alone.

This contradicts the file-backed workflow goal: `.agents/state.md` is meant to preserve enough state to resume after cancellation, prompt failure, blocked evidence, or partial completion.

## Impact

Rerunning the CLI can overwrite useful recovery state and duplicate expensive prompt turns. A transition that had already started or completed may be repeated because the persisted current state is ignored.

Examples:

- A cancellation during milestone generation writes `Cancelled`, but the next run overwrites it with `CoreReady` and starts selection again. The cancellation state itself is generic: it records `CoreReady -> Cancelled` rather than the interrupted transition.
- A failed prompt writes `EvidenceBlocked`, but the next run overwrites it before the blocker is evaluated. For `RoadmapStepException`, the inner prompt-transition failure can also be overwritten by the outer catch as a generic `RoadmapStateMachine` blocker.
- A completed active epic preparation may be discarded by a new selection pass instead of resuming at milestone generation.
- A terminal paused selection state such as `StrategicInvestigationRequired`, `RoadmapRevisionRequired`, or `NoSuitableInitiative` can be overwritten by the next invocation even though the previous run deliberately paused.
- Retired exclusions stored in `.agents/state.md` are not reliably preserved across a normal rerun because `SaveStateAsync(..., [], [])` is used by most transitions.

This makes the state document more like a status log than an actual workflow state.

## Verified Design Constraints

- The state enum contains more resume points than the current implementation actually persists. For example, `OperationalContextReady`, `GenerateExecutionPrompt`, `ExecutionPromptReady`, and `ExecutionLoop` exist, but the operational-context generator, execution-prompt generator, materializer, and execution bridge do not save those states.
- `RunPromptTransitionAsync` saves the target state as `Started` before each prompt and `Completed` after each successful prompt, but downstream artifact writes can happen after the completed state write. A planner must verify artifacts instead of trusting state alone.
- State output paths are sometimes coarse. For example, audit and spec bundle transitions record output directories before numbered files are written. Resuming from those states may require the decision ledger, transition journal, lifecycle table, or deterministic latest-file discovery.
- `PromptContractRegistry` already declares required inputs and outputs for prompt transitions. That registry should be reused by any artifact-readiness check instead of duplicating transition prerequisites.
- Issue `005-transition-journal-input-hashes-empty.md` is related. Journal-backed recovery becomes much stronger if journal entries contain durable input hashes.

## Solution Options

### Option 1: Minimal Do-No-Harm Resume Gate

Load `.agents/state.md` before preflight state mutation. If the persisted state is blocked, cancelled, or terminal paused, return the matching outcome without writing `CoreReady`.

Suggested behavior:

- No state file: run the current flow unchanged.
- `EvidenceBlocked`: report the blocker and return failed or paused.
- `Cancelled`: report cancellation and require an explicit retry path.
- `StrategicInvestigationRequired`, `RoadmapRevisionRequired`, `NoSuitableInitiative`: return paused.
- Any active workflow state: either continue current behavior or require `--restart` until a fuller planner exists.

Pros:

- Smallest fix.
- Prevents silent destruction of blocked and paused states.
- Establishes the correct load-before-write invariant.

Cons:

- Does not resume mid-pipeline work.
- Still leaves `LoadAsync` too shallow for reliable transition recovery.
- Requires a later issue to complete artifact-aware resume.

### Option 2: State-And-Artifact Resume Planner

Add a resume planner that decides the next action from the persisted state and artifact lifecycle.

Suggested structure:

```csharp
internal sealed class RoadmapResumePlanner
{
    public RoadmapResumePlan Plan(RoadmapStateDocument? state, RoadmapArtifactSnapshot artifacts);
}
```

The planner should be conservative and artifact-aware:

- If no state exists, start at preflight and bootstrap.
- If state is `CoreReady`, bootstrap completion context only if it is missing; otherwise continue selection.
- If state is `RoadmapCompletionContextReady`, continue selection.
- If state is `SelectNextStrategicInitiative` and `.agents/selection.md` is ready, parse the selection and continue the selected preparation route instead of rerunning selection.
- If state is `EpicPreparationAudit`, use the exact audit evidence path if available; otherwise block rather than guessing.
- If state is `ActiveEpicReady`, generate milestones only when `.agents/epic.md` exists and is ready.
- If state is `MilestoneSpecsReady`, generate missing operational/execution artifacts, but skip already-ready artifacts after validation.
- If state is `ExecutionPromptReady` or `ExecutionLoop`, validate prerequisites and run or resume execution.
- If state is `EvidenceBlocked`, stop and report the blocker unless an explicit retry flag is added later.
- If state is `Cancelled`, resume from the last safe transition boundary only when required inputs still exist.
- If state is terminal paused (`StrategicInvestigationRequired`, `RoadmapRevisionRequired`, `NoSuitableInitiative`), do not proceed automatically.

Separate preflight from state mutation. Preflight should validate north-star files and emit prompt contracts, but it should not overwrite `.agents/state.md` unless the planner chooses a transition that starts at `CoreReady`.

Required supporting changes:

1. Expand `RoadmapStateStore.LoadAsync` to parse `LastTransition`, blockers, active artifacts, last decision id, projection counts, next transitions, and retired exclusions.
2. Add an artifact snapshot abstraction that checks existence plus lifecycle state for known artifacts.
3. Preserve runtime state across `SaveStateAsync` calls instead of passing `[]` for retired exclusions and blockers by default.
4. Add explicit state saves for operational context generation, execution prompt generation, materialization, and execution-loop entry.
5. Route blocked/cancelled/paused states before invoking prompt transitions.

Pros:

- Matches the current file-backed architecture.
- Keeps recovery deterministic without introducing a new workflow engine.
- Can be implemented incrementally with focused tests.

Cons:

- Requires careful artifact validation because current state writes are not always aligned with artifact writes.
- Needs parser work in `RoadmapStateStore`.

### Option 3: Journal-Backed Recovery Planner

Make the transition journal the primary recovery source. On startup, read `.agents/journal/transitions.jsonl`, group records by correlation id, and identify the latest transition as started, completed, failed, or missing completion. Use `.agents/state.md` as the human-readable projection, not the sole authority.

This option should be paired with issue 005 so `InputArtifactHashes` are populated for each transition.

Pros:

- Better at detecting interrupted transitions than state alone.
- Can distinguish started-without-completed from completed-before-artifact-write if the journal and artifact snapshot disagree.
- Provides an audit trail for why a resume decision was chosen.

Cons:

- Requires a journal reader, correlation grouping, and reconciliation rules.
- Current journal entries have empty input hashes, so this option is incomplete until issue 005 is fixed.
- Still needs state parsing for blockers and human-facing paused states unless those move into structured journal records.

### Option 4: Declarative Workflow Graph

Replace the ad hoc top-level `RunAsync` sequence with a transition graph where each transition declares:

- source states
- target state
- required inputs
- required outputs
- idempotency behavior
- resume behavior
- blocked/paused routing

`RunAsync` would repeatedly ask the graph for the next legal transition given the persisted state and artifact snapshot.

Pros:

- Strongest long-term model.
- Reduces drift between enum states, prompt contracts, lifecycle rows, and actual execution order.
- Makes future states easier to add safely.

Cons:

- Largest change.
- Higher risk for an MVP CLI unless implemented after the state-and-artifact planner proves the transition boundaries.

## Recommended Path

Implement Option 2 first, with the load-before-write invariant from Option 1 as the first slice. Defer full journal-backed recovery until input hashes are fixed, but shape the planner so a journal snapshot can be added later.

Do not treat `CoreReady` as the default startup state when a state file exists. The only automatic `CoreReady` write should happen when the planner has determined this is a fresh repository or an explicit restart.

## Acceptance Criteria

- Existing `.agents/state.md` is loaded before writing a new state.
- Rerun behavior is determined by persisted current state and artifact readiness.
- Blocked and terminal paused states are not silently overwritten.
- Started-but-not-completed transitions have deterministic recovery behavior.
- The CLI can still initialize a fresh repository with no state file.
- `RoadmapStateStore.LoadAsync` round-trips the fields needed for resume, including last transition status and blockers.
- `SaveStateAsync` preserves durable runtime state such as retired exclusions unless the caller deliberately changes it.
- Operational context generation, execution prompt generation, materialization, and execution-loop entry have explicit persisted states or are covered by artifact-based resume rules.

## Suggested Tests

- Existing `ActiveEpicReady` state with ready `.agents/epic.md` resumes at milestone generation without calling `SelectNextEpic`.
- Existing `EvidenceBlocked` state returns a blocked outcome without overwriting the blocker or writing `CoreReady`.
- Existing `StrategicInvestigationRequired` state returns paused without calling `SelectNextEpic`.
- Missing state still performs the normal bootstrap and selection flow.
- Existing `SelectNextStrategicInitiative` state with ready `.agents/selection.md` parses the selection and continues preparation without rerunning selection.
- Existing `MilestoneSpecsReady` state with ready specs but missing operational context generates operational context and execution prompt without rerunning milestone prompts.
- Existing `Cancelled` state can be retried from the last safe transition boundary only when required inputs still exist.
- State-store round-trip test covers current state, last transition fields, blockers, active artifact rows, next transitions, and retired exclusions.
- Regression test proves startup preserves retired exclusions from the previous state document.
