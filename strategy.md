# Strategy: Input Token Processing Progress

## Problem

The CLIs can spend a long time after a turn is submitted and before the first
visible output arrives. In that interval the model may be receiving the request,
queuing, applying cache, and processing input tokens. From the operator's point
of view the CLI looks idle even though useful work may be happening.

The goal is to capture enough timing and token data to estimate that hidden
phase over time, and to print progress in a way that is useful without pretending
we have direct visibility into the provider's internal prefill progress.

## Operating Principles

- Separate **observed** facts from **estimated** progress.
- Never print `100%` for input processing until an actual first-output signal is
  observed.
- Prefer elapsed time, estimated token count, and confidence labels over false
  precision.
- Keep terminal output stable and low-noise; use stderr/status rendering for
  human progress and preserve stdout for machine-readable command output.
- Make the mechanism fail-open. Missing timing, token, or usage signals should
  degrade to a spinner and elapsed timer, not break a turn.

## Turn Timeline

Use a consistent set of timestamps for every agent turn:

| Phase | Timestamp | Meaning |
| --- | --- | --- |
| Prompt prepared | `promptPreparedAt` | Final prompt text/context is assembled and token-estimated. |
| Process ready | `processReadyAt` | Codex process or app-server session is ready to accept a turn. |
| Request submitted | `requestSubmittedAt` | The CLI has written/sent the turn request. |
| Request accepted | `requestAcceptedAt` | The transport confirms the turn started, if available. |
| First output | `firstOutputAt` | First assistant content, tool event, status event, or output token arrives. |
| Turn completed | `completedAt` | Final result and usage accounting are available. |

The hidden interval we care about is mostly:

```text
requestSubmittedAt -> firstOutputAt
```

If the transport can expose `requestAcceptedAt`, split the interval:

```text
requestSubmittedAt -> requestAcceptedAt -> firstOutputAt
```

That makes it possible to distinguish local transport overhead from remote
queue/prefill latency.

## Capture Signals

### Before submit

- Prompt character count and byte count.
- Exact token count when a tokenizer is available.
- Fallback token estimate, such as `(chars + 3) / 4`, when exact tokenization is
  unavailable.
- Prompt component hashes: generated prompt source hash, plan hash, handoff hash,
  operational context hash, decisions hash, and any other large input source.
- Session role: planning, operational execution, decision, transfer, or context
  update.
- Transport shape: persistent app-server turn vs one-shot `codex exec --json`.
- Model/config identity if the CLI can read it.
- Whether the session is warm and whether large prompt prefixes appear reused.

### During submit and wait

- Request payload bytes written to stdin/socket.
- Time spent writing the request locally.
- JSON-RPC acknowledgement or `turn/start` event when app-server exposes one.
- First event of any kind from Codex, even if it is not assistant text.
- First visible assistant output separately from first protocol event.
- Cancellation and timeout timestamps.

### After completion

- Reported prompt tokens, output tokens, cached input tokens, and effective
  tokens from the existing per-turn telemetry.
- Final status and error classification.
- Post-turn capacity snapshots where already captured.
- The actual `requestSubmittedAt -> firstOutputAt` duration used to calibrate the
  estimator.

## Estimation Approaches

### 1. Token-count-first estimate

Start every turn with the best available prompt token estimate. Exact tokenizer
counts are preferred. If that is unavailable, use the existing deterministic
fallback and label the count as estimated.

Useful display inputs:

- `promptTokensEstimated`
- `cachedTokensEstimated`
- `uncachedTokensEstimated = promptTokensEstimated - cachedTokensEstimated`
- `estimateSource = exact | historical | char_fallback`

This gives the CLI something truthful to say immediately:

```text
Processing input: about 182k prompt tokens, cache estimate 96k.
```

### 2. Historical first-output latency model

Maintain a small local history keyed by fields that materially change prefill
latency:

- CLI command and session role.
- Model/config identity.
- Transport mode.
- Prompt token bucket, for example `0-32k`, `32-96k`, `96-192k`, `192k+`.
- Cached-token ratio bucket.
- Warm vs cold session.

For each bucket, keep an exponentially weighted moving average and a rough
spread for:

```text
requestSubmittedAt -> firstOutputAt
```

A simple first version can model expected wait as:

```text
expectedWait =
  fixedOverhead
  + uncachedTokensEstimated / uncachedTokensPerSecond
  + cachedTokensEstimated / cachedTokensPerSecond
```

The model does not need to be perfect. It only needs to distinguish a normal
90-second wait on a 200k-token prompt from an unexpected stall on a 5k-token
prompt.

### 3. Confidence scoring

Print a confidence label beside estimates:

| Confidence | When to use |
| --- | --- |
| High | Exact token count and at least several matching history samples. |
| Medium | Estimated token count or sparse but relevant history. |
| Low | Char-count fallback only, unknown model, or no matching history. |

When confidence is low, avoid percentages and ETAs. Show elapsed time and the
estimated input size instead.

### 4. Monotonic bounded progress

If printing a percentage, make it explicitly estimated and cap it before the
first output signal:

```text
estimatedProgress = min(92%, elapsed / expectedWait)
```

Use a smoothing curve rather than linear progress if desired, but keep the
behavior understandable:

- Progress moves forward while the elapsed time is within the expected range.
- Progress slows as it approaches the cap.
- It never reaches completion until `firstOutputAt` is observed.
- If elapsed exceeds the expected p95, switch the message from progress to stall
  detection.

Example state changes:

```text
processing input: 54% est, 00:18 elapsed, ETA about 00:15
processing input: 88% est, 00:47 elapsed, first output not seen yet
still waiting for first output: 01:30 elapsed, longer than usual for this input
```

### 5. Cache reuse inference

Input cache behavior is often the biggest difference between a tolerable wait
and a very long one. Estimate cache reuse conservatively:

- Exact cached tokens from a completed prior turn should calibrate later turns.
- Stable prompt prefix hashes can imply likely cache hits.
- A warm persistent session should increase confidence, but should not be
  treated as proof of cache reuse.
- If prompt component hashes changed significantly, downgrade cached-token
  confidence.

The UI should say `cache estimate`, not `cached`, until final usage is reported.

## CLI Printing Strategy

### TTY mode

Use one updating status line while waiting for first output. Keep it short and
phase-oriented:

```text
[codex] processing input | 182k prompt tokens est | 00:18 elapsed | ETA about 00:27 | confidence medium
```

If a percentage is available:

```text
[codex] processing input | 54% est | 182k tokens est | 00:18 elapsed | ETA about 00:15
```

On first output, replace the status line with a final phase transition:

```text
[codex] first output after 00:33; streaming response
```

Then let normal streaming output take over.

Recommended update cadence:

- First update immediately after submission.
- Then every 500-1000 ms while the terminal line is active.
- If no new information changes, only refresh the spinner/elapsed time.
- After 30 seconds, include a more descriptive wait message.

### Non-TTY and CI mode

Do not spam progress lines. Print only phase transitions and periodic long-wait
events:

```text
[codex] submitted turn: promptTokensEst=182000 cachedTokensEst=96000 confidence=medium
[codex] waiting for first output: elapsed=30s eta=45s confidence=medium
[codex] first output: elapsed=33s
```

For CI, default to no percentage unless `--verbose` is set. CI logs age poorly
when they contain hundreds of transient progress updates.

### JSON/event mode

Machine-readable mode should emit structured events instead of terminal status:

```json
{"event":"input_processing_started","sessionId":"...","turnIndex":4,"promptTokensEstimated":182000,"cachedTokensEstimated":96000,"confidence":"medium"}
{"event":"input_processing_progress","sessionId":"...","turnIndex":4,"elapsedMs":18000,"estimatedPercent":54,"etaMs":15000,"confidence":"medium"}
{"event":"first_output","sessionId":"...","turnIndex":4,"elapsedMs":33000}
```

This can feed later visualizers without parsing human strings.

### Verbosity modes

| Mode | Behavior |
| --- | --- |
| Quiet | Spinner or no progress; always show hard stalls and final errors. |
| Normal | One status line in TTY, sparse phase lines in non-TTY. |
| Verbose | Token estimates, cache estimates, ETA, confidence, and stall thresholds. |
| JSON | Structured events only. |

## What Not To Print

Avoid messages that imply direct provider visibility:

```text
Processed 120k/182k input tokens
```

Unless the provider actually emits that count mid-turn, this is misleading.

Prefer:

```text
Processing input: 66% estimated from elapsed time and prior turns
```

Also avoid exact-looking ETAs when confidence is low:

```text
ETA 17.2s
```

Prefer:

```text
ETA about 20s
```

or:

```text
waiting for first output: 00:42 elapsed
```

## Telemetry Shape

Add an input-processing telemetry record either as a sibling JSONL stream or as
additive fields on the existing per-turn session telemetry.

Candidate fields:

| Field | Type |
| --- | --- |
| `timestamp` | ISO-8601 string |
| `repoName` | string |
| `sessionId` | string |
| `sessionType` | string |
| `turnIndex` | int |
| `transport` | string |
| `model` | string or null |
| `promptHash` | string |
| `promptBytes` | int |
| `promptChars` | int |
| `promptTokensEstimated` | int |
| `cachedTokensEstimated` | int or null |
| `tokenEstimateSource` | string |
| `estimateConfidence` | string |
| `promptPreparedAt` | ISO-8601 string |
| `requestSubmittedAt` | ISO-8601 string |
| `requestAcceptedAt` | ISO-8601 string or null |
| `firstEventAt` | ISO-8601 string or null |
| `firstOutputAt` | ISO-8601 string or null |
| `completedAt` | ISO-8601 string or null |
| `reportedPromptTokens` | int or null |
| `reportedCachedTokens` | int or null |
| `reportedOutputTokens` | int or null |
| `status` | string |
| `estimatorVersion` | string |

This can be compacted into one final row per turn. Progress events do not need to
be persisted unless a future visualizer wants fine-grained in-flight replay.

## Estimator State

Keep estimator state small and disposable:

```text
.LoopRelay/telemetry/input-processing-model.json
```

Candidate contents:

- Version number.
- Per-bucket sample count.
- EWMA first-output latency.
- Approximate p50/p90/p95 or a simple variance measure.
- Last updated timestamp.

The state is an optimization, not authority. If the file is missing, corrupt, or
from an old version, start fresh.

## Stall Detection

A good progress display should identify abnormal waits:

- If elapsed exceeds `max(expectedWait * 2, p95)`, switch to a stall-style
  message.
- If there is no history, use conservative static thresholds by token bucket.
- For app-server sessions, distinguish no acknowledgement from acknowledged but
  no first output.

Example messages:

```text
[codex] still waiting for request acknowledgement: 00:20 elapsed
[codex] first output is taking longer than usual: 02:10 elapsed, 182k tokens est
```

This gives the operator actionable context without declaring failure too early.

## Implementation Sketch

1. Add a small `TurnProgressClock`/collector around the existing agent runtime
   boundary that records phase timestamps with a monotonic clock.
2. Add `IInputTokenEstimator` for prompt token and cache estimates. Start with
   tokenizer-or-char-fallback and leave room for exact tokenizer integration.
3. Add `IInputProcessingLatencyModel` backed by local JSON. Use bucketed EWMA
   history and keep the format versioned.
4. Add a shared CLI progress renderer so plan, roadmap, and loop commands print
   the same phase language.
5. Feed final reported token usage back into the estimator after each completed
   turn.
6. Add tests for TTY rendering decisions, non-TTY sparse logging, JSON events,
   missing telemetry fail-open behavior, and estimator calibration.

## Open Questions

- Can the current Codex app-server protocol expose a reliable request-accepted
  event for every turn, or do we infer acceptance from the first event?
- Is an exact tokenizer available in the CLI runtime without adding a large or
  fragile dependency?
- Should progress state be per repository, per user, or global under
  `CODEX_HOME`?
- Which commands should default to progress rendering, and which should require
  `--verbose` because they already stream enough context?
- Can prompt component hashes predict cache reuse well enough to justify showing
  a cache estimate in normal mode?

## Recommended First Slice

Start with low-risk observability and conservative output:

1. Capture `requestSubmittedAt`, `firstOutputAt`, prompt character count, fallback
   token estimate, and final reported usage.
2. Print a TTY status line with elapsed time and estimated prompt tokens, but no
   percentage until history exists.
3. Persist one final row per turn and build a bucketed EWMA from completed turns.
4. Add estimated percent and ETA only for medium/high-confidence buckets.
5. Later, improve cache inference and exact tokenization.

This keeps the first implementation honest: the CLI becomes visibly alive during
long input waits, while the estimates earn precision only as the telemetry proves
them out.
