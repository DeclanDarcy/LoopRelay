# LoopRelay.Cli Performance Remediation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove per-turn process spawns and store scans from the telemetry path, reuse in-hand state instead of re-deriving it from the database, and clean up the composition/runner duplications the audit proved.

**Architecture:** This project is the composition root and hosts the execution owners, so most tasks here are *consumers* of APIs added by the LoopRelay.Core, LoopRelay.Orchestration.Primitives, and LoopRelay.Agents plans — check each task's dependency line. Independent tasks are marked; do those in any order.

**Tech Stack:** .NET 10, xUnit (`tests/LoopRelay.Cli.Tests` — note the loop's components are `internal` and the test project drives them via `InternalsVisibleTo`, `LoopRelay.Cli.csproj:37-40`).

**Source findings:** PERF-03 (Cli share), PERF-08 (caller side), PERF-09, PERF-11, PERF-13, PERF-16 (DecisionSession half), PERF-21, PERF-22, PERF-24, PERF-25, PERF-26, PERF-27e, PERF-28 (Cli share) in [production-code-performance-audit.md](../../production-code-performance-audit.md). Line references are against commit `0de6b5a8`.

## Global Constraints

- Telemetry stays fail-open: probe/locator failures never fail a turn.
- Decision turn-state advances must remain durable, in order, **before** the turn proceeds — recovery depends on them (PERF-16 fix changes the waiting mechanism, never the ordering/durability guarantee).
- The freshness validator's fresh observation is untouchable (see the Orchestration plan's Global Constraints).
- Causal-identity values written to evidence must be bit-identical after PERF-11 (they feed the durable causal chain).
- Validator output hashing format (`--- path ---` framing, sorted paths) is observable in evidence — must not change.

## Cross-Project Dependencies

- **After LoopRelay.Core Task 1:** every measurement here re-baselines.
- **Task 2 needs LoopRelay.Agents Task 1** (`LocateAsync`).
- **Task 1's resolver change pairs with LoopRelay.Orchestration.Primitives Task 2** (interface).
- **Task 8 needs LoopRelay.Orchestration.Primitives Task 5** (targeted effect access).
- **Task 9's deletion pairs with LoopRelay.Orchestration.Primitives Task 8f** (`CanonicalLedgerEvidenceProjection`).

---

### Task 1: Observation plumbing in the composition/runner — PERF-03 (Cli share) + PERF-24b

**Files:**
- Modify: `Services/Cli/UnifiedCliRunner.cs:361-397` (startup observes at `:365`, runs `EffectWorker.RunOnceAsync` whose `EffectWorkerResult` is ignored, then unconditionally re-observes at `:374`), `:613,616` (two identical `SelectChain` calls per `RunWorkflowAsync`)
- Modify: `Services/Cli/CompositionKernelOwners.cs:104-148` (`RepositoryObservationProductResolver` — becomes the ambient-observation adapter per the Orchestration plan Task 2 interface)
- Test: `tests/LoopRelay.Cli.Tests`

**Change contract:**
1. Startup: capture the `EffectWorkerResult` at `:373` and re-observe only when it reports executed/settled work (read `EffectWorker.cs:26`'s result type for the exact fields). A quiet workspace startup then performs one verification pass, not two.
2. Resolver adapter: implement the ambient-observation resolution added by Orchestration Task 2 (constructor/overload receiving the cycle's `RepositoryObservation`), keeping the fresh-observing behavior available for the freshness validator wiring at `LoopRelayCompositionRoot.cs:455`.
3. `SelectChain`: assign the `:613` result to a local and reuse it at `:616` (pure function, identical args — verified by the audit).

- [ ] **Step 1: Read `RunCoreAsync` and the effect-worker result type;** write a test driving startup on a no-pending-effects fixture asserting exactly one observation (counting decorator on the observations dependency, injectable via the internal composition seams).
- [ ] **Step 2: Implement all three; run `dotnet test tests/LoopRelay.Cli.Tests`.**
- [ ] **Step 3: Commit** — `perf(cli): conditional startup re-observe, ambient product resolution, single chain selection`

### Task 2: Cache and cheapen rollout-path resolution — PERF-08 (caller side). *Depends: Agents Task 1.*

**Files:**
- Modify: `Services/Telemetry/SessionTelemetryRecorder.cs:44-51` (verified: builds a full `CodexRolloutRepository().ReadExactAsync(...)` per turn and keeps only `.Location`; falls back to `_locator.Resolve`)
- Modify: `Services/Agents/GatedAgentRuntime.cs:49-53` (passes `cachedLogPath: null` every turn and discards the returned path)
- Modify: `Services/Telemetry/FileSystemCodexRolloutLocator.cs:36-58` (opens+parses the first line of every rollout file ever created; no date bound)
- Test: `tests/LoopRelay.Cli.Tests`

**Change contract:**
1. Replace `ReadExactAsync` with the Agents plan's `LocateAsync` in the telemetry path (diagnosis paths elsewhere keep `ReadExactAsync`).
2. `RecordTurnAsync` returns the resolved path (it already returns `string?`) — make `GatedAgentRuntime` retain it per session and pass it as `cachedLogPath` on subsequent turns, so resolution happens once per session.
3. Date-bound the locator: enumerate only day directories ≥ `openedAtUtc.Date` and skip files whose creation time is older than `openedAtUtc` minus a small tolerance before opening.

- [ ] **Step 1: Read the three files + `GatedAgentSession.cs:55`;** map the turn→record flow and where the session object can hold the cached path.
- [ ] **Step 2: Write tests:** second turn of a session performs no store enumeration (counting/faked locator+repository doubles); locator ignores out-of-range day directories.
- [ ] **Step 3: Implement; run suite; commit** — `perf(cli): cache rollout path per session; bounded locator scan`

### Task 3: Throttle the quota probe — PERF-09. *Independent.*

**Files:**
- Modify: `Services/Telemetry/CodexUsageProbe.cs:23-56` and its call in `Services/Telemetry/SessionTelemetryRecorder.cs:43` (verified: `ProbePostAsync` on every `RecordTurnAsync`; each probe spawns `codex app-server`, JSON-RPC initialize + `account/rateLimits/read`, ≤30 s timeout, serialized after the turn)
- Test: `tests/LoopRelay.Cli.Tests`

**Change contract:** cache the last successful `CodexUsageStatus` with a monotonic timestamp (`Stopwatch`-based, not wall clock) and a TTL (start at 5 minutes; make it a constant, not new config). Within TTL, `QueryAsync` returns the cached snapshot without spawning. First check audit §15 Q6: `grep -rn "CodexUsageProbe\|QueryAsync" src/` — the probe's doc comment claims it also "gates the loop"; if a second consumer exists, confirm staleness tolerance there before setting the TTL, and record the answer in `## Decisions`. Fail-open and cancellation semantics unchanged; a failed probe does not poison the cache (keep the previous good snapshot, subject to TTL).

- [ ] **Step 1: Trace all consumers; decide TTL; record.**
- [ ] **Step 2: Write tests with a fake clock/spawner:** two turns within TTL → one spawn; TTL expiry → re-probe; probe failure → null result, cache retained.
- [ ] **Step 3: Implement; run suite; commit** — `perf(cli): TTL-cache codex quota probe`

### Task 4: Reuse in-hand causality — PERF-11. *Independent.*

**Files:**
- Modify: `Services/Cli/CompositionPromptExecutionOwner.cs:1922-1934` (verified: `ResolveCausalityAsync` reads ALL attempts via `_persistence.ReadAttemptsAsync` and `.Single(...)`-filters in memory, plus a workspace-identity read on the write-path open)
- Callers: `:850,944,1007,1114,1315,1492-1493,1680,1734,1919`
- Test: `tests/LoopRelay.Cli.Tests`

**Change contract:** `PromptDispatchAuthorization.Causality` (a complete `CanonicalCausalContext`, `PromptGatewayContracts.cs:97-105`) is built by `TransitionRuntime.cs:52-57,141-147` from the same values persisted into the attempt row and stored in `CurrentAuthorization` at dispatch (`:176`). Make `ResolveCausalityAsync` return `CurrentAuthorization.Causality` (throwing the same way it does today when execution context is unavailable). **Gate first, then switch:** add a temporary assertion test (or debug-only check) comparing both derivations across every transition family in the existing fixtures — the audit rates the equivalence "strongly inferred", so prove it before deleting the DB read.

- [ ] **Step 1: Write the equivalence test:** for each transition-family fixture in the Cli test suite that dispatches, assert DB-derived context == `CurrentAuthorization.Causality` field-by-field.
- [ ] **Step 2: Run it against current code.** All-green proves the reuse is safe; any mismatch → stop, report the divergent family, and re-scope.
- [ ] **Step 3: Switch the implementation; keep the unavailable-context throw; delete the equivalence scaffold or demote it to one canary test.**
- [ ] **Step 4: Run suite; commit** — `perf(cli): reuse dispatch-authorized causality`

### Task 5: Products-only read in the feature-effect executor — PERF-13. *Independent.*

**Files:**
- Modify: `Services/Cli/CanonicalFeatureEffectExecutor.cs:96-99` (loads the full nine-table snapshot; sole consumption is `snapshot.Products.Where(produced.Contains(...))`)
- Test: `tests/LoopRelay.Cli.Tests`

**Change contract:** replace `LoadSnapshotAsync` with the store's existing products read (`ReadProductsAsync` per the audit — verify the exact member on `CanonicalWorkflowPersistenceStore` first), on a read-only connection, ideally filtered to the produced identities. "Re-observe committed products" semantics preserved: the read still happens at execution time, from the durable store.

- [ ] **Step 1: Read the executor + confirm the products-read API; write a parity test** (effect execution outcome identical on a seeded fixture).
- [ ] **Step 2: Implement; run effect suites; commit** — `perf(cli): products-only read in feature-effect executor`

### Task 6: Unblock decision turn-state persistence — PERF-16 (Cli half). *Coordinate contract shape with Orchestration Task 8d.*

**Files:**
- Modify: `Services/Decisions/DecisionSession.cs:1227-1228` (`CompareAndSwapDecisionTurnAsync(...).GetAwaiter().GetResult()` inside the turn-progress observer; ~5 advances per proposal turn)
- Test: `tests/LoopRelay.Cli.Tests`

**Change contract:** the observer callback currently blocks the streaming thread on SQLite I/O. Replace sync-over-async with an ordered async mechanism — either make the observer contract async end-to-end (preferred if Orchestration Task 8d does the same for the boundary journal; align the shape), or drain through a bounded ordered channel that is awaited to completion before the turn advances past each state. **The guarantee to preserve, verbatim: each turn-state advance is durable, in order, before the flow that depends on it continues.** Read the full `AgentTurnProgress.Use(durableTurn)` region and every CAS consumer before choosing.

- [ ] **Step 1: Read the observer plumbing end-to-end; write/extend a recovery test** that kills the flow between two advances and asserts recovery sees the last durable state (this pins the guarantee).
- [ ] **Step 2: Implement; run decision + recovery suites; commit** — `perf(cli): async decision turn-state persistence`

### Task 7: Read each validated artifact once — PERF-21. *Independent.*

**Files:**
- Modify: `Services/Cli/CompositionProductValidatorOwner.cs:684` (emptiness read), `:724-727` (contract re-read), `:771-790` (`HashArtifactsAsync` re-reads everything again); same pattern `:458` vs `:564`
- Test: `tests/LoopRelay.Cli.Tests`

**Change contract:** read each artifact into an in-memory map once per validation pass; run emptiness/contract checks and hashing from that map. Hash format (`--- path ---` framing over sorted paths) and all failure messages byte-identical.

- [ ] **Step 1: Write a test pinning the hash output** for a fixed artifact set (if one doesn't already exist) and one pinning a contract-failure message.
- [ ] **Step 2: Implement single-read; run suite; commit** — `perf(cli): single read per validated artifact`

### Task 8: Batch completion-phase evidence markers — PERF-22. *Depends: Orchestration Task 5.*

**Files:**
- Modify: `Services/Cli/CompositionPromptExecutionOwner.cs:1997-2107` (Record per Phase/Info/Warn; `FlushAsync` loops `WriteCandidateAsync` per message — each a full durable-effect cycle)
- Test: `tests/LoopRelay.Cli.Tests`

**Change contract:** accumulate pending markers and flush them as one plan append + one worker run (per Orchestration Task 5's targeted access), or as one combined evidence file **only if** nothing consumes the per-marker paths individually — check recovery/consumers first (`grep -rn` the marker path patterns) and record the answer. Per-marker path/sequence layout preserved unless that check proves it private.

- [ ] **Step 1: Consumer check; record in `## Decisions`.**
- [ ] **Step 2: Write test on the completion-flush fixture: same markers durable, receipt-verified, in order; count of store round-trips reduced (counting double).**
- [ ] **Step 3: Implement; run completion suites; commit** — `perf(cli): batch completion evidence markers`

### Task 9: Deletions and lifecycle micro-fixes — PERF-24a, PERF-25, PERF-26, PERF-27e, PERF-28 (Cli share). *Independent; each sub-item is its own commit.*

**9a — Duplicate operational-delta write.** `Services/Decisions/DecisionSession.cs:908` and `:956` write identical content to the same path within one transfer. Read the surrounding transfer flow to confirm the second write's inputs cannot diverge from the first (the audit verified identity at `0de6b5a8`); delete `:956`. Guarantee: the delta exists on disk before the evolution operation reads it — the `:908` write precedes it. Gate: transfer tests. Commit: `perf(cli): drop duplicate operational-delta write`.

**9b — Telemetry sink lifetime init.** `Services/Telemetry/SqliteSessionTelemetrySink.cs:28-34,65-72` re-runs directory create, gitignore probe, and (sync) `EnsureSchemaAsync` per append; `Services/Telemetry/RotatingJsonlTelemetrySink.cs:29-51` rescans the day's files (O(files) `FileInfo` probes) per append. Do directory/gitignore/schema once per sink lifetime (lazy first-append init); remember the active JSONL file + size and roll forward, re-scanning only on rollover or external deletion (fail-open to a rescan on IO errors). Preserve rotation thresholds and crash-safe append. Gate: telemetry tests + a two-append test asserting one init. Commit: `perf(cli): one-time telemetry sink initialization`.

**9c — Decision machinery under its first-init guard.** `Services/Cli/CompositionPromptExecutionOwner.cs:1059-1107` constructs `recoveryMechanisms`, `RecoveryRuntime`, `LoopArtifacts`, router, dispatcher on every decision-session invocation; only the first initialization consumes them (`executeDecisionSession ??=`). Move construction inside `if (executeDecisionSession is null)`. Keep `executeRecoveryStore ??=` reuse. Gate: decision-session suites. Commit: `perf(cli): construct decision-session machinery once`.

**9d — Load settings once.** `Services/Cli/LoopRelayCompositionRoot.cs:214,223,341,349` call `CliSettingsLoader.Load()` four times at startup (file read + full deserialize each). Load once into a local at the top of `CreateProduction` and pass it down. Behavior identical (same file, same parse). Gate: composition tests + a run smoke. Commit: `perf(cli): single settings load at composition`.

**9e — Delete `LedgerEvidenceRetrieval`.** `Services/Cli/LedgerEvidenceRetrieval.cs` has zero production callers (lead-verified in the audit); its only role is invoking `CanonicalLedgerEvidenceProjection` (deleted by Orchestration Task 8f — same PR). Sweep `tests/` for consumers first; if tests exercise it, delete them with it or re-point them at the store. Commit: `chore(cli): remove unreachable ledger evidence retrieval`.

- [ ] 9a  - [ ] 9b  - [ ] 9c  - [ ] 9d  - [ ] 9e

---

## Verification Gate (whole plan)

- `dotnet build` + `dotnet test tests/LoopRelay.Cli.Tests` green; decision/recovery/completion certification fixtures green.
- Audit M5 (per-turn spawns and CODEX_HOME bytes) at target after Tasks 2–3: zero rollout-store sweeps after a session's first turn; probe spawns ≤ 1 per TTL window.
- One full `run` smoke on a real workspace: identical workflow outcomes, evidence chain intact, `status` output unchanged.

## Decisions

*(append: Task 3 second-consumer answer + TTL; Task 8 marker-consumer answer; Task 4 equivalence result)*
