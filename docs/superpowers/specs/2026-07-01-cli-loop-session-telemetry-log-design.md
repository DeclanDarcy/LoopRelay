# CLI Loop — Per-Turn Session Telemetry Log

**Date:** 2026-07-01
**Status:** Design — awaiting review
**Author:** declandarcy (with Claude)

## Motivation

The CLI loop drives codex through many turns, but nothing durably records what each
turn *cost* against the codex usage windows. We want an ongoing, machine-readable
ledger so we can estimate a rough **effective-tokens → capacity-percent ratio** over
time (how much of the 5h / weekly window a unit of token spend actually burns), and
later drive a visualizer off it.

Today the pieces exist but are transient: token usage is parsed per turn and discarded
after cost accounting; capacity is probed before every turn and thrown away after
gating; codex's own rollout log is never located. This feature captures all of it as
one JSONL row per codex turn.

## Goals

- One JSONL record per **codex turn** (both persistent turns and one-shots).
- Record the 11 requested fields (+ 3 additive: timestamp, turnIndex, split raw tokens).
- Reuse existing signals wherever they already exist; add the minimum new plumbing.
- **Never break the loop.** All telemetry work is best-effort / fail-open, matching the
  usage gate's existing philosophy (`UsageGate.cs:49`).

## Non-goals

- No pruning / retention policy — **keep all files**. A separate visualizer will manage
  pruning later.
- No backend/REST/UI surface. CLI-only, consistent with where capacity already lives.
- No change to token *math* or the router — we only *read* what already exists.
- No attempt to make codex emit its session id; we locate its rollout file heuristically.

## Record grain & schema

**Grain: one line per codex turn.** A codex *process* (one rollout file) serves many
turns, so many rows share one `codexLogPath`; that is expected and correct.

One compact JSON object per line (`sessions.<date>.<NNNN>.jsonl`):

| Field | Type | Meaning / source |
|-------|------|------------------|
| `timestamp` | string (ISO-8601 UTC) | When the row was emitted (turn completion). *Additive — needed for the time-series ratio.* |
| `repoName` | string | Human repo name = `Path.GetFileName(repository.Path)` (`Program.cs:26`). |
| `codexLogPath` | string \| null | Absolute path to the codex rollout JSONL for this session's process (see resolution). `null` if unresolved. |
| `sessionId` | string (GUID) | CommandCenter's `SessionIdentity` (`SessionIdentity.cs:5`). Internal correlation key, **not** codex's id. |
| `sessionType` | string | `SessionRole` name: `Decision` / `OperationalExecution` / `Transfer` / `ContextUpdate` (`SessionRole.cs`). |
| `turnIndex` | int | `AgentTurnResult.TurnIndex` (1-based within the session). *Additive — the natural per-turn key.* |
| `promptTokens` | int | `AgentTokenUsage.PromptTokens`. *Raw tokens = `promptTokens + outputTokens`; split so the ratio is fully reconstructable.* |
| `outputTokens` | int | `AgentTokenUsage.OutputTokens`. |
| `cachedTokens` | int | `AgentTokenUsage.CachedInputTokens` (cached subset of prompt). 0 may mean "no cache" *or* "not reported" by codex — stored raw, undistinguished. |
| `effectiveTokens` | double | `IDecisionCostModel.Measure(usage)` = `(prompt − cached) + cached×0.10 + output` (`EffectiveTokenCostModel.cs:28`). Same "effective" the router uses. |
| `preFiveHourPercent` | int \| null | 5h window remaining % at turn start. |
| `postFiveHourPercent` | int \| null | 5h window remaining % just after the turn. |
| `preWeeklyPercent` | int \| null | Weekly window remaining % at turn start. |
| `postWeeklyPercent` | int \| null | Weekly window remaining % just after the turn. |

Capacity is a **remaining-percent (0–100)**, never a token budget — codex exposes only
`usedPercent` + `resetsAt` (`CodexUsage.cs:11`). A capacity field is `null` when the
probe failed (fail-open); note a reported `100` for the weekly window can be a genuine
value *or* the parser's stand-in for an unreported window (`CodexRateLimitsParser` `Window`),
so downstream analysis should treat lone `100`s with mild suspicion.

## The seam & control flow

All wiring lives at the one chokepoint every gated turn already passes through:
`GatedAgentSession` / `GatedAgentRuntime` (`GatedAgentRuntime.cs`). Both the persistent
turn path (`RunTurnAsync:59`) and the one-shot path (`RunOneShotAsync:29`) get the same
treatment.

Per-turn sequence (inside the wrapper):

1. `pre = await usageGate.WaitForCapacityAsync(ct)` — the gate already probes here; we
   now **return** that snapshot (see below) instead of discarding it.
2. `result = await inner.RunTurnAsync(prompt, onChunk, ct)` — unchanged.
3. `post = await probe.QueryAsync(ct)` — one added probe for the post-run capacity.
4. `codexLogPath = cachedPath ??= locator.Resolve(spec.WorkingDirectory, sessionOpenedAt)`
   — resolved once per session, cached, reused by later turns.
5. Build `SessionTelemetryRecord`, `sink.Append(record)`.
6. **Return `result`.** Steps 3–5 are wrapped in try/catch; any failure warns and is
   swallowed — the turn result is always returned.

## Capacity capture

`IUsageGate.WaitForCapacityAsync` changes signature from `Task` to
`Task<CodexUsageStatus?>` and returns the capacity **the turn will actually start with**:

- No wait needed → return the single snapshot it already fetched.
- It had to wait for a window reset → after the delay, **re-probe once** and return the
  fresh (post-reset) snapshot, so `pre*Percent` reflects reality rather than the ~1%
  pre-reset reading.
- Probe unreadable → return `null` (unchanged fail-open behavior; wrapper records null
  capacities).

Post-run capacity is a second `ICodexUsageProbe.QueryAsync` after the turn. **Cost:** one
extra short-lived `codex app-server` probe per turn. The loop is serial and turns run for
minutes, so this is negligible.

## Codex rollout filepath resolution

Codex writes one rollout per process at `~/.codex/sessions/YYYY/MM/DD/rollout-<ts>-<uuid>.jsonl`,
whose first line is `session_meta` carrying `session_id`, `timestamp`, and `cwd` (= the
repo path). CommandCenter never receives codex's id, so we locate the file heuristically:

- **`FileSystemCodexRolloutLocator.Resolve(workingDirectory, openedAtUtc)`**: scan
  `~/.codex/sessions` for `rollout-*.jsonl` files whose first-line `session_meta.cwd`
  equals `workingDirectory` and whose `session_meta.timestamp` (or file creation time) is
  `>= openedAtUtc`; return the newest match, else `null`.
- Resolution is **lazy, once per session**: the rollout file is created by codex at
  `thread/start` (first turn), so we resolve after the first turn completes and cache the
  path on the wrapper for every subsequent turn. One-shots resolve their own file after
  the process completes.
- Root is `~/.codex` (expand `%USERPROFILE%`/`HOME`), overridable via `CODEX_HOME` if set.

**Caveat:** the loop is serial (`LoopRunner` runs decision then execution) and each codex
process creates exactly one new rollout, so cwd + `openedAtUtc` uniquely identify the file
in practice. If two processes ever share a cwd and start in the same instant, newest-wins
could mis-attribute; acceptable given the serial loop, and `null` is always a safe fallback.

## Log storage & rotation

- **Location:** repo-local, git-ignored: `<repo>/.commandcenter/telemetry/sessions.<date>.<NNNN>.jsonl`.
  Not under `.agents/` — that submodule is committed & pushed every iteration
  (`AgentsSubmodulePublisher`), which would mean noisy telemetry commits and steady bloat.
  Add `.commandcenter/` to the repo's `.gitignore` (create if absent).
  > **Superseded (2026-07-04):** `.commandcenter/` is now self-ignoring — `FileDecisionSessionResumeStore`
  > writes a `.commandcenter/.gitignore` containing `*` when it first creates the directory, so the manual
  > root-.gitignore step is no longer load-bearing once the loop has run a decision step in the repo.
- **Rotation: per-day / size hybrid.**
  - A new file begins each **calendar day** (UTC): `sessions.2026-07-01.0000.jsonl`.
  - Within a day, when the active file crosses the size cap (**5 MiB = 5,242,880 bytes**,
    a single named constant), roll to the next sequence:
    `sessions.2026-07-01.0001.jsonl`, `…0002…`, etc.
  - **Keep all files** — no deletion, ever.
  - Active-file selection before each append: for today's date, take the highest existing
    `NNNN`; if that file is `>= 5 MB`, use `NNNN+1`; else append to it. New day → `0000`.
- **Writer:** append one compact JSON line via `System.Text.Json`; guard appends with a
  lock (turns are serial, but the sink is a single shared instance — cheap correctness).
  Directory created on first write.

## Components & interfaces

Each unit has one purpose, a defined interface, and is independently testable:

- **`SessionTelemetryRecord`** (record/DTO) — the 14 fields above; owns its JSON shape.
- **`ISessionTelemetrySink`** — `void Append(SessionTelemetryRecord record)`.
  - `RotatingJsonlTelemetrySink` — per-day/size hybrid file selection + serialization +
    locked append. Depends on: base directory, size cap, a clock.
  - `NullSessionTelemetrySink` — no-op; used when telemetry is disabled or construction fails.
- **`ICodexRolloutLocator`** — `string? Resolve(string workingDirectory, DateTimeOffset openedAtUtc)`.
  - `FileSystemCodexRolloutLocator` — the `~/.codex/sessions` scan above.
- **`IUsageGate.WaitForCapacityAsync`** — return type widened to `Task<CodexUsageStatus?>`.
- **`GatedAgentRuntime` / `GatedAgentSession`** — inject `ICodexUsageProbe`,
  `ICodexRolloutLocator`, `ISessionTelemetrySink`, `IDecisionCostModel`, `repoName`, and a
  clock; orchestrate the per-turn sequence; cache the resolved path per session; record
  the session-open time.
- **`Program.cs`** — construct the concrete sink (path = `<repo>/.commandcenter/telemetry`),
  locator, and clock; derive `repoName` from `repository.Path`; pass through. A CLI
  flag / env (`--no-session-log` or `COMMANDCENTER_SESSION_LOG=0`) swaps in the null sink;
  **default on**.

A minimal clock abstraction (`TimeProvider`, or the codebase's existing convention as used
by `CodexRateLimitsParser.Parse(json, now)`) supplies `timestamp` and the day boundary so
rotation and stamping are deterministic under test.

## Error handling (fail-open)

Telemetry must never wedge or crash a turn:

- Post-probe, path resolution, record build, and sink append are wrapped in try/catch in
  the wrapper; on failure, `console.Warn(...)` and continue.
- A `null` pre/post status → `null` capacity fields (not an error).
- Sink I/O failure (locked file, disk) → warn, drop that row, keep looping.
- Locator failure or no match → `codexLogPath = null`.

## Testing plan (xUnit, `CommandCenter.CLI.Tests`)

- **Rotation** (`RotatingJsonlTelemetrySink`): appends to today's `0000`; rolls to `0001`
  when size cap crossed; starts a fresh `0000` on a new day (driven by a fake clock);
  never deletes; concurrent appends stay well-formed (one JSON object per line).
- **Serialization**: `SessionTelemetryRecord` → exactly the 14 keys, correct types, nulls
  emitted for absent capacity; raw = prompt+output reconstructs.
- **Locator** (`FileSystemCodexRolloutLocator`): matches by cwd + newest-after-open against
  a temp `~/.codex/sessions` tree; ignores non-matching cwd; returns `null` on no match /
  missing root / malformed `session_meta`.
- **Gate return**: `WaitForCapacityAsync` returns the fetched status; re-probes and returns
  the fresh status after a wait; returns `null` when the probe is unreadable (extends
  `UsageGateTests`).
- **Wrapper emission**: with a fake probe (distinct pre/post), fake locator, fake sink, and
  a stub inner session, one turn emits one record with correct pre/post/token/effective
  values and the cached path reused across two turns; one-shot emits one record.
- **Fail-open**: a throwing sink and a null-returning probe both leave the turn result
  intact and the loop uninterrupted.

## Settled decisions

- Grain = **per codex turn** (user-confirmed).
- Location = **repo-local git-ignored `.commandcenter/telemetry/`** (user-confirmed).
- Rotation = **per-day + ~5 MB size hybrid, keep all** (user-confirmed).
- Capacity = **remaining percent**; user is aware and wants the token→percent ratio.
- Effective tokens = the router's canonical `EffectiveTokenCostModel.Measure`.

## Out of scope

- Retention/pruning (future visualizer).
- Surfacing codex's own session id or parsing rollout contents beyond `session_meta`.
- Any backend/UI exposure.
