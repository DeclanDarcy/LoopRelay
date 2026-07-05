# Execution Bridge Treats Any Completed Turn As Epic Complete

## Audit Status

Verified against the current codebase.

The original finding is correct: `RoadmapExecutionBridge` treats transport completion from the execution agent as domain completion of the active epic. The audit found two additional details that make the issue more concrete:

- `RoadmapExecutionBridgeResult.BlockedResult` exists, but production code never creates it and `RoadmapStateMachine` does not branch on `Blocked` even if it were returned.
- `RoadmapState.ExecutionBlocked` exists and `RoadmapResumePlanner` treats it as a terminal paused state, but `RunExecutionAndCertificationAsync` never writes it.

## Verified Code Paths

Relevant code:

- `src/CommandCenter.Roadmap.CLI/RoadmapExecutionBridge.cs`
- `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs`
- `src/CommandCenter.Roadmap.CLI/RoadmapState.cs`
- `src/CommandCenter.Roadmap.CLI/RoadmapResumePlanner.cs`
- `src/CommandCenter.Roadmap.CLI/ExecutionPromptGenerator.cs`
- `tests/CommandCenter.Roadmap.CLI.Tests/RoadmapStateMachineSelectionTests.cs`

Confirmed behavior:

1. `RoadmapExecutionBridge.RunAsync` reads `.agents/execution-prompt.md`, invokes `AgentSpecs.ExecutionBridge(repository)`, echoes the agent output, and returns `RoadmapExecutionBridgeResult.Completed(result.Output)` for every `AgentTurnState.Completed`.
2. Non-completed agent states become `RoadmapExecutionBridgeResult.Failed(...)`.
3. `RoadmapExecutionBridgeResult.BlockedResult(...)` is only defined on the record; no production call site returns it.
4. `RunExecutionAndCertificationAsync` saves `ExecutionLoop` as `Started`, marks `.agents/epic.md` lifecycle as `Executing`, calls the bridge, and checks only `bridge.EpicCompleted`.
5. If `bridge.EpicCompleted` is `false`, the method throws `RoadmapStepException(bridge.Message)`. It does not inspect `bridge.Blocked`, persist execution evidence, or write `ExecutionBlocked`.
6. If `bridge.EpicCompleted` is `true`, the method writes `EpicCompletionDetected`, then always runs `EvaluateEpicCompletionAndDrift`.
7. The completion router can later route `Closure Recommendation = Continue Epic` back to `ExecutionLoop`, but this happens only after the system has already recorded `EpicCompletionDetected` and run completion certification.
8. `ExecutionPromptGenerator` renders execution scope, active epic, first spec, and operational context. It does not require a machine-readable execution disposition.
9. The roadmap state-machine tests inject a private `FakeRoadmapExecutionBridge` that always returns `RoadmapExecutionBridgeResult.Completed("done")`. They do not exercise the production bridge mapping or blocked/continue execution outcomes.

## Impact

A cleanly stopped execution-agent turn is not evidence that the active epic is complete. It only proves the process reached a completed transport state.

False-positive paths include:

- The execution agent completes one milestone and reports that more work remains.
- The execution agent reports that it is blocked by missing evidence, unsafe changes, failing tests, dependency problems, or required human input.
- The execution agent produces malformed, empty, or advisory output while still exiting cleanly.
- The execution agent asks for continuation because the work is too large for one turn.

All of those currently flow into `EpicCompletionDetected` and trigger `EvaluateEpicCompletionAndDrift`. Depending on the evaluation output, this can:

- record a misleading transition history,
- spend an expensive certification turn on an execution state that was never complete,
- let a weak completion evaluation close the epic,
- update roadmap completion context from a false completion premise,
- leave the existing `ExecutionBlocked` state unused,
- hide the distinction between runtime failure, domain blocker, and ordinary continuation.

This issue interacts with `issues/002-prompt-failure-state-overwritten-by-generic-blocker.md`: if a future bridge returns `BlockedResult` today, the state machine still throws a generic `RoadmapStepException`, and the top-level catch path can persist a generic `EvidenceBlocked` state instead of a precise execution blocker.

## Root Cause

The execution bridge has no domain output contract. It collapses two separate concepts into one boolean:

- transport state: did the agent turn finish cleanly?
- roadmap state: is the active epic complete, blocked, or still in progress?

Other roadmap transitions already separate these layers. For example:

- `SelectionParser` parses `## Recommendation Summary`.
- `EpicPreparationAuditParser` parses `## Audit Disposition`.
- `CompletionEvaluationParser` parses `## Evaluation Summary`.
- `ArtifactPromotionService` classifies and validates epic-authoring output before writing `.agents/epic.md`.

Execution is the outlier: a completed process result is treated as a domain transition without parsing or validation.

## Solution Options

### Option 1: Strict Execution Disposition Contract

Add a required `## Execution Disposition` section to execution-agent output and parse it before returning a bridge result.

Suggested output contract:

```markdown
## Execution Disposition

| Field | Value |
|---|---|
| Status | Epic Complete |
| Confidence | High |
| Evidence Summary | Tests pass and all milestone acceptance criteria are satisfied. |
| Next Step | EvaluateEpicCompletionAndDrift |
```

Allowed `Status` values:

- `Epic Complete`
- `Execution Blocked`
- `Continue Required`

Suggested parser shape:

```csharp
internal sealed class ExecutionBridgeDispositionParser
{
    public ExecutionBridgeDisposition Parse(string markdown);
}

internal sealed record ExecutionBridgeDisposition(
    ExecutionBridgeStatus Status,
    string Confidence,
    string EvidenceSummary,
    string NextStep);

internal enum ExecutionBridgeStatus
{
    EpicComplete,
    ExecutionBlocked,
    ContinueRequired,
}
```

State-machine routing:

- `EpicComplete`: save `EpicCompletionDetected`, then run `EvaluateEpicCompletionAndDrift`.
- `ExecutionBlocked`: persist the bridge output as blocker evidence, save `ExecutionBlocked` or `EvidenceBlocked` with `Paused`, and return `RoadmapOutcome.Paused`.
- `ContinueRequired`: save `ExecutionLoop` with `Paused`, keep transition intent `ContinueExecution`, keep active epic lifecycle as `Executing`, and do not run completion certification.
- malformed or missing disposition: persist parser failure evidence and block without running completion certification.
- transport failure: keep it distinct from domain blocker and persist a runtime failure state once.

Pros:

- Smallest change that fixes the incorrect domain transition.
- Reuses existing markdown table parser style.
- Makes `BlockedResult` meaningful or replaces it with a typed result.
- Can use the existing `ExecutionBlocked` state.

Cons:

- Trusts execution-agent self-reporting unless paired with later certification.
- Requires prompt updates and regression tests around malformed output.

### Option 2: Minimal Completion Claim Gate

Require a narrow explicit completion marker, such as an `## Epic Completion Claim` table, and treat any completed turn without that marker as `Continue Required`.

Example:

```markdown
## Epic Completion Claim

| Field | Value |
|---|---|
| Completed | Yes |
| Evidence | ... |
```

Pros:

- Low implementation cost.
- Prevents arbitrary clean exits from becoming epic completion.
- Can be introduced without modeling every execution outcome immediately.

Cons:

- Still needs a blocked/continue convention.
- Boolean markers are easier for agents to misuse than a full disposition contract.
- Does not make `ExecutionBlocked` a first-class path unless additional work is added.

### Option 3: Typed Bridge Result Without Immediate Prompt Contract

First replace `RoadmapExecutionBridgeResult(bool EpicCompleted, bool Blocked, string Message)` with a typed outcome:

```csharp
internal sealed record RoadmapExecutionBridgeResult(
    RoadmapExecutionBridgeOutcome Outcome,
    string Message);

internal enum RoadmapExecutionBridgeOutcome
{
    EpicComplete,
    ExecutionBlocked,
    ContinueRequired,
    RuntimeFailed,
    MalformedOutput,
}
```

Then update `RunExecutionAndCertificationAsync` to branch exhaustively on the enum. The parser or classifier can initially be conservative, for example:

- non-completed transport state -> `RuntimeFailed`
- completed output with explicit completion evidence -> `EpicComplete`
- completed output with blocked heading -> `ExecutionBlocked`
- all other completed output -> `ContinueRequired` or `MalformedOutput`

Pros:

- Removes the ambiguous boolean API first.
- Forces the state machine to handle blocked and continue outcomes explicitly.
- Allows an incremental parser rollout.

Cons:

- Without a prompt contract, classification will be heuristic.
- Risk of temporary rules becoming permanent string matching.

### Option 4: Evaluate Progress After Every Execution Turn

Rename the post-execution transition from "completion detected" to "execution turn finished", then run a progress evaluation prompt after every completed execution turn. That evaluator, not the execution bridge, decides whether to close, continue, reopen, gather evidence, or block.

This is a larger semantic correction:

- replace or supplement `EpicCompletionDetected` with a state such as `ExecutionTurnCompleted` or `ExecutionReviewRequired`,
- change `EvaluateEpicCompletionAndDrift` into a broader `EvaluateExecutionProgressAndDrift`,
- let its parser route `Close Epic`, `Continue Epic`, `Execution Blocked`, `Reopen Epic`, or `Gather More Evidence`.

Pros:

- Reduces reliance on execution-agent self-certification.
- Preserves the existing idea that an independent evaluation prompt should inspect repository reality.
- Avoids recording `EpicCompletionDetected` before completion is actually evaluated.

Cons:

- Larger contract and naming migration.
- More expensive if every partial execution turn triggers a review prompt.
- Still needs a blocked outcome and state persistence.

### Option 5: File-Based Execution Evidence Gate

Use execution artifacts as the completion boundary. For example, require `.agents/plan.md` and generated milestone checklists to show all required items complete, plus an explicit final handoff or completion report, before the bridge can return `EpicComplete`.

Pros:

- Grounds completion in repository artifacts instead of final chat text alone.
- Aligns with `ExecutionCompatibilityMaterializer`, which already creates `.agents/plan.md` and milestone files.
- Can support resumable execution more naturally.

Cons:

- More invasive because it needs a durable execution evidence model.
- Requires clear rules for checklist ownership, partial completion, and stale milestone files.
- Might duplicate some responsibility currently assigned to completion evaluation.

## Recommended Path

Use Option 1 as the near-term fix, with the typed enum shape from Option 3. It is the smallest correction that:

- stops treating transport completion as epic completion,
- uses the existing markdown parser pattern,
- gives `ExecutionBlocked` a real production path,
- keeps completion certification for actual completion claims,
- makes tests straightforward.

If the roadmap CLI wants stronger independent verification, follow with Option 4 after the immediate false-positive transition is removed.

## Acceptance Criteria

- A completed execution turn with `Status = Epic Complete` proceeds to `EpicCompletionDetected` and then completion evaluation.
- A completed execution turn with `Status = Execution Blocked` does not run completion evaluation and persists blocker evidence.
- A completed execution turn with `Status = Continue Required` leaves the workflow paused at `ExecutionLoop` with `ContinueExecution`.
- A completed execution turn without a valid disposition is treated as malformed and does not run completion evaluation.
- A non-completed execution-agent turn remains a transport/runtime failure, not a domain blocker.
- `RoadmapState.ExecutionBlocked` is either used by production code or removed from the state model.
- `RoadmapExecutionBridgeResult.BlockedResult` is either used by production code or replaced by an exhaustive typed outcome.
- The execution prompt includes the required disposition contract.
- State-machine tests can exercise completed, blocked, continue, malformed, and transport-failure bridge outcomes.

## Suggested Tests

- `ExecutionBridge_parses_epic_complete_disposition`
- `ExecutionBridge_parses_execution_blocked_disposition`
- `ExecutionBridge_parses_continue_required_disposition`
- `ExecutionBridge_rejects_completed_output_without_disposition`
- `ExecutionBridge_transport_failure_remains_runtime_failure`
- `RoadmapStateMachine_epic_complete_bridge_output_runs_completion_evaluation`
- `RoadmapStateMachine_blocked_bridge_output_persists_execution_blocker`
- `RoadmapStateMachine_continue_required_bridge_output_pauses_execution_loop`
- `RoadmapStateMachine_malformed_bridge_output_blocks_without_completion_evaluation`
- `ExecutionPromptGenerator_renders_execution_disposition_contract`
