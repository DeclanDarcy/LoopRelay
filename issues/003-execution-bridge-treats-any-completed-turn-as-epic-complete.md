# Execution Bridge Treats Any Completed Turn As Epic Complete

## Finding

`RoadmapExecutionBridge` maps every completed execution-agent turn to `EpicCompleted = true`.

Relevant code:

- `src/CommandCenter.Roadmap.CLI/RoadmapExecutionBridge.cs`
- `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs`

Current behavior:

```csharp
return result.State == AgentTurnState.Completed
    ? RoadmapExecutionBridgeResult.Completed(result.Output)
    : RoadmapExecutionBridgeResult.Failed(...);
```

The state machine then treats that result as real epic completion:

1. saves `ExecutionLoop` as started
2. runs the execution bridge
3. if `EpicCompleted`, saves `EpicCompletionDetected`
4. runs `EvaluateEpicCompletionAndDrift`

There is a `BlockedResult` factory on `RoadmapExecutionBridgeResult`, but the bridge never produces it.

## Impact

The CLI can run completion certification after a normal completed agent turn that did not actually complete the epic. A completed process only proves the agent stopped cleanly; it does not prove that all milestone specs are satisfied, that the active epic is done, or that execution was not blocked.

This can create false-positive completion routing, updating roadmap completion context or closing/continuing epics based on an execution output that was never parsed as an execution disposition.

## Root Cause

The execution bridge lacks a domain output contract. Runtime prompt outputs have parsers such as `SelectionParser`, `EpicPreparationAuditParser`, and `CompletionEvaluationParser`, but execution bridge output is interpreted only through process state.

## Proposal

Define and enforce an execution-bridge disposition contract.

Suggested output section:

```markdown
## Execution Disposition

| Field | Value |
|---|---|
| Status | Epic Complete | Execution Blocked | Continue Required |
| Confidence | High | Medium | Low |
| Evidence Summary | ... |
| Next Step | ... |
```

Add a parser:

```csharp
internal sealed class ExecutionBridgeDispositionParser
{
    public ExecutionBridgeDisposition Parse(string markdown);
}

internal sealed record ExecutionBridgeDisposition(
    string Status,
    string Confidence,
    string EvidenceSummary,
    string NextStep);
```

Route statuses explicitly:

- `Epic Complete`: return `RoadmapExecutionBridgeResult.Completed(output)`
- `Execution Blocked`: return `RoadmapExecutionBridgeResult.BlockedResult(output)`
- `Continue Required`: save/return a paused execution-loop outcome without running completion certification

Then update `RunExecutionAndCertificationAsync`:

- if completed, proceed to `EpicCompletionDetected`
- if blocked, persist `ExecutionBlocked` or `EvidenceBlocked` with the execution output as evidence
- if continue required, leave `ExecutionLoop` paused with `ContinueExecution`
- if malformed, block with parser evidence and do not run completion certification

This mirrors the rest of the roadmap CLI: completed agent turn means a transport success; a parser decides the domain transition.

## Acceptance Criteria

- A completed execution turn with `Status = Epic Complete` proceeds to completion evaluation.
- A completed execution turn with `Status = Execution Blocked` does not run completion evaluation and persists blocker evidence.
- A completed execution turn with `Status = Continue Required` keeps the workflow at `ExecutionLoop` with `ContinueExecution`.
- A completed execution turn without a disposition table is treated as malformed and blocked.
- `RoadmapExecutionBridgeResult.BlockedResult` is used by production code or removed.

## Suggested Tests

- `ExecutionBridge_completed_disposition_routes_to_completion_detection`
- `ExecutionBridge_blocked_disposition_persists_execution_blocker`
- `ExecutionBridge_continue_required_does_not_run_completion_evaluation`
- `ExecutionBridge_malformed_completed_output_blocks`
- `ExecutionBridge_transport_failure_remains_runtime_failure`
