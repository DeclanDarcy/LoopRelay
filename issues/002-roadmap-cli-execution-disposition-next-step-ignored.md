# Roadmap CLI execution disposition next step ignored

## Verification status

Verified against the current codebase.

## Finding

`ExecutionDispositionParser` parses `Status`, `Confidence`, `Evidence Summary`, and `Next Step`, but `RoadmapExecutionOutcomeInterpreter` routes only on `Status`. The `Next Step` field is required but opaque: it is not checked against the allowed command vocabulary, and it is not checked for consistency with the status.

Contradictory output such as:

```markdown
| Status | Epic Complete |
| Confidence | High |
| Evidence Summary | More execution work remains. |
| Next Step | ContinueExecution |
```

is interpreted as `RoadmapExecutionOutcomeKind.EpicComplete` and sent to completion certification. The inverse mismatches are also possible: `Continue Required` with `EvaluateEpicCompletionAndDrift` still routes to continuation, and `Execution Blocked` with `ContinueExecution` still routes to execution-blocked handling.

## Impact

The execution bridge is a high-trust boundary because it runs with workspace write access. Its final disposition controls whether the roadmap proceeds to completion certification, pauses for continuation, or blocks. Ignoring `Next Step` weakens that boundary and allows internally inconsistent execution output to drive the wrong workflow branch.

This is a boundary failure rather than a presentation issue. The roadmap runtime already has a safe malformed-output path, but it is not used when the disposition table is syntactically valid and semantically contradictory. A single inconsistent table can therefore:

- trigger `EvaluateEpicCompletionAndDrift` even though the execution agent commanded `ContinueExecution`;
- classify execution evidence as ready instead of blocked;
- ask the completion evaluator to reason from an execution claim the agent's own next-step command contradicted;
- hide the contradiction in normal evidence rather than surfacing it as a protocol violation.

## Evidence

- `src/CommandCenter.Roadmap.CLI/RoadmapExecutionOutcomeInterpreter.cs`
  - `ExecutionDispositionParser.Parse` validates `Status` through a known-status dictionary and validates `Confidence`, but stores `Next Step` via `Required(fields, "Next Step")` with no allowed-value or pair validation.
  - `RoadmapExecutionOutcomeInterpreter.Interpret` switches only on `disposition.Status` and returns `EpicComplete`, `ContinueRequired`, or `ExecutionBlocked` from that field alone.
  - `ExecutionDisposition.NextStep` is a `string`, so downstream code cannot distinguish a validated command from arbitrary table text.
- `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs`
  - `RunExecutionAndCertificationAsync` routes from `executionOutcome.Kind`.
  - `EpicComplete` immediately calls `RunCompletionCertificationAsync`.
  - `ContinueRequired`, `ExecutionBlocked`, `MalformedOutput`, and `RuntimeFailure` already have distinct persistence paths, including `PersistMalformedExecutionOutputAsync` for contract failures.
  - `PersistExecutionEvidenceAsync` marks `EpicComplete` and `ContinueRequired` evidence as `Ready`; a mismatched `Epic Complete + ContinueExecution` disposition therefore becomes ready evidence instead of blocked evidence.
- `src/CommandCenter.Roadmap.CLI/ExecutionPromptGenerator.cs`
  - The prompt lists `Status` values and `Next Step` values as separate vocabularies, but does not state the one valid next-step command for each status.
- `tests/CommandCenter.Roadmap.CLI.Tests/RoadmapExecutionOutcomeInterpreterTests.cs`
  - `Completed_transport_is_interpreted_from_explicit_execution_disposition` parameterizes over all statuses.
  - Its helper always emits `| Next Step | ContinueExecution |`, so the test suite currently accepts `Epic Complete + ContinueExecution` and `Execution Blocked + ContinueExecution`.
- `tests/CommandCenter.Roadmap.CLI.Tests/RoadmapStateMachineExecutionRoutingTests.cs`
  - The state-machine route tests cover the three valid status/next-step pairs, but do not include contradictory pairs.

## Concrete failure path

1. The execution bridge returns a completed transport result with a syntactically valid `## Execution Disposition` table.
2. The table says `Status = Epic Complete` and `Next Step = ContinueExecution`.
3. `ExecutionDispositionParser.Parse` returns an `ExecutionDisposition` because all required fields are present.
4. `RoadmapExecutionOutcomeInterpreter.Interpret` ignores `NextStep` and returns `RoadmapExecutionOutcomeKind.EpicComplete`.
5. `RoadmapStateMachine.RunExecutionAndCertificationAsync` persists execution evidence and calls `RunCompletionCertificationAsync`.
6. The state moves through `EpicCompletionDetected` toward `EvaluateEpicCompletionAndDrift` instead of pausing as malformed execution output.

Expected behavior: the interpreter should reject this as malformed execution output, preserve the raw execution output as evidence, and route through the existing `ResolveMalformedExecutionOutput` recovery path.

## Scope notes

- Runtime transport failures are already distinct from malformed domain output and should remain `RuntimeFailure`.
- The existing malformed-output state path is suitable for this issue; the missing piece is classifying contradictory disposition pairs as malformed before outcome routing.
- This issue is separate from `issues/001-roadmap-cli-completion-routing-trusts-contradictory-recommendation.md`. Issue 002 guards the execution-to-certification handoff; issue 001 guards the later completion-certification route.

## Proposal

Validate execution disposition as a coherent typed command.

### Recommended approach: typed disposition command

1. Add an enum for the next-step command, for example:
   - `EvaluateEpicCompletionAndDrift`
   - `ContinueExecution`
   - `ResolveExecutionBlocker`
2. Parse `Next Step` into that enum instead of storing it only as a string.
3. Define the valid status/command pairs in one place:
   - `Epic Complete` requires `EvaluateEpicCompletionAndDrift`.
   - `Continue Required` requires `ContinueExecution`.
   - `Execution Blocked` requires `ResolveExecutionBlocker`.
4. Treat unknown commands and mismatched status/command pairs as malformed execution output. Include both values in the diagnostic message.
5. Include the validated command in `ExecutionDisposition` or `RoadmapExecutionOutcome` so downstream state persistence can record the actual validated command without repeating string logic.
6. Update `ExecutionPromptGenerator` to show the same one-to-one mapping the parser enforces.

This keeps the human-readable disposition table while making it a defensible protocol.

## Solution options

### Option A: minimal parser guard

Keep `NextStep` as a string, add an allowed-value set, and compare it with a `Dictionary<ExecutionDispositionStatus, string>` in `ExecutionDispositionParser.Parse`.

Pros:

- Smallest code change.
- Reuses the existing `MarkdownParseException` to reach `MalformedOutput`.
- Low risk to state-machine behavior for valid output.

Cons:

- Leaves command handling stringly typed.
- Prompt generation and parser validation can drift unless the expected-pair map is shared.
- Downstream code still cannot know whether `NextStep` was validated except by trusting parser construction.

### Option B: typed command enum in the interpreter

Introduce `ExecutionDispositionNextStep`, parse `Next Step` into that enum, validate the pair, and store the enum on `ExecutionDisposition`. Derive `RoadmapExecutionOutcomeKind` only after validation.

Pros:

- Makes invalid commands unrepresentable after parsing.
- Gives tests a clear protocol surface.
- Keeps the change localized to the execution interpretation boundary and state evidence rendering.
- Provides a better base for future execution outcomes.

Cons:

- Touches more call sites than Option A because evidence rendering must convert the enum back to display text.
- Still leaves route policy split between the interpreter and `RoadmapStateMachine` unless the route metadata is also centralized.

### Option C: central execution disposition contract table

Create a small contract table with rows such as:

| Status | Next Step | Outcome Kind | Transition Intent |
|---|---|---|---|
| Epic Complete | EvaluateEpicCompletionAndDrift | EpicComplete | EvaluateEpicCompletionAndDrift |
| Continue Required | ContinueExecution | ContinueRequired | ContinueExecution |
| Execution Blocked | ResolveExecutionBlocker | ExecutionBlocked | ResolveExecutionBlocker |

Use that table for parser validation, prompt generation, and tests.

Pros:

- Best drift resistance: prompt vocabulary, parser validation, and route metadata share one authority.
- Makes adding a future execution outcome explicit.
- Reduces duplicated string literals.

Cons:

- Larger refactor than needed for the immediate bug.
- Requires care not to overfit state-machine routing into a generic table before the surrounding workflow needs it.

### Option D: make `Next Step` the authoritative command, with status cross-checking

Route from a parsed next-step command, but reject the output when `Status` does not match the command's required status.

Pros:

- Aligns runtime behavior with the imperative field name, `Next Step`.
- Avoids treating a descriptive status as the command source.

Cons:

- Still needs the same pair validation to be safe.
- The current outcome model is status-named (`EpicComplete`, `ContinueRequired`, `ExecutionBlocked`), so this is a larger semantic shift than Option B.

### Option E: prompt-only clarification

Update `ExecutionPromptGenerator` to show the three valid rows instead of independent OR lists.

Pros:

- Useful as a companion change to reduce agent mistakes.
- Very low implementation cost.

Cons:

- Insufficient by itself. The execution boundary must defend against malformed or contradictory agent output at runtime.

## Test plan

- Update `RoadmapExecutionOutcomeInterpreterTests`:
  - valid pair: `Epic Complete + EvaluateEpicCompletionAndDrift` returns `EpicComplete`;
  - valid pair: `Continue Required + ContinueExecution` returns `ContinueRequired`;
  - valid pair: `Execution Blocked + ResolveExecutionBlocker` returns `ExecutionBlocked`;
  - unknown `Next Step` returns `MalformedOutput`;
  - each mismatched status/next-step pair returns `MalformedOutput`.
- Add a state-machine regression test in `RoadmapStateMachineExecutionRoutingTests`:
  - `Epic Complete + ContinueExecution` routes to `EvidenceBlocked`;
  - `TransitionIntent.Intent` is `ResolveMalformedExecutionOutput`;
  - no completion evaluation evidence is written;
  - the completion evaluation prompt is not invoked;
  - raw execution output remains in execution evidence.
- Add or update `ExecutionPromptGeneratorTests` to assert that the generated prompt contains the paired mapping, not only independent OR vocabularies.
- Keep the existing valid route tests passing unchanged for valid pairs.

## Recommended implementation order

1. Implement Option B in `RoadmapExecutionOutcomeInterpreter.cs`.
2. Update `ExecutionPromptGenerator` to emit the paired mapping.
3. Add interpreter tests for valid pairs, unknown commands, and mismatches.
4. Add the state-machine regression for `Epic Complete + ContinueExecution`.
5. If more execution outcomes are expected soon, promote the pair mapping into Option C's central contract table as a follow-up.

## Acceptance criteria

- Execution output with mismatched `Status` and `Next Step` is rejected as malformed.
- Execution output with an unknown `Next Step` is rejected as malformed.
- Valid execution disposition pairs continue to route exactly as today.
- Tests prove `Epic Complete + ContinueExecution` no longer reaches completion certification.
- Malformed execution disposition evidence preserves the raw execution output and routes to `ResolveMalformedExecutionOutput`.
- The generated execution prompt communicates the valid status/next-step pairs.
