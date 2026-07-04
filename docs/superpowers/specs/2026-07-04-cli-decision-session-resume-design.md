# CLI Loop: Resume the Decision Session Across Runs

**Date:** 2026-07-04
**Status:** Approved
**Scope:** CommandCenter.CLI (loop), CommandCenter.Agents (session/protocol seam), CommandCenter.Plan.CLI (epic rollover clearing), CommandCenter.Core (shared store)

## Motivation

The CLI loop owns ONE warm read-only codex decision process, reused across loop iterations
(`DecisionSession.session`, lazily opened, primed inline with `operational_context.md` on its first
proposal turn). That process accumulates epic-scoped conversation state — but it dies with the CLI
process. Every restart of the loop re-primes a fresh process from `operational_context.md` + the
latest handoff, discarding everything the previous decision process had accumulated since its last
transfer.

This feature resumes the previous decision session when the loop enters the decision step for the
first time in a run, IF a previous session exists. The data needed to resume is persisted under
`{REPO_DIR}/.commandcenter/`. Upon epic completion the persisted session is cleared.

## Verified codex capability (feasibility)

Verified against the installed binary (`codex-cli 0.142.5`, via `codex app-server
generate-json-schema --experimental`):

- The app-server v2 protocol has **`thread/resume`** with required `threadId` (docs: "Prefer using
  thread_id whenever possible"); optional `cwd`, `sandbox`, `approvalPolicy` overrides; and
  `excludeTurns: true` to omit replayed history from the response.
- `ThreadResumeResponse` has the **same shape as `ThreadStartResponse`** — a required `thread`
  object whose `id` the existing `CodexAppServerSession.ExtractThreadId` already parses.
- Resume loads the thread from disk (codex's own rollout under `~/.codex/sessions`); a missing /
  unknown / deleted rollout yields a JSON-RPC error — a clean fallback trigger.
- The thread id is ALREADY captured today (`thread/start` → `result.thread.id` →
  `CodexAppServerSession.threadId`) but is a private field: never exposed, never persisted. This
  feature surfaces and persists it.

## Goals

1. First decision-step entry of a CLI run resumes the persisted decision session when one exists.
2. Resume state lives at `{REPO_DIR}/.commandcenter/decision-session.json`.
3. Epic completion clears the persisted session — at BOTH seams (loop gate + Plan.CLI rollover).
4. Fail-soft: any resume failure degrades loudly to today's fresh-process behavior.

## Non-goals

- Execution-session resume (per-slice sessions stay ephemeral).
- Backend (legacy `RepositoryOrchestrator`) parity — recorded as a technical-debt entry, per the
  backend-is-legacy policy.
- Concurrent loops against one repo (unsupported today; unchanged).
- Restoring telemetry's per-session rollout-path cache. Note: a resumed thread may continue its
  ORIGINAL rollout file, whose start timestamp predates the new session's `openedAtUtc`, so the
  telemetry locator may record `codexLogPath: null` for resumed sessions. Telemetry is fail-open;
  accepted degradation.

## Design

### 1. Persisted state — `{REPO_DIR}/.commandcenter/decision-session.json`

One compact camelCase JSON object (same serialization conventions as the telemetry ledger):

```json
{
  "schemaVersion": 1,
  "threadId": "<codex thread id>",
  "occupancyTokens": 123456,
  "reuseCost": 42.5,
  "reuseCycles": 3,
  "lastCycleCost": 12.1,
  "prevCycleCost": 11.9,
  "transferCost": 250000.0,
  "transferCount": 1,
  "previousOperationalContextSize": 18000,
  "operationalContextGrowthStreak": 0,
  "savedAtUtc": "2026-07-04T12:34:56Z"
}
```

- `threadId` — the codex app-server thread id; the resume key.
- The five per-process router-accounting fields + the two cross-recycle transfer-calibration fields
  + the two operational-context-health fields mirror `DecisionSession`'s in-memory state exactly, so
  cost-aware transfer routing and the size-health warning behave identically across restarts.
- **Invariant: the file is only ever written after a successful decision turn.** Its existence
  therefore implies the thread was primed — no `seeded` field is needed; successful resume always
  restores `seeded = true`.

**Store type:** `IDecisionSessionResumeStore` + `FileDecisionSessionResumeStore` in
**CommandCenter.Core** (shared so both CommandCenter.CLI and CommandCenter.Plan.CLI reference it):
`ReadAsync()` (null on missing/corrupt; corrupt file is deleted), `WriteAsync(state)`,
`ClearAsync()` (idempotent delete). All operations are fail-open in the telemetry sense — an IO
error never breaks a turn or the loop; failures surface as console warnings only.

**Self-ignoring directory (load-bearing):** when creating `.commandcenter/`, the store writes
`.commandcenter/.gitignore` containing `*` (if absent). `WorkingTreeChangeDetector` and `CommitGate`
exclude only `.agents`, so an un-ignored state file would make a clean tree look dirty (corrupting
the no-changes/stall gates) and would be committed into target repos. The self-ignore removes the
dependency on the manual "add `.commandcenter/` to the target repo's .gitignore" convention (and
incidentally closes the same pre-existing footgun for telemetry once the store first writes).

### 2. Protocol / session layer (CommandCenter.Agents)

- `AgentSessionSpec` gains optional `ResumeThreadId` (nullable string, default null).
- `AgentSpecs.Decision(repository)` gains an optional `resumeThreadId` parameter.
- `CodexAppServerProtocol` gains a `ThreadResume(id, threadId, cwd, sandbox, approvalPolicy)` frame
  builder sending `excludeTurns: true` (history replay is not needed and can be large).
- `CodexAppServerSession.EnsureHandshakeAsync`: when `spec.ResumeThreadId` is set, send
  `thread/resume` instead of `thread/start` (same `initialize → initialized → …` envelope; same
  `ExtractThreadId` on the response). A resume error does NOT silently fall back — see below.
- **Eager handshake on resume:** `AgentRuntime.OpenSessionAsync` triggers the handshake immediately
  when `spec.ResumeThreadId` is set (normal opens stay lazy). Rationale: the caller decides priming
  (whether to prepend `operational_context.md`) at prompt-build time, BEFORE the first turn — it
  must know the resume outcome by then. On resume failure the runtime disposes the process and
  throws a typed `AgentSessionResumeException`; the priming policy stays where it lives today, in
  `DecisionSession`.
- `IAgentSession` exposes `string? ThreadId` (null until the handshake completes; null forever for
  one-shot/legacy sessions). `GatedAgentSession` passes it through. Test fakes updated.

### 3. DecisionSession (CommandCenter.CLI)

- New constructor dependency: the resume store.
- **First open only** (a `resumeAttempted` flag on the object — one attempt per CLI process,
  matching "first time entering the decision step when running"): read the store. If state exists,
  open with `AgentSpecs.Decision(repository, resumeThreadId: state.ThreadId)`.
  - **Success:** restore the nine accounting/health fields, set `seeded = true`, log
    `Resumed decision session (thread <id>).` via `console.Info`.
  - **`AgentSessionResumeException`:** log the reason via `console.Warn`, `ClearAsync()` the store,
    open fresh — from here behavior is byte-identical to today (inline priming).
- **Restore ordering:** restored state is applied only at successful resume-open. The router
  evaluation at the top of `RunAsync` runs BEFORE the open, so the first route of a run sees
  pre-restore (zeroed) inputs and routes `Continue` — the existing `Transfer && !seeded → Continue`
  downgrade independently guards the same hazard (a `Transfer` route would call
  `session!.RunTurnAsync` against a null session). From the second iteration on, the router sees the
  restored accounting.
- **Persist point:** in `RunAsync`, immediately after `seeded = true; RecordProposalCost(...)` —
  write the store with `session.ThreadId` + current accounting. One small file write per decision
  step.
- **`CloseAsync` gains a disposition:** transfer-recycle and failed-turn closes **delete** the
  persisted state (matching the in-memory "this thread is dead" reset; the post-transfer fresh
  thread re-persists after its first successful turn, so the NEXT run resumes the post-transfer
  session). `DisposeAsync` **keeps** the file — it is precisely the resume payload for the next run,
  and no turn can mutate the thread between the last persist and disposal.
- Note on the crash window: a transfer-close deletes the file and the rewrite happens only after the
  fresh process's first successful turn, so a crash inside that window loses the calibrated
  `transferCost` (next run re-seeds at the 250k default). Accepted — the file models "resumable
  thread", not a standalone metrics ledger.

### 4. Clearing on epic completion — both seams, both idempotent

1. **LoopRunner:** when `MilestoneGate.IsEpicCompleteAsync()` fires at the top of an iteration,
   `ClearAsync()` before returning `LoopOutcome.EpicCompleted`. This fires on every re-run against a
   completed epic; deletion is a no-op then. LoopRunner gains the store as a dependency (testable
   through the existing harness).
2. **Plan.CLI:** in `PlanPipeline.RunAsync`, after `EpicRolloverStep.TryArchiveAsync` returns true
   (the post-gate has proven the archive), `ClearAsync()` — covering the epic-rolled-over-without-
   the-loop-observing gap (the rollover criterion is artifact presence, not checkbox state, so the
   two seams genuinely do not subsume each other).

### 5. Kill switch

`COMMANDCENTER_DECISION_RESUME=0` (or `false`) disables the resume attempt (state is still written
and cleared normally; only the resume-on-open is skipped). Mirrors the `COMMANDCENTER_SESSION_LOG`
convention. Default: enabled. Insurance against `thread/resume` behavioral surprises — the protocol
subcommand is still marked experimental upstream.

## Error handling summary

| Failure | Behavior |
|---|---|
| State file missing | Fresh open (today's behavior), no log |
| State file corrupt | Warn, delete, fresh open |
| `thread/resume` rejected (rollout deleted, unknown id, protocol error) | Warn with codex's error, clear store, fresh open with inline priming |
| Store IO error on write/clear | Warn, continue — never breaks a turn or the loop |
| Resume succeeds but rollout stale-ish (context evolved externally) | Not detected; next transfer naturally rebuilds context. Accepted |

## Testing

Follows existing repo patterns (hand-rolled fakes in `TestDoubles.cs`, real temp dirs for file IO):

1. **Store tests** (real temp dir, like `RotatingJsonlTelemetrySinkTests`): round-trip, corrupt-file
   → null + deleted, idempotent clear, `.gitignore` self-ignore written once with `*`.
2. **DecisionSession tests** (`FakeAgentRuntime`/`ScriptedTurn`): resume-success skips the
   operational-context prepend (prompt assertion) and restores router inputs; resume-failure warns,
   clears, falls back to primed fresh open; persist-after-turn writes threadId + accounting;
   transfer-close deletes then re-persists after the next turn; failed-turn close deletes; dispose
   keeps; second open in one run never attempts resume; kill switch skips the attempt.
3. **Session-layer tests:** `ThreadResume` frame shape (threadId, cwd, sandbox, approvalPolicy,
   excludeTurns); eager handshake on `ResumeThreadId`; JSON-RPC error → `AgentSessionResumeException`
   + process disposed; `ThreadId` exposed post-handshake and passed through `GatedAgentSession`.
4. **LoopRunner harness test:** epic-complete gate clears the store before returning
   `LoopOutcome.EpicCompleted`.
5. **Plan.CLI test:** rollover-true path clears the store; rollover-false path does not.
6. **Manual real-codex smoke:** run loop → let a decision turn complete → kill CLI → rerun → observe
   `Resumed decision session (thread …)` and a proposal turn WITHOUT context re-priming; complete an
   epic → verify the file is gone.

## Documentation / debt

- `technical-debt.md`: backend decision session (`RepositoryOrchestrator`) does not resume —
  won't-fix-in-place per the legacy-backend policy.
- `.gitignore` guidance in the telemetry design doc is superseded by the self-ignoring directory.

## Design decisions log

- **Resume by `threadId`, not rollout path** — path is `[UNSTABLE]` in the protocol and the
  locator's cwd+timestamp heuristic can misattribute between decision/execution sessions sharing the
  repo cwd; codex docs prefer thread_id.
- **Typed exception over silent in-session fallback** — priming is decided at prompt-build time;
  a silent `thread/start` fallback inside the session would receive a prompt composed for a warm
  thread.
- **No `seeded` field in the schema** — write-after-successful-turn invariant makes it always true.
- **Restore-at-open ordering** — prevents a restored `reuseCycles > 0` from routing `Transfer`
  against a not-yet-opened session.
- **Clear at both epic seams** — loop gate is the user-facing requirement; rollover clearing covers
  the loop-never-reran gap (user-approved).
