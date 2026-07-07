# Plan: Truthful Input-Wait Progress for the CLIs

## Goal

Maximize truthful operator awareness during periods where Codex exposes little
or no direct progress information.

The CLI should stop looking idle after a turn is submitted and before the first
output arrives. It should do that by reporting facts it can observe, modest
derived estimates, and clearly projected terminal status. The implementation
must not imply that LoopRelay can see provider-side token processing progress.

## Current Problem

Long agent turns have a quiet interval:

```text
request submitted -> first visible output
```

During that interval the provider may be queuing, receiving input, applying
cache, performing prefill, or doing other work. The CLI does not see those
internal steps. Today it usually provides no useful feedback, so a normal long
input wait can look like a hang.

## Design Principle

Classify every progress signal by epistemic strength:

| Layer | Meaning | Examples |
| --- | --- | --- |
| Observed | Directly measured by the CLI/runtime. | Request submitted, first protocol event, first visible output, completion, reported tokens. |
| Derived | Computed from observed state and local history. | Estimated prompt tokens, expected first-output latency, confidence. |
| Projected | Human-facing rendering over observed and derived state. | Spinner, status text, progress line. |

The UI is a projection over observed and derived state. It must never present a
projection as provider fact.

## Non-Goals

- Do not show percentages in the first implementation.
- Do not show ETA in the first implementation.
- Do not expose cache-token estimates in normal UI.
- Do not claim that input tokens have been processed unless Codex emits that
  fact directly.
- Do not add a heavy tokenizer dependency before proving the basic experience is
  useful.
- Do not persist high-frequency progress events unless a later visualizer needs
  replay.

## Existing Seams

Use the current runtime and console boundaries:

- `src/LoopRelay.Cli/GatedAgentRuntime.cs` already wraps every main Loop CLI
  one-shot and persistent-session turn.
- `src/LoopRelay.Plan.Cli/PlanSession.cs`, `src/LoopRelay.Plan.Cli/ReviewStep.cs`,
  and `src/LoopRelay.Plan.Cli/SandboxedPromptStep.cs` call agent turns with a
  `ConsoleTurnRenderer`.
- `src/LoopRelay.Roadmap.Cli/RoadmapPromptRunner.cs` and
  `src/LoopRelay.Roadmap.Cli/RoadmapExecutionBridge.cs` do the same for roadmap
  prompts.
- `src/LoopRelay.Infrastructure/Console/ConsoleTurnRenderer.cs` is the shared
  stream renderer and is the natural first-output detection point.
- `src/LoopRelay.Agents/Abstractions/IAgentTokenEstimator.cs` already exists.
  The current `DeterministicAgentTokenEstimator` gives the governed fallback:
  `(text.Length + 3) / 4`.
- `src/LoopRelay.Cli/SessionTelemetryRecorder.cs` already records final per-turn
  usage after completion. Input-wait telemetry should be additive and
  fail-open, matching that philosophy.

## Transport Breakdown

Capture transport phases when the runtime can observe them:

```text
prompt prepared
request write started
request write completed
provider acknowledgement
first protocol event
first visible output
completion
```

Not every transport can expose every phase. Missing timestamps are valid and
should be recorded as null. The important distinction is:

- No request write completion: likely local process/stdin/socket trouble.
- Request written but no acknowledgement: local transport, network, or provider
  acceptance delay.
- Acknowledged but no first event: provider queueing or inference delay.
- First protocol event but no visible output: Codex is alive but has not emitted
  assistant-visible content.

This breakdown helps diagnose waits without pretending to know provider
internals.

## Slice 1: Observed Wait Visibility

Ship only the minimum useful behavior:

- Estimate prompt tokens with `IAgentTokenEstimator`.
- Record `requestSubmittedAt`.
- Detect `firstOutputAt`.
- Record `completedAt`.
- Print elapsed time while waiting for first output.
- Persist one final input-wait row per turn, or add equivalent fields to the
  existing session telemetry row.

TTY display:

```text
[codex] processing input
182k prompt tokens (estimated)
00:42 elapsed
```

Non-TTY display:

```text
[codex] submitted turn: promptTokensEstimated=182000
[codex] waiting for first output: elapsed=30s
[codex] first output: elapsed=42s
```

JSON/event display:

```json
{"event":"input_wait_started","turnIndex":4,"promptTokensEstimated":182000}
{"event":"first_output","turnIndex":4,"elapsedMs":42000}
```

Requirements:

- No percentage.
- No ETA.
- No confidence label.
- No cache estimate in normal output.
- Status/progress goes to stderr or structured event output, never mixed into
  machine-readable stdout.
- If progress rendering fails, the turn continues.

## Slice 2: Historical Expected Range

After Slice 1 has collected enough completed turns, add a small latency history:

- Bucket by session role, transport mode, model/config when available, and prompt
  token range.
- Store sample count and a simple expected range for
  `requestSubmittedAt -> firstOutputAt`.
- Keep state local, versioned, and disposable.

Suggested state file:

```text
.LoopRelay/telemetry/input-wait-model.json
```

TTY display once history is available:

```text
[codex] processing input
about 180k prompt tokens
normally first output arrives between 25-40 seconds
31 seconds elapsed
```

Rules:

- Still no percentage.
- Still no ETA.
- If there is insufficient history, fall back to Slice 1 output.
- Cache inference may improve the internal bucket/model, but should not be shown
  in normal output.

## Slice 3: Optional ETA and Confidence

Only add ETA and confidence after telemetry proves the expected-range model is
useful:

- Add confidence labels only when they are backed by exact-enough inputs and
  enough matching history.
- Round ETA aggressively: `about 20s`, not `17.2s`.
- Consider enabling ETA only in verbose mode at first.
- Keep percentages out unless there is a later explicit decision to accept the
  psychological risk that users read them as completion.

Preferred verbose display:

```text
[codex] processing input | about 180k prompt tokens | elapsed 31s | expected 25-40s | confidence medium
```

Avoid:

```text
[codex] processing input | 54% estimated
```

That percentage means "54% through our expected waiting window", but users will
read it as "54% complete."

## Telemetry Shape

Capture one final row per turn. Add fields to the current session telemetry if
that keeps correlation simpler; otherwise write a sibling JSONL stream under
`.LoopRelay/telemetry/`.

Candidate fields:

| Field | Type | Layer |
| --- | --- | --- |
| `timestamp` | ISO-8601 string | Observed |
| `repoName` | string | Observed |
| `sessionId` | string | Observed |
| `sessionType` | string | Observed |
| `turnIndex` | int | Observed |
| `transport` | string | Observed |
| `model` | string or null | Observed |
| `promptChars` | int | Observed |
| `promptBytes` | int | Observed |
| `promptTokensEstimated` | int | Derived |
| `tokenEstimateSource` | string | Derived |
| `promptPreparedAt` | ISO-8601 string or null | Observed |
| `requestWriteStartedAt` | ISO-8601 string or null | Observed |
| `requestSubmittedAt` | ISO-8601 string or null | Observed |
| `requestAcceptedAt` | ISO-8601 string or null | Observed |
| `firstProtocolEventAt` | ISO-8601 string or null | Observed |
| `firstOutputAt` | ISO-8601 string or null | Observed |
| `completedAt` | ISO-8601 string or null | Observed |
| `reportedPromptTokens` | int or null | Observed |
| `reportedCachedTokens` | int or null | Observed |
| `reportedOutputTokens` | int or null | Observed |
| `status` | string | Observed |
| `estimatorVersion` | string | Derived |

Cache-related fields may be recorded for calibration, but normal UI should not
print them until a later design explicitly handles uncertainty around cache
reuse.

## Rendering Rules

TTY mode:

- Use one stable status area or line while waiting for first output.
- Refresh no more than once per second.
- Show prompt token estimate and elapsed time in Slice 1.
- Replace the wait status with a transition when first output arrives.
- Let normal streaming output take over after first output.

Non-TTY mode:

- Print phase transitions and sparse long-wait lines only.
- Avoid repeated progress spam in CI logs.

JSON mode:

- Emit structured events only.
- Do not mix human progress text into stdout JSON streams.

Quiet mode:

- Suppress routine progress.
- Still show hard stalls, cancellation, and final errors.

Verbose mode:

- May show transport breakdown and historical expected ranges.
- ETA/confidence are Slice 3 features, not Slice 1 defaults.

## Stall Messages

Slice 1 can use conservative static thresholds by prompt-size bucket. Slice 2 can
use historical expected ranges.

Examples:

```text
[codex] still waiting for request acknowledgement: 00:20 elapsed
[codex] still waiting for first output: 02:10 elapsed, about 182k prompt tokens
```

Stall messages should identify the phase that is stalled. They should not
declare provider failure unless the transport or process has actually failed.

## Implementation Steps

1. Add a turn-progress collector around agent turn invocation.
   - It records monotonic elapsed time and wall-clock timestamps.
   - It accepts phase notifications for request submission, protocol event,
     first visible output, and completion.
   - It is best-effort and cancellation-aware.

2. Teach the shared stream renderer to detect first output.
   - Wrap the existing `onChunk` callback.
   - Treat first assistant-visible delta as `firstOutputAt`.
   - Track first tool/protocol event separately when available.
   - Preserve existing duplicate-final-echo suppression.

3. Wire Slice 1 into the main Loop CLI.
   - Start at `GatedAgentRuntime` because it already covers persistent turns and
     one-shots.
   - Use `IAgentTokenEstimator` before submitting each prompt.
   - Ensure session telemetry still records even if progress rendering fails.

4. Wire the same rendering contract into Plan and Roadmap CLIs.
   - Prefer shared infrastructure over per-CLI progress implementations.
   - Keep CLI-specific wrappers thin, mirroring the existing console pattern.

5. Add input-wait telemetry.
   - Prefer additive fields if that does not disturb existing consumers.
   - Otherwise add a sibling JSONL sink with the same rotation/fail-open rules as
     session telemetry.
   - Preserve nulls for unavailable transport phases.

6. Add Slice 2 latency history after Slice 1 data exists.
   - Bucket simply.
   - Store sample count and expected range.
   - Ignore corrupt or outdated state and rebuild from fresh samples.

7. Evaluate Slice 3 separately.
   - Add ETA/confidence only after checking real telemetry.
   - Keep percentage display out unless explicitly re-approved.

## Tests

Add focused tests before broad UI polish:

- Token estimate is shown from `DeterministicAgentTokenEstimator` in Slice 1.
- TTY progress shows elapsed time and estimated prompt tokens, with no ETA or
  percentage.
- Non-TTY progress prints sparse phase lines only.
- JSON mode emits structured events without human progress text.
- First visible output stops the input-wait display and normal stream rendering
  continues.
- Tool/protocol events can be recorded without being mislabeled as visible
  assistant output.
- Missing phase timestamps serialize as null.
- Telemetry sink failure warns or drops the row without breaking the turn.
- Historical expected range is not shown until the bucket has enough samples.
- Cache estimates are not printed in normal mode.

## Acceptance Criteria

- A long quiet turn visibly reports that input is being processed, with elapsed
  time and estimated prompt size.
- The default UI contains no percentages, no ETA, and no cache-token claims.
- Machine-readable output remains parseable.
- The implementation distinguishes observed timestamps from derived estimates in
  telemetry.
- Progress rendering is shared across Loop, Plan, and Roadmap CLIs or has a clear
  path to sharing before Slice 2.
- All progress and telemetry failures are fail-open.
