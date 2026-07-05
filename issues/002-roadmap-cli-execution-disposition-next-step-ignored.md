# Roadmap CLI execution disposition next step ignored

## Finding

`ExecutionDispositionParser` parses `Status`, `Confidence`, `Evidence Summary`, and `Next Step`, but `RoadmapExecutionOutcomeInterpreter` routes only on `Status`. Contradictory output such as `Status = Epic Complete` with `Next Step = ContinueExecution` is treated as an epic completion claim.

## Impact

The execution bridge is a high-trust boundary because it runs with workspace write access. Its final disposition controls whether the roadmap proceeds to completion certification, pauses for continuation, or blocks. Ignoring `Next Step` weakens that boundary and allows internally inconsistent execution output to drive the wrong workflow branch.

## Evidence

- `ExecutionDispositionParser.Parse` captures `Next Step`.
- `RoadmapExecutionOutcomeInterpreter.Interpret` switches only on `disposition.Status`.
- Current tests accept all statuses with `Next Step = ContinueExecution`, including `Epic Complete`.

## Proposal

Validate execution disposition as a coherent typed command.

A robust approach:

1. Replace free-form `NextStep` handling with an enum.
2. Define the one valid command pair for each status:
   - `Epic Complete` requires `EvaluateEpicCompletionAndDrift`.
   - `Continue Required` requires `ContinueExecution`.
   - `Execution Blocked` requires `ResolveExecutionBlocker`.
3. Treat mismatched status/next-step pairs as malformed execution output and persist the raw output as evidence, using the existing `PersistMalformedExecutionOutputAsync` path.
4. Include the validated command in `RoadmapExecutionOutcome` so downstream code can route from the validated command rather than repeating string logic.
5. Add parser/interpreter tests for all valid pairs and for each mismatched pair.

This keeps the human-readable disposition table while making it a defensible protocol.

## Acceptance criteria

- Execution output with mismatched `Status` and `Next Step` is rejected as malformed.
- Valid execution disposition pairs continue to route exactly as today.
- Tests prove `Epic Complete + ContinueExecution` no longer reaches completion certification.
