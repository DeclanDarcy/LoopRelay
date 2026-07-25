# Production-Code Performance, Redundancy, and Unnecessary-Work Audit

Audited surface frozen at commit `0de6b5a8` (branch `improve-performance`), 2026-07-25.
Method: production boundary resolved first; seven parallel subsystem audits traced execution paths from `src/LoopRelay.Cli/Program.cs`; every Critical/High finding and every deletion-class claim was independently re-verified by the lead auditor against current sources ("lead-verified" below). No repository content other than this report was modified.

---

## 1. Executive Summary

LoopRelay's production surface is a single CLI (`LoopRelay.CLI.exe`) that runs a bounded orchestration loop (default cap 32 continuation steps) driving codex child processes, with a single SQLite workspace database as canonical authority. The audit found the system architecturally disciplined about correctness — durability boundaries, fail-closed schema handling, and causal evidence chains are consistently enforced — but that discipline has been implemented at the **wrong lifecycles**, producing large, structurally provable overheads on every hot path. The causes are primarily **architectural, not local**: three ownership/lifecycle defects account for the majority of findings.

**Dominant cost patterns (each reported independently by 2–4 subsystem auditors):**

1. **Schema convergence at connection-open lifecycle (PERF-01, Critical).** Every store operation — 101+ `OpenAsync` call sites, conservatively 50–100 opens per attempt — re-runs full schema inspection (~180 SQL probes), workspace-identity validation, a legacy-resume probe, and an IMMEDIATE **write transaction** executing one-time repair SQL, on unpooled connections in rollback-journal mode (PERF-10). Validity is per-process; execution is per-operation.
2. **Observation as unscoped global reconstruction (PERF-03/04/05/23).** "Observe the repository" loads nine full ledger tables (including every historical raw codex output), SHA-256s the whole database twice, hashes every artifact file, runs `PRAGMA foreign_key_check`, and spawns `git status` — and this runs 4–5× per continuation step, including once per attempt to answer "which of ≤5 input products are usable" and once per attempt solely to populate a field **no production code reads** (lead-verified).
3. **Snapshot-shaped store APIs forcing full-history reads for keyed questions (PERF-02/11/12/13/14).** Ledger tables grow monotonically for the life of a workspace (which spans many CLI invocations), so per-operation cost grows with workspace age — an aggregate O(history²) trajectory with no retention or compaction mechanism (PERF-29).

**Highest-value remediations:** memoize schema verification per process and confine repair SQL to the migration branch (removes the largest per-operation multiplier and most gratuitous write transactions); delete the consumerless post-attempt observation and hand the cycle's observation down to the product resolver; add keyed reads to the persistence stores. These are deletions, reuse, and lifecycle moves — not caches or new machinery.

One finding is dual-natured: the permission evaluator rebuilds itself per command **and silently evaluates the built-in default policy instead of the operator-configured one** (PERF-07, lead-verified) — a one-line fix that is also a policy-fidelity correction.

**Main evidence limitation:** all magnitudes are structural (statement counts, call multiplicities, growth rates proven from code); no wall-clock, allocation, or production-database measurements exist. Nothing in this report fabricates timings; the Measurement Plan (§12) defines what to measure before sizing the payoff of the two tuning-class items (WAL/pooling, probe throttling).

---

## 2. Audit Scope and Production Boundary

**Deployable unit.** `publish-cli.bat` publishes `src/LoopRelay.Cli` (assembly `LoopRelay.CLI.exe`, net10.0, framework-dependent) to `C:\tools\command-center`, copying `config/settings.default.json` to `settings.json` on first publish. This is the only shipped executable.

**Included production assemblies (9)** — resolved from `LoopRelay.Cli.csproj:20-27` plus transitive project references:
`LoopRelay.Cli`, `LoopRelay.Agents`, `LoopRelay.Application`, `LoopRelay.Cli.Surface`, `LoopRelay.Completion`, `LoopRelay.Core`, `LoopRelay.Infrastructure`, `LoopRelay.Orchestration.Primitives` (assembly `LoopRelay.Orchestration`), `LoopRelay.Projections`, plus `LoopRelay.Permissions` reachable via both `LoopRelay.Agents.csproj:15` and `LoopRelay.Orchestration.csproj:11-13`.

**Build-time only.** `LoopRelay.Prompts.Generator` is a netstandard2.0 Roslyn source generator referenced analyzer-only (`LoopRelay.Core.csproj:17-20`, `OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"`); it never ships and has no runtime cost.

**Excluded as audit subjects** (used only as evidence of behavior/scale):

* `tests/**` (11 test projects), fixtures, `.agents/` evidence trees.
* `src/LoopRelay.Certification` — its own `OutputType Exe`, but dev tooling: `docs/certification.md:7-9` explicitly reserves it as "an explicit operator campaign rather than a component-test target"; `docs/orchestration-baseline.md:12-13` calls it "a separate evidence-producing executable, not an alternate application entrypoint." Not referenced by the CLI, not published.
* `CommandCenter.*` — zero source files exist anywhere; every occurrence is a stale gitignored `bin/obj` artifact from before the repo rename (e.g. `src/LoopRelay.Cli/obj/CommandCenter.CLI.csproj.nuget.g.props`). `CommandCenter.UI` has no source and no serving path.
* `src/LoopRelay.Plan.Cli`, `src/LoopRelay.Roadmap.Cli` — empty shells (no `.csproj`, only untracked bin/obj leftovers); `docs/orchestration-baseline.md:3-5` confirms neither is "a supported or reachable runtime authority."

**Entry point and runtime shape.** `Program.cs:23-69`: parse args → `LoopRelayCompositionRoot.CreateProduction` (settings load, policy resolution, DI construction) → one `UnifiedCliRunner.RunAsync(request)`; Ctrl+C cancels via a single CTS and disposal tears down codex children. The long-running loop is `OrchestrationKernel.RunAsync` (`Chaining/OrchestrationKernel.cs:100-122`), bounded by `policy.execution.maxUnboundedContinuationSteps` (default 32, `config/settings.default.json`). One provider attempt per cycle; the workspace SQLite database (`.LoopRelay/persistence/looprelay.sqlite3`, `LoopRelayWorkspaceDatabase.cs:71`) persists across CLI invocations, so ledger state accumulates for the life of an epic.

**Runtime state:** SQLite canonical authority; `.LoopRelay/telemetry/` JSONL export; `.LoopRelay/evidence/<phase>/*.md`; `.LoopRelay/exports/workspace.canonical.json` (`UnifiedCliRunner.cs:467`); git-published `.agents/` artifact tree. Repo-root `artifacts/` and `transitions/` have no production path construction (legacy working dirs).

**Configuration knobs affecting cost** (`config/settings.default.json`): brain model `gpt-5.6-sol` at effort `xhigh`; `maxUnboundedContinuationSteps: 32`; `maxNoChangesCommits: 2`; `policy.decisions.sessionResume: true`; `policy.recovery.strategy: "resume-only"`; **`policy.runtime.sessionTelemetry: true` (default-on; drives PERF-08/09)**; `usageLimitWaitRetry: true`; `inputWaitReporting: true`. No `Directory.Build.props` exists (the comment at `LoopRelay.Cli.csproj:8-9` is stale); `Directory.Packages.props` pins SQLite/DI 10.0.0; `global.json` pins SDK 10.0.301.

**Lifecycle classes used below:** per-CLI-invocation startup · per-kernel-cycle (≤32/invocation) · per-attempt (≈1/cycle) · per-turn (provider turns within an attempt) · per-effect/entity/row · failure/recovery-only · shutdown.

**Ambiguous boundaries:** none material. Reachability of all reported paths was traced from `Program.cs`; reflection/DI-based loading is confined to the composition root, which was read directly.

---

## 3. Production Execution-Path Map

| Path | Entry point | Major work | I/O boundaries | Repetition risks | Lifecycle |
| ---- | ----------- | ---------- | -------------- | ---------------- | --------- |
| Startup | `Program.cs:23-57` → `LoopRelayCompositionRoot.CreateProduction` | Settings load (×4, see PERF-27), policy resolve, catalog validation, DI wiring | settings.json read; SQLite first touch | Settings re-parsed 4× | Startup |
| Run bootstrap | `UnifiedCliRunner.RunCoreAsync:361-397` | Observe → effect-worker pass → observe again → prerequisite inspection → dispatch workflow | Full observation ×2 (each = SQLite full-projection + hash sweep + `git status`) | Second observation unconditional even when zero effects ran (PERF-03) | Startup |
| Kernel cycle | `OrchestrationKernel.RunAsync:100-122` | Observe, resolve workflow, run controller, append decision fact (JSON+SHA-256 of full observation), boundary observers | SQLite reads/writes; observation | Observation per cycle + per controller + per resolver (PERF-03); full-history snapshot serialization | Per-cycle ×≤32 |
| Attempt | `TransitionRuntime.RunAsync:34-149` | Input gate, prompt context, attempt row, read receipt (+`git rev-parse`), render, dispatch, ~5 state persists, commit transaction | SQLite (dozens of opens, each paying PERF-01), git spawns | `PersistStateAsync` ×~5 each loading full snapshot (PERF-02) | Per-attempt |
| Provider turn | `GatedAgentRuntime` → `CodexAppServerSession` | codex child spawn, per-line JSON parse, approval round-trips, spine rows (3 SQLite writes/turn), telemetry record | Process stdio; SQLite; `codex app-server` spawn per turn (PERF-09); CODEX_HOME scan (PERF-08) | Quota probe + rollout scan per turn | Per-turn |
| Approval | `CodexAppServerSession.Dispatch:507-603` → Permissions | Parse, canonicalize, fingerprint, cache lookup, rule engine, invariant guard | None (in-memory) + per-segment file stats | Engine rebuilt per command with wrong policy (PERF-07); frame parsed up to 3× (PERF-27) | Per-effect |
| Effect settlement | `TransitionEffectCoordinator.CoordinateAsync:124-129` → `EffectWorker.RunOnceAsync` | ≤16 passes: global unsettled scan, lease CAS, execute, receipt, plan re-hydration | SQLite (4–6 opens per settled effect) | Whole-plan N+1 per pass; post-commit re-reads (PERF-06) | Per-attempt/effect |
| Evidence write | `DurableFilesystemWriteEffectPlanner:59-117` | Append intent, read-back, ad-hoc worker cycle, receipt | SQLite + filesystem | Full durable-worker machinery per file; in-memory `only:` filter after full hydration (PERF-06) | Per-entity/file |
| Interpretation/validation | `CompositionOutputInterpreterOwner` / `CompositionProductValidatorOwner` | Output parse, evidence candidates, artifact contract checks, hashing | File reads ×2–3 per artifact (PERF-21) | Re-reads within one attempt | Per-attempt |
| Decision session | `DecisionSession.RunAsync:100-260` | Router, resume/open, proposal + recommendation turns, durable turn CAS, artifact writes | SQLite (sync-over-async CAS ×~5, PERF-16); provider turns | Manifest double-load/rewrite (PERF-18) | Per-attempt |
| Completion/certification | `CompletionKernelBoundaryObserver`; `RunCompletionCertification` | Exit-gate snapshot reads, evidence listing, 2–3 agent prompts, per-message evidence files | SQLite full-history reads (PERF-14); durable write cycle per console message (PERF-22) | Growing with workspace age | Per-boundary / per-epic |
| Status | `UnifiedCliRunner.ExecuteStatusAsync:438` | Full snapshot composition, read-receipt re-hash, `json_extract` recovery scan (PERF-20) | SQLite + file re-hash | Acceptable at per-invocation lifecycle | Per-invocation |
| Recovery/import/storage cmds | `RecoveryRuntime`, `CanonicalImportGateway`, storage commands | Scans, migration, export/import | SQLite + filesystem | Proportionate to explicit-operation lifecycle | Failure/admin-only |
| Shutdown | `Program.cs:56` → `LoopRelayCompositionRoot.DisposeAsync:610-626` | Session registry → provider teardown, bounded process kill | Process signals | None found | Shutdown |

---

## 4. Cost-Center Inventory

| Cost center | Locations | Trigger | Lifecycle | Frequency | Scale factor | Evidence |
| ----------- | --------- | ------- | --------- | --------- | ------------ | -------- |
| Schema ensure + repair write txn | `LoopRelayWorkspaceDatabase.cs:109-194,3075-3082`; all store `OpenAsync` helpers | Any write-path store op | Per-operation | 50–100+/attempt (101+ call sites) | ~180 probes + IMMEDIATE txn each | Proven from code (lead-verified) |
| Unpooled connection churn, DELETE-journal commits | `LoopRelayWorkspaceDatabase.cs:1588-1610`; no WAL/busy_timeout anywhere in production | Every DB op | Per-operation | Every store op | Journal file create/delete per write txn | Proven from code (lead-verified) |
| Full repository observation | `RepositoryObserver.cs:49-208` | Any state question | Per-cycle ×4–5 | ≤32 cycles ×4–5 + startup ×2 | O(DB size + artifact bytes + history rows) | Proven from code |
| Storage deep verification | `WorkspaceStorageInspector.cs:18,28,33-34,45` | Inside every observation | Per-observation | ×4–5/cycle | 2× full-DB SHA-256 + FK check + full tree hash | Proven from code (lead-verified) |
| Full-history snapshot load | `CanonicalWorkflowPersistenceStore.cs:1136-1158` (9 unfiltered tables incl. `document_json`) | Observation; state persists; feature effects | Per-observation / ×~5 per attempt | Grows with lifetime attempts | O(history), quadratic aggregate | Proven from code |
| Artifact hash sweep | `RepositoryObserver.cs:728-745,896-912` | Every observation | Per-observation | ×4–5/cycle | O(evidence tree size), grows per epic | Proven from code |
| git process spawns | `RepositoryObserver.cs:829-894`; `CompositionKernelOwners.cs:432,881-944`; `WorkingTreeChangeDetector.cs:28` | Observation, gates, receipts, slice detection | Per-observation/attempt | Several/cycle | Full working-tree scan each | Proven from code |
| Effect pipeline hydration | `CanonicalEffectWorkStore.cs:116-178,331,385,444-512`; `EffectWorker.cs:32-43,248-278`; coordinator `:124-129` | Effect phase; candidate writes | Per-effect step | ≤16 passes × items × deps | (1+3N) queries × full payload JSON | Proven from code |
| Per-turn telemetry | `SessionTelemetryRecorder.cs:43-49`; `CodexUsageProbe.cs:23-56`; `CodexRolloutRepository.cs:39-56` | Every recorded turn (default-on) | Per-turn | Every turn | 1 process spawn + full CODEX_HOME read | Proven from code (lead-verified); Configuration-dependent |
| Approval evaluation | `PermissionHandler.cs:21-53`; `PermissionEvaluatorEngine.cs:30,46-47` | codex server-request (cache miss) | Per-effect | Novel command shapes | Engine + policy merge per command | Proven from code (lead-verified) |
| Rendered-prompt read-back | `CanonicalTransitionPersistenceStores.cs:470-477` | Prompt append | Per-attempt | 1+/attempt | O(all prompts ever, full text) | Proven from code (lead-verified) |
| Evidence listing | `SqliteExecutionEvidenceStore.cs:88-120` | Completion evaluation | Per-boundary | Grows with workspace age | All bodies + SHA-256/row | Proven from code |
| Causality re-derivation | `CompositionPromptExecutionOwner.cs:1922-1934`; `CanonicalWorkflowPersistenceStore.cs:1242-1290` | Delegated planners | 2–4×/cycle | Grows with lifetime attempts | O(all attempts ever) | Proven from code (lead-verified) |
| Decision turn CAS | `DecisionSession.cs:1227-1228` | Turn-state advances | Per-attempt ×~5 | Every decision turn | Sync-over-async SQLite write | Proven from code |
| Status snapshot | `ReadReceiptStaleness.cs:19-60`; `CanonicalStatusSnapshotComposer.cs:32-56` | `status` command | Per-invocation | On demand | Full table + file re-hash | Proven from code; acceptable lifecycle |

Necessary expensive work recorded without a remediation finding: per-line JSON parsing of codex stdout (single-pass, incremental — `CodexAppServerSession.cs:430-436`); `CommitTransitionAsync` single-transaction commit (`CanonicalWorkflowPersistenceStore.cs:130-319` region — correct durability boundary); per-turn session-spine rows (causal guarantee); prompt rendering allocations (the output is the product); codex child processes themselves.

---

## 5. Findings by Severity

### Critical

## `PERF-01 — Full schema verification and one-time repair SQL execute as a write transaction on every store operation`

### Classification
* Category: Database I/O, Lifecycle, Redundant safeguard, Architectural root cause
* Severity: Critical
* Confidence: High
* Evidence status: Proven from code (lead-verified); absolute magnitude Requires benchmark

### Locations
* Entry point: `Program.cs` → `UnifiedCliRunner.RunAsync` → kernel/attempt/effect paths
* Primary locations: [LoopRelayWorkspaceDatabase.cs:109-194](src/LoopRelay.Core/Services/Persistence/LoopRelayWorkspaceDatabase.cs:109) (`EnsureSchemaAsync`), [:3075-3082](src/LoopRelay.Core/Services/Persistence/LoopRelayWorkspaceDatabase.cs:3075) (`CanonicalDataRepairSql`), `ReadSatisfiedRequirementsAsync:1465-1538` (one SQL probe per requirement, ~180 total)
* Related locations: every write-path `OpenAsync` — `CanonicalWorkflowPersistenceStore.cs:1500-1516` (27 ops; adds unconditional `workspace_metadata` upsert, see PERF-19), `SqliteRecoveryStore.cs:1156` (22), `CanonicalImportStore.cs:357` (14), `CanonicalEffectWorkStore.cs:429` (10), `CanonicalRecoveryStore.cs:340` (10), `CanonicalDecisionRecoveryStore.cs:258` (8), `CanonicalInteractionStore.cs:412` (6), `CanonicalStorageOperationStore.cs:141` (4), plus `SqliteSessionTelemetrySink.cs:34` (per turn), `LedgerLoopHistoryStore.cs:33`, `SqliteDecisionSessionResumeStore` (4 sites), `CanonicalCompletionAuthorityStore.cs:200-208`, `CanonicalCheckpointStore.cs:19-26`
* Configuration: none gates it
* Call path: any persistence touch (attempt rows, read receipts, prompt facts, dispatch lifecycle, spine rows, checkpoints, effect intents/leases/receipts, telemetry) → `OpenAsync` → `EnsureSchemaAsync`

### Current Behavior
On a fully migrated (CanonicalV15Complete) database, every call still runs: `PRAGMA foreign_keys` + full `InspectSchemaAsync` → `ClassifyV15ShapeAsync` re-probing ~180 tables/columns/indexes/FKs one query each, workspace-identity validation, a legacy-resume probe, then `BeginTransaction(deferred: false)` (IMMEDIATE — acquires the write lock) executing `CanonicalDataRepairSql` — five `UPDATE … WHERE state='Blocked'` scans over canonical state tables (two of them over the growing `canonical_transition_runs`) plus `DROP TABLE IF EXISTS canonical_blockers` — and commits (lines 145-154). No per-process memoization exists anywhere. Shape-requirement lists are computed properties rebuilt (LINQ + `ToArray` + fingerprint re-hash) on each access (`:73-92, 1250-1415`).

### Trigger and Frequency
Every write-path store operation: conservatively 50–100 connection opens per attempt (spine recorder alone is 3/turn; a candidate evidence write ~8; effect settlement 4–6), multiplied by ≤32 cycles per invocation and by invocations over an epic.

### Why It Matters
Order 10³–10⁴ redundant SQL commands per attempt, plus one gratuitous write transaction (journal create/commit/delete in DELETE-journal mode — see PERF-10) per operation. The IMMEDIATE lock serializes all store operations against each other, including logically read-only ones, making this the dominant fixed overhead and the primary contention source on the hot path.

### Evidence
Lead-verified: `EnsureSchemaAsync` structure at lines 109-155 (structurally-complete branch commits repair SQL every call); repair SQL at 3075-3082; call-site counts from grep across stores. Proven: per-operation invocation, statement structure, IMMEDIATE transaction. Inferred: 50–100 opens/attempt (structural count, not traced). Requires benchmark: wall-clock share.

### Required Semantics
`WorkspaceCompatibilityImportRequiredException` for legacy DBs; hard failure on Unknown/newer schema; migration atomicity for <v15; immutable workspace-identity validation; legacy-resume import idempotency (already `INSERT OR IGNORE`); per-connection `PRAGMA foreign_keys`.

### Root Cause
A process-lifetime invariant (schema is v15-complete; repair is a one-time migration companion) enforced at per-connection lifecycle. A v15-stamped-complete database cannot contain the legacy 'Blocked' vocabulary the repair targets — the safeguard is obsolete on that branch.

### Recommended Remediation
(a) Execute `CanonicalDataRepairSql` and legacy-resume import only in the migration (non-complete) branch — deletes the per-operation write transaction outright. (b) Memoize verification per process keyed by (database path, stamped schema_version + shape fingerprint): full physical probe on first contact or stamp change, cheap 2-row `schema_metadata` read (or nothing) thereafter; subsequent opens run only `PRAGMA foreign_keys`. (c) If full probing is retained for first contact, batch it (single `sqlite_master` read + one `pragma_table_info` per table). Convert requirement-list computed properties to `static readonly` fields.

### Alternatives Considered
Holding one long-lived connection per store would amortize opens but not the ensure work and complicates the import/export file-swap flows; memoization is smaller and preserves the existing connection model. Caching inspection results inside `InspectSchemaAsync` without keying on the stamp would risk staleness across explicit migrations.

### Risks and Tradeoffs
If a second process migrated the same workspace mid-run, a memoizing process would not notice; the stamp re-read variant closes this at negligible cost. No documented multi-writer scenario exists (see §15). Repair-SQL confinement changes nothing observable on healthy databases (lead-verified: the branch condition `structurallyComplete` already proves the vocabulary converged).

### Verification
SQLite command trace (statement count per store op / per attempt) before and after; existing schema/migration and certification fixtures (`PersistenceLifecycleRunner`) unchanged; a legacy-DB fixture still raises the import-required exception.

### Expected Impact
Removes the largest per-operation SQLite multiplier and nearly all incidental write transactions on the hot path; qualitative but structurally certain. Magnitude to be confirmed per §12-M1.

---

## `PERF-02 — Transition state persistence loads the full nine-table snapshot (including all historical raw outputs) to update one keyed row, ~5× per attempt`

### Classification
* Category: Database I/O, Algorithmic complexity, Memory allocation
* Severity: Critical
* Confidence: High
* Evidence status: Proven from code (call structure lead-verified); Scale-dependent (magnitude)

### Locations
* Entry point: `TransitionRuntime.RunAsync`
* Primary locations: [CanonicalTransitionPersistenceStores.cs:37-72](src/LoopRelay.Orchestration.Primitives/Persistence/CanonicalTransitionPersistenceStores.cs:37) (`PersistStateAsync`/`PersistCompletedAsync` → `ExistingOrFallbackAsync:152-171`), `CanonicalWorkflowPersistenceStore.cs:1136-1158` (`LoadSnapshotAsync` — nine unfiltered tables), `:1598-1625` (`ReadTransitionEvidenceAsync` materializes every `document_json`, i.e. every historical raw codex output and commit capture)
* Related locations: `TransitionRuntime.cs:205,265,307,359,582` (the ~5 persist calls per attempt)
* Call path: kernel → controller → `TransitionRuntime` → `PersistStateAsync` → `ExistingOrFallbackAsync` → `LoadSnapshotAsync` → `FirstOrDefault(run => run.RunId == runId)`

### Current Behavior
Each state advance of one transition run loads every workflow/stage/run/evidence/product/gate/effect/warning/marker row ever persisted — deserializing all raw-output JSON documents — then filters in memory for a single run id.

### Trigger and Frequency
~5×/attempt, every attempt, every cycle. The workspace database persists across invocations, so row counts grow for the epic's lifetime; attempt N pays O(N) five times → aggregate O(N²).

### Why It Matters
Unbounded growth of the hottest write path's read cost, including large-document materialization; latency and memory grow with workspace age with no ceiling.

### Evidence
Lead-verified: `PersistStateAsync`/`PersistCompletedAsync` both route through `ExistingOrFallbackAsync` (lines 41, 58). Store-side full-table loads proven by the subsystem auditor from `LoadSnapshotAsync` internals (no WHERE/LIMIT at lines 1179-1822 read sites). Growth rate strongly inferred (3–6 evidence rows per attempt observed in `TransitionRuntime` write sites).

### Required Semantics
Fallback-record construction when the row is missing; read-only access semantics; recovery snapshot contents (`LoadRecoveryAsync`, failure-only, unchanged).

### Root Cause
The store API exposes only snapshot-shaped reads; the owner of the run record re-derives it globally (architectural root cause AR-3, §9).

### Recommended Remediation
Add a keyed `ReadTransitionRunAsync(runId)` (single indexed SELECT) and use it in `ExistingOrFallbackAsync`, preserving the fallback path. No schema change: `run_id` is the natural key.

### Alternatives Considered
Caching the snapshot per attempt would mask the defect and introduce staleness across the attempt's own writes; a keyed read is strictly simpler.

### Risks and Tradeoffs
None material — the record produced must be value-identical to the snapshot-filtered one; a parity test covers it.

### Verification
Query trace per attempt (expect 9 table scans ×5 → 1 keyed read ×5); unit parity test of `PersistStateAsync` output; certification transition suites.

### Expected Impact
Converts a quadratic-aggregate hot-path cost to constant per state advance. Qualitative; magnitude per §12-M2.

---

### High

## `PERF-03 — Repository observation executed 4–5× per kernel cycle; one full observation per attempt feeds a field with no reader`

### Classification
* Category: Unnecessary operation, Lifecycle, Architectural root cause
* Severity: High
* Confidence: High
* Evidence status: Proven from code (no-reader claim and call sites lead-verified)

### Locations
* Entry point: `UnifiedCliRunner.RunCoreAsync` / `OrchestrationKernel.RunAsync`
* Primary locations: [WorkflowChaining.cs:352-370](src/LoopRelay.Orchestration.Primitives/Chaining/WorkflowChaining.cs:352) (post-attempt `ObserveAsync` whose result becomes `ObservationAfter`), [WorkflowChaining.cs:106](src/LoopRelay.Orchestration.Primitives/Chaining/WorkflowChaining.cs:106) (`ObservationAfter` — sole occurrence of the identifier in `src/`; no reader), `CompositionKernelOwners.cs:104-148` (`RepositoryObservationProductResolver` re-observes globally per attempt), `TransitionRuntime.cs:59` (attempt-start resolve), `OrchestrationKernel.cs:122` (kernel re-observe), `WorkflowChaining.cs:515` (chain-boundary observe), `UnifiedCliRunner.cs:365,374` (startup double observation; the effect-worker result between them is ignored)
* Related locations: `SnapshotInputFreshnessValidator.cs:18-19` (freshness re-resolve — **required**, do not touch)
* Call path: kernel cycle → chain runner → controller → runtime; each layer re-derives global state

### Current Behavior
One kernel cycle performs ~4–5 complete observations (each: storage verification per PERF-04, full persistence projection per PERF-05, artifact hash sweep per PERF-23, `git status` spawn). Lead-verified specifics: the controller's post-attempt observation (`:354`) is placed into `WorkflowControllerResult.ObservationAfter` positionally (`:369`) after the stop decision is already computed (`:355-363`), and no production code reads that field — the kernel performs its own fresh `ObserveAsync` at `:122` instead. The attempt-start product resolver rebuilds a full observation to answer "which of ≤5 required input products are usable" while the controller already holds an observation of the same instant (`WorkflowControllerRequest.Observation`, `:96`). At startup the runner observes, runs one effect-worker pass whose result is discarded, and unconditionally observes again.

### Trigger and Frequency
Every cycle (≤32/invocation) plus startup ×2 and chain boundaries.

### Why It Matters
Observation is the single most expensive derivation in the system (PERF-04/05/23 describe its interior); multiplying it 4–5× per cycle multiplies every one of those costs. One of the five is provably pure waste.

### Evidence
Lead-verified: grep for `ObservationAfter` returns only the declaration; `:354` call and positional construction confirmed; stop-reason computation independent of the observed value confirmed.

### Required Semantics
A fresh observation at each kernel cycle boundary (codex mutates the tree between cycles) and the promotion-time freshness re-resolution (its contract is detecting concurrent change — reusing an older observation there would narrow detection). Corruption fail-closed behavior.

### Root Cause
Observation has no owner and no validity window: every consumer forces a fresh global derivation instead of the cycle handing its observation down (AR-2, §9). Duplicate ownership of "post-attempt state" between controller and kernel.

### Recommended Remediation
(1) Delete the `ObserveAsync` at `WorkflowChaining.cs:354` and the `ObservationAfter` field — or have the kernel consume `ControllerResult.ObservationAfter` at `:122` instead of re-observing; either way exactly one observation per cycle is saved. (2) Pass the cycle's ambient observation into the attempt-start `IProductResolver` (request-scoped adapter); keep the freshness validator's own resolution untouched. (3) Re-observe at startup only when the effect worker reports executed work.

### Alternatives Considered
A time-based observation cache — rejected: introduces a validity policy where explicit hand-down is simpler and exact.

### Risks and Tradeoffs
Tests may consume `ObservationAfter` (tests were out of scope); deletion requires a test sweep. Reusing the cycle observation for product resolution widens (never narrows) the freshness-conflict window because the validator still re-observes at promotion time.

### Verification
Multi-cycle run trace counting `ObserveAsync` invocations per cycle (expect 4–5 → 2: cycle + freshness); product gate outcomes and freshness-conflict certification tests unchanged.

### Expected Impact
Removes ~40–60% of observation executions per cycle by count (structural; wall-clock per §12-M3).

---

## `PERF-04 — Deep storage integrity verification (double full-DB SHA-256, full tree hash, foreign_key_check) fused into every routine observation`

### Classification
* Category: File I/O, Database I/O, Redundant safeguard (misplaced boundary)
* Severity: High
* Confidence: High
* Evidence status: Proven from code (lead-verified); absolute cost Scale-dependent

### Locations
* Entry point: any `RepositoryObserver.ObserveAsync`
* Primary locations: [WorkspaceStorageInspector.cs:18](src/LoopRelay.Orchestration.Primitives/Storage/WorkspaceStorageInspector.cs:18) (`InventoryAsync` SHA-256s every file under `.LoopRelay/persistence` — including the database), [:28](src/LoopRelay.Orchestration.Primitives/Storage/WorkspaceStorageInspector.cs:28) (the database hashed a second time for `byteHash`), `:33-34` (full read-only shape classification + `ForeignKeyViolationsAsync`), `:44-47` (interrupted-row scans)
* Related locations: `RepositoryObserver.cs:54` (verification inside every observation)
* Call path: every observation listed under PERF-03

### Current Behavior
Lead-verified: the persistence directory inventory hashes the DB file (it resides in that directory), then line 28 computes an independent second SHA-256 of the same file; `PRAGMA foreign_key_check` walks every child table of an append-only ledger; full shape classification re-runs the ~180-probe inspection read-only. All of this executes 4–5× per cycle on the healthy path.

### Trigger and Frequency
Per observation (see PERF-03 multipliers). Cost is O(database bytes + persistence-tree bytes + all FK'd rows), all of which grow monotonically.

### Why It Matters
Whole-database hashing twice per observation, several times per cycle, is deep-integrity work (a `storage verify` command concern) executing at routine-observation lifecycle.

### Evidence
Lead-verified inventory/byteHash duplication and FK check. Frequency proven via PERF-03 call sites.

### Required Semantics
Refusal to mutate unusable/corrupt authorities; `StorageHealth` semantics for explicit storage commands; interrupted-operation detection before mutation; corrupt-stamp detection must still block mutation.

### Root Cause
One verification tier serves two boundaries: explicit operator verification (deep) and routine pre-mutation health checks (needs only: file exists, stamp well-formed, interrupted markers absent).

### Recommended Remediation
(1) Reuse the inventory's hash for `byteHash` (pure deletion of one full-file hash). (2) Introduce a light health tier for routine observation (existence + stamp + interrupted markers + journal-artifact check), keeping deep verification (hashes, FK check, full classification) for `storage verify`, startup, and cycle boundaries if desired.

### Alternatives Considered
Skipping verification when DB (size, mtime) is unchanged within a cycle — viable but weaker than an explicit tier split; mtime granularity on NTFS makes it slightly less trustworthy.

### Risks and Tradeoffs
A corrupted-but-stamped database would be caught later (first failing operation or explicit verify) instead of at next observation; SQLite itself fails loudly on structural corruption, and mutation paths keep their own guards.

### Verification
Storage certification suites; benchmark `ObserveAsync` on an aged workspace before/after (§12-M3); confirm `storage verify` output unchanged.

### Expected Impact
Removes the largest fixed I/O block inside each observation; compounds with PERF-03.

---

## `PERF-05 — Observation read model loads unbounded full-history ledger tables on every observation`

### Classification
* Category: Database I/O, Algorithmic complexity, Memory allocation, Architectural root cause
* Severity: High
* Confidence: High
* Evidence status: Proven from code; growth rate Strongly inferred; magnitude Scale-dependent

### Locations
* Entry point: any `ObserveAsync` → `CanonicalPersistenceProjection.ProjectAsync` (`CanonicalPersistenceReadModel.cs:49-77`)
* Primary locations: `CanonicalWorkflowPersistenceStore.cs:1179,1270,1317-1377,1418,1462,1576,1607,1707,1742,1780,1822` — no WHERE/LIMIT on any read
* Related locations: consumers `RepositoryObserver.cs:68-71,122-158,181-203`; per-transition re-scan `WorkflowResolver.cs:186-189`; per-cycle JSON+SHA-256 of the whole observation `OrchestrationKernel.cs:159-171` (has a real consumer — decision-fact identity — but scales with history)
* Call path: PERF-03 call sites

### Current Behavior
All runs, attempts, transition runs, evidence events, gate evaluations, effect records, warnings, agent turns, and read receipts ever written are read, JSON-deserialized, and flattened per observation.

### Trigger and Frequency
4+ observations/cycle × ≤32 cycles × invocations over an epic; evidence grows 3–6 rows per attempt → per-observation cost grows linearly with lifetime attempts, aggregate quadratic.

### Why It Matters
Same growth trajectory as PERF-02 but multiplied by observation count; also inflates the per-cycle snapshot serialization.

### Evidence
Read sites proven (no filtering); consumers' actual needs (latest state per key, current products, unsettled effects, current-run boundaries) established by the subsystem auditor from `RepositoryObserver`/`WorkflowResolver` usage.

### Required Semantics
`WorkflowResolver`'s completed-run detection; gate-usability inputs; recovery markers; evidence identities appearing in decision facts.

### Root Cause
Append-only ledger design leaked into the read model; no scoping or watermark (AR-3, §9).

### Recommended Remediation
Scope `LoadSnapshotAsync` reads: latest-per-key for transition runs (SQL window or keyed max), current-run filters for evidence/gate/effect tables; split "current state" (per-observation) from "full history" (on-demand, status/recovery only).

### Alternatives Considered
Retention/compaction of old rows — a bigger semantic change (ledger is evidence); read-side scoping preserves the ledger untouched.

### Risks and Tradeoffs
Any consumer silently depending on full-history presence in the observation object would break; the auditor found none, but the change should land behind parity tests on observation-derived decisions.

### Verification
Row-count trace per observation on an aged fixture; decision-fact and resolver outputs byte-identical for equal current state.

### Expected Impact
Bounds observation cost by current-state size instead of workspace age. Compounds with PERF-03/04.

---

## `PERF-06 — Effect pipeline amplification: whole-plan N+1 hydration per pass, 4–6 connection opens per settled effect, post-commit re-reads, unfiltered global scans per candidate write`

### Classification
* Category: Database I/O, Algorithmic complexity
* Severity: High
* Confidence: High
* Evidence status: Proven from code; constants Scale-dependent

### Locations
* Entry point: `TransitionEffectCoordinator.CoordinateAsync` (per effects-bearing transition); `DurableFilesystemWriteEffectPlanner` (per evidence file)
* Primary locations: `CanonicalEffectWorkStore.cs:116-147` (scan N+1), `:155-178` (plan N+1), `:331,385` (post-commit full re-read after lifecycle append/receipt), `:444-512` (every item read loads its full event history + receipt), `EffectWorker.cs:32-43` (`only:` filter applied in memory after full hydration of up to 128 unsettled intents whose `definition_json` embeds entire file contents), `:248-278` (per-dependency re-read of dependency intent and full transition plan, per item, per pass), `TransitionEffectCoordinator.cs:124-129` (≤16 passes, each with a full plan re-read), `DurableFilesystemWriteEffectPlanner.cs:24-34,59-117` (full worker cycle per candidate file; `ScheduleAsync` reads the whole plan to find one Started parent)
* Call path: controller → coordinator → worker; interpreter evidence writes (`CompositionOutputInterpreterOwner.cs:136-450`); completion flush (`CompositionPromptExecutionOwner.cs:2062-2080`)

### Current Behavior
Settling one effect costs ~4–6 connection opens (each paying PERF-01) including re-reading the just-committed item whose state/rowversion are already known in-transaction. Plan hydration is 1+~3N queries with full payload JSON deserialization, repeated per pass and per dependency edge. Writing one evidence file spins the general-purpose worker over the global unsettled backlog with the target filter applied only after hydration.

### Trigger and Frequency
Per effects-bearing transition (≈1/attempt) and per evidence candidate (5–20/attempt); passes × items × dependencies multiply.

### Why It Matters
Dozens of redundant queries and full-payload deserializations per transition; grows quadratically with evidence volume per transition and with any unsettled backlog.

### Required Semantics
Lease CAS semantics; row-version conflict detection; lifecycle transition policy; idempotency-key dedupe; dependency-barrier semantics (`EffectWorker.cs:261-275`); receipt verification; intent-before-write ordering.

### Root Cause
Durable-receipt machinery built once at maximum generality and reused for simple known-target writes without targeted access (AR-4, §9).

### Recommended Remediation
Push the `only:` filter and the Started-parent lookup into SQL (`WHERE effect_intent_id = ?` / `WHERE transition_run_id = ? AND status='Started' AND executor_key LIKE 'canonical-transition-effect:%'`); return the updated item from the settling transaction instead of re-reading; hydrate plans in one JOIN and reuse one plan snapshot within a coordination pass; project id/state only for dependency checks (lazy payloads).

### Alternatives Considered
Batching all evidence writes of one interpretation into one intent — larger semantic change to receipt granularity; targeted access achieves most of the win without it.

### Risks and Tradeoffs
Cache-within-a-pass must not span passes (durable-barrier check depends on fresh child-effect state between passes).

### Verification
Query-count trace over one Execute transition and one candidate write (§12-M4); effect-ordering certification suite.

### Expected Impact
Reduces per-effect persistence traffic by a large constant factor; compounds with PERF-01.

---

## `PERF-07 — Permission evaluator engine is rebuilt per command and evaluates the built-in default policy instead of the configured one`

### Classification
* Category: CPU, Memory allocation, Duplicate authority (with a policy-fidelity defect)
* Severity: High (severity carried by reliability/policy enforcement; pure CPU cost alone would be Medium)
* Confidence: High
* Evidence status: Proven from code (lead-verified)

### Locations
* Entry point: codex approval request → `CodexAppServerSession.EnqueueApprovalResponse:580-603` → `PermissionGateway.Evaluate` → `PermissionHandler.Evaluate:48`
* Primary locations: [PermissionEvaluatorEngine.cs:30](src/LoopRelay.Permissions/Services/Evaluation/PermissionEvaluatorEngine.cs:30) (`Evaluate` calls static `EvaluateSingle`), [:46-47](src/LoopRelay.Permissions/Services/Evaluation/PermissionEvaluatorEngine.cs:46) (`EvaluateSingle` allocates `new PermissionEvaluatorEngine()` — parameterless ctor → `PermissionPolicyOptions.Default` — and evaluates on that instance, so the singleton's `_policy` is never consulted)
* Related locations: `PermissionPolicyFactory.cs:13-26` (`MergeWithMinimum` computes `MergeHardDeny` twice, builds ~10 FrozenSets per engine construction); DI registration from settings `ServiceCollectionExtensions.cs:32-48`; custom policy loaded at `CliSettingsLoader.cs:110-111`
* Configuration: `settings.json` `permissions` section (operator-authored rules)
* Call path: every cache-miss approval, per parsed command segment; approvals block the codex turn

### Current Behavior
Lead-verified: for each command, a fresh engine + full policy merge is constructed and rule evaluation runs against the **default** policy. The configured policy is honored only by construction of the unused `_policy` field. The hard-deny minimum still holds (defaults include it) and the singleton `InvariantGuard` (correct policy) backstops denies.

### Trigger and Frequency
Per novel command shape (fingerprint cache absorbs repeats — `InMemoryPermissionCache`); on the turn-blocking approval path.

### Why It Matters
Wasted FrozenSet/merge construction per command on a latency-critical path — and, more importantly, operator-configured allow/review rules are silently ignored by rule evaluation. That is a reliability/enforcement defect discovered via the performance pattern; it is reported here because the remediation is the same line.

### Required Semantics
Deny short-circuit aggregation; closed-world deny; invariant-guard ordering; hard-deny minimum.

### Root Cause
A static test-support helper (`EvaluateSingle`) left on the production instance path.

### Recommended Remediation
Change line 30 to call the instance method `EvaluateSingleCore(command)`. Hoist `MergeHardDeny` to a local in `MergeWithMinimum`. Keep the static only if tests require it.

### Risks and Tradeoffs
**This is a deliberate behavior change**: custom policies that operators believed were active will start taking effect. That is the registered intent (DI passes the configured policy), but any environment relying on the current default-only behavior would observe different approval outcomes. Flag in release notes.

### Verification
Unit test: engine constructed with a custom `SafeBashCommands` set honors it; allocation profile of the approval path; existing permission suites.

### Expected Impact
Removes per-command construction cost; restores policy fidelity. Qualitative.

---

## `PERF-08 — Resolving one rollout path for telemetry reads the entire CODEX_HOME store`

### Classification
* Category: File I/O, Unnecessary operation (for this consumer)
* Severity: High
* Confidence: High
* Evidence status: Proven from code (call site lead-verified); magnitude Scale-dependent

### Locations
* Entry point: per recorded provider turn (telemetry default-on)
* Primary locations: [SessionTelemetryRecorder.cs:44-49](src/LoopRelay.Cli/Services/Telemetry/SessionTelemetryRecorder.cs:44) (consumes only `exact.Location`), `CodexRolloutRepository.cs:39-56` (recursive enumeration of `sessions/`, `archived_sessions/`, `archived/`), `:88` (whole-file `ReadAllTextAsync` before the first-line id check can bail), `:96,112` (full split + JsonDocument per line), `:270` (SHA-256 of full content); no early exit after a match (ambiguity check scans everything)
* Related locations: `FileSystemCodexRolloutLocator.cs:36-58` (fallback locator opens/parses the first line of every rollout file ever created; `GatedAgentRuntime.cs:49-53` passes `cachedLogPath: null` and discards the returned path, so the scan repeats)
* Configuration: `policy.runtime.sessionTelemetry: true` (default)
* Call path: `RecordTurnAsync` → `CodexRolloutRepository.ReadExactAsync` / `_locator.Resolve`

### Current Behavior
A forensic-grade reader (built for resume-failure diagnosis, where its rigor is appropriate) is reused to answer "what is the path of this thread's rollout file": every file in the user's real codex home — months of rollouts, files up to MBs — is fully read, line-parsed, and hashed to return a filename.

### Trigger and Frequency
Per recorded turn when no cached path is supplied (the gated runtime supplies none and does not retain the result), for every session with a provider thread id.

### Why It Matters
Hundreds of MBs of file I/O and thousands of JSON parses per turn on real user machines, growing with the user's global codex usage — unrelated to this workspace.

### Required Semantics
Exact-match plus Ambiguous/Partial/Corrupt classification for the resume-failure and recovery consumers (`AgentRuntime.cs:477`, `RecoverySources.cs:47`) — unchanged. Fail-to-null for telemetry.

### Root Cause
Consumer-blind reuse: a path-only consumer bound to a full-forensics API; the caller also discards the result it could cache.

### Recommended Remediation
For the telemetry consumer: filter candidates by filename (codex rollout filenames embed the thread id) or read only each file's first line to match `session_meta.id`; parse fully only the single match; skip digest/record materialization when only `Location` is needed. Cache the resolved path per session in the runtime (it already has a `cachedLogPath` parameter designed for this). Date-bound the fallback locator's scan (only day directories ≥ `openedAtUtc.Date`).

### Risks and Tradeoffs
None to diagnosis paths if the fast path is a separate method; telemetry keeps fail-open behavior.

### Verification
File-open count during one turn against a populated codex home (§12-M5); diagnosis-path certification fixtures unchanged.

### Expected Impact
Eliminates the largest per-turn file-I/O cost on machines with real codex history.

---

## `PERF-09 — A codex app-server child process is spawned after every turn to fill two nullable telemetry columns`

### Classification
* Category: Network I/O (process/IPC), Lifecycle
* Severity: High
* Confidence: High
* Evidence status: Proven from code (lead-verified); Configuration-dependent (default-on)

### Locations
* Entry point: every recorded provider turn
* Primary locations: [SessionTelemetryRecorder.cs:43](src/LoopRelay.Cli/Services/Telemetry/SessionTelemetryRecorder.cs:43) (`ProbePostAsync` on every `RecordTurnAsync`), `CodexUsageProbe.cs:23-56,91-128` (resolve executable, spawn `codex app-server`, JSON-RPC initialize + `account/rateLimits/read`, up to 30 s timeout, serialized after the turn)
* Configuration: `policy.runtime.sessionTelemetry: true` (default)
* Call path: `GatedAgentRuntime.cs:39-55` / `GatedAgentSession.cs:55` → `SessionTelemetryRecorder.RecordTurnAsync`

### Current Behavior
Each turn pays a full child-process spawn and JSON-RPC handshake to read quota percentages whose only consumers are the `FiveHourRemainingPercent`/`WeeklyRemainingPercent` telemetry columns. Usage-limit gating does **not** consume it — `UsageLimitDetector.cs:58-85` parses failed-turn diagnostics instead.

### Trigger and Frequency
Every turn, many per attempt, serialized on the turn's completion path before control returns.

### Why It Matters
Likely the largest fixed per-turn wall-clock overhead in the agent layers; quota validity is minutes-scale, far exceeding turn cadence.

### Required Semantics
Fail-open (null on any failure); cancellation propagation; per-turn token rows unchanged.

### Root Cause
The probe owns its own transport at per-event lifecycle instead of a shared, validity-scoped one.

### Recommended Remediation
Throttle: probe at most once per N minutes (or once per kernel cycle), reusing the last snapshot for intermediate rows; or issue `account/rateLimits/read` over the already-open `CodexAppServerSession`.

### Risks and Tradeoffs
Telemetry rows between probes carry a slightly stale percentage — acceptable for a monotonically decaying quota; note the snapshot timestamp if precision matters.

### Verification
Process-spawn count per attempt with telemetry on (§12-M5); turn wall-clock before/after.

### Expected Impact
Removes one process spawn + handshake per turn.

---

## `PERF-10 — Connection pooling disabled on all factories; no WAL/busy_timeout/synchronous tuning anywhere in production`

### Classification
* Category: Database I/O, Resource management
* Severity: High
* Confidence: High
* Evidence status: Proven from code (lead-verified); magnitude Requires benchmark

### Locations
* Primary locations: [LoopRelayWorkspaceDatabase.cs:1588-1610](src/LoopRelay.Core/Services/Persistence/LoopRelayWorkspaceDatabase.cs:1588) (`Pooling = false` in all three factories); `WorkspaceDatabaseInspection.cs:29-39`
* Related locations: no production `PRAGMA journal_mode/busy_timeout/synchronous` exists (grep-verified; only `LoopRelay.Certification` sets busy_timeout — `PersistenceLifecycleRunner.cs:279`)
* Call path: every store operation

### Current Behavior
Every operation pays sqlite3 open/close (file open, header read, lock probe, journal-recovery check). The database runs in rollback-journal (DELETE) mode: every write transaction creates and deletes a `-journal` file — two extra NTFS file create/delete operations per commit. With PERF-01 unfixed there are ≥2 write commits per store op.

### Trigger and Frequency
All persistence, all lifecycles.

### Why It Matters
File open/create/delete are the expensive syscalls on Windows; this multiplies every persistence touch, and the absence of busy_timeout converts any transient lock overlap into an immediate failure rather than a short wait.

### Required Semantics
Durability of committed transactions (WAL preserves it; choose `synchronous=NORMAL` vs FULL deliberately); import/export flows that copy or replace the database file must checkpoint/close WAL first (`WorkspaceStorageApplicationService`, `CanonicalImportGateway`); inspection code that hashes the DB file must account for `-wal` side files.

### Root Cause
Connection-string defaults chosen for isolation and never revisited; no owner for connection-lifetime policy (AR-5, §9).

### Recommended Remediation
Enable pooling (or long-lived per-store connections) and set `journal_mode=WAL` + `busy_timeout` once at first open. If pooling must stay off for the file-swap flows, scope the exception to those flows. Coordinate with PERF-04 (inventory hashing must include WAL/SHM or checkpoint first).

### Alternatives Considered
Leaving DELETE mode and only pooling — halves the win; WAL also removes writer-blocks-reader stalls that PERF-01's IMMEDIATE transactions currently amplify.

### Risks and Tradeoffs
Whether `Pooling=false` is deliberate for DB-file replacement during import/export is undocumented (§15); the remediation must verify those flows on a WAL database.

### Verification
Benchmark one loop iteration's persistence wall time before/after (§12-M1); export/import round-trip tests on WAL.

### Expected Impact
Reduces per-operation fixed cost and journal churn across every finding above; magnitude requires the benchmark.

---

### Medium

## `PERF-11 — ResolveCausalityAsync scans the entire attempts table to rebuild causality already held in memory`

### Classification
* Category: Database I/O, Unnecessary operation
* Severity: Medium (grows with workspace age)
* Confidence: High
* Evidence status: Proven from code (scan lead-verified); in-hand equivalence Strongly inferred

### Locations
* Primary: [CompositionPromptExecutionOwner.cs:1922-1934](src/LoopRelay.Cli/Services/Cli/CompositionPromptExecutionOwner.cs:1922) (`ReadAttemptsAsync` → `.Single(...)` in memory + `ReadWorkspaceIdentityAsync`); `CanonicalWorkflowPersistenceStore.cs:1242-1290` (unfiltered SELECT + 2 PRAGMA column checks per call); `:1493-1498` (identity read uses write-path `OpenAsync` → full PERF-01 cost)
* Related: callers at `:850,944,1007,1114,1315,1492-1493,1680,1734,1919`
* Call path: delegated effect/recovery planners inside the prompt execution owner, 2–4×/cycle

### Current Behavior
Reads every attempt row ever written to reconstruct `(RunId, WorkflowInstanceId, TransitionRunId, AttemptId)` — values the dispatcher already holds: `PromptDispatchAuthorization.Causality` is a complete `CanonicalCausalContext` built by `TransitionRuntime.cs:52-57,141-147` from the same fields persisted into the attempt row, retained in `CurrentAuthorization` (`:176`).

### Why It Matters
O(total-attempts-ever) read repeated several times per cycle, forever.

### Required Semantics
Throw when execution context is unavailable; identical causal identity values.

### Root Cause / Remediation
Re-derivation from the database instead of reusing the authorized context (AR-3). Return `CurrentAuthorization.Causality`; if an existence check is desired, use `WHERE attempt_id = ? AND transition_run_id = ?`.

### Verification
Unit-compare both contexts across transition families before switching; query trace.

### Expected Impact
Removes a growing full-table scan from the per-cycle path.

---

## `PERF-12 — Rendered-prompt append reads back the entire prompt table (full text bodies) to compute one ordinal`

### Classification
* Category: Database I/O, Redundant safeguard
* Severity: Medium
* Confidence: High
* Evidence status: Proven from code (lead-verified)

### Locations
* Primary: [CanonicalTransitionPersistenceStores.cs:470-477](src/LoopRelay.Orchestration.Primitives/Persistence/CanonicalTransitionPersistenceStores.cs:470) (`AppendAsync` → `ReadRenderedPromptsAsync` → `FindIndex`); `CanonicalWorkflowPersistenceStore.cs:749-797` (reads `rendered_text` of every prompt); fallback `ReadAsync:488-543` loads full prompts + attempts tables
* Call path: `TransitionRuntime` prompt rendering, ≥1/attempt

### Current Behavior
After inserting one prompt, every historical rendered prompt (full text) is loaded to locate the new row's index and confirm readability.

### Required Semantics
"Not readable after append" failure detection (a keyed re-read preserves it); ordinal semantics; in-memory cache behavior.

### Root Cause / Remediation
Read-back safeguard implemented via the only available read (full table). Compute the ordinal via `COUNT(*)` in the same connection or a keyed `WHERE rendered_prompt_id = ?` read.

### Verification
Prompt persistence tests; query trace per attempt.

---

## `PERF-13 — Feature-effect executor loads the full nine-table snapshot; consumes products only`

### Classification
* Category: Database I/O
* Severity: Medium
* Confidence: High
* Evidence status: Proven from code; Scale-dependent

### Locations
* Primary: `CanonicalFeatureEffectExecutor.cs:96-99` → `LoadSnapshotAsync`; sole consumption `snapshot.Products.Where(produced.Contains(...))`
* Call path: each canonical-transition-effect intent (≈1/transition)

### Remediation
Read products only (existing `ReadProductsAsync` on a read-only connection), filtered by identity. Preserve "re-observe committed products" semantics.

---

## `PERF-14 — Evidence listing materializes and SHA-validates every body to return paths`

### Classification
* Category: Database I/O, Serialization
* Severity: Medium
* Confidence: High
* Evidence status: Proven from code; growth Scale-dependent

### Locations
* Primary: `SqliteExecutionEvidenceStore.cs:88-120` (SELECT includes `body`; `ValidateHash` per row; glob filter applied in memory after fetch); consumer `CompletionArtifacts.cs:37-45` keeps only `RelativePath`
* Call path: `CompletionPromptContextBuilder.cs:47` / `CompletionCertificationService.cs:53` → `CompletionArtifacts.ListAsync`

### Current Behavior / Why It Matters
Every completion-phase listing transfers all evidence markdown bodies (unbounded growth) and hashes each, then discards everything but paths; rows failing the glob are still fully read and hashed.

### Remediation
Path-only listing (`SELECT stem, sequence, logical_path`) for path consumers; filter by pattern in SQL; hash-validate only rows whose content is returned. Content consumers keep validation; preserve (stem, sequence) ordering.

---

## `PERF-15 — Read guards re-run full physical shape classification per read; migration executor double-inspects`

### Classification
* Category: Database I/O, Redundant safeguard
* Severity: Medium
* Confidence: Medium-High
* Evidence status: Proven from code

### Locations
* Primary: `LoopRelayWorkspaceDatabase.cs:196-406` (`InspectSchemaAsync`/`ClassifyV15ShapeAsync`, ~180 probes); consumers `LedgerLoopHistoryStore.cs:104-109` (per `ReadLatestAsync`), `WorkspaceStorageInspector.cs:33`, `ImportPortfolioDetector.cs:44`; `WorkspaceSchemaMigrationExecutor` (`WorkspaceDatabaseInspection.cs:54-55`) runs `EnsureSchemaAsync` then `InspectSchemaAsync` back-to-back on the same connection

### Current Behavior
"Is this a canonical v15 DB?" is answered by re-verifying every table/column/index/FK although the answer is stamped in 4 `schema_metadata` rows whose validity the write path already enforces.

### Remediation
Same memoization as PERF-01, or an `InspectStampedAsync` that trusts a well-formed stamp and falls back to full classification when absent/mismatched. Delete the redundant second inspection in the migration executor (ensure already verified shape in-transaction). Corrupt-stamp detection must still block mutation.

---

## `PERF-16 — Sync-over-async durable writes on provider streaming and decision-turn paths`

### Classification
* Category: Async execution, Locking
* Severity: Medium
* Confidence: High
* Evidence status: Proven from code

### Locations
* Primary: `DecisionSession.cs:1227-1228` (`CompareAndSwapDecisionTurnAsync(...).GetAwaiter().GetResult()`, ~5 advances/turn under `AgentTurnProgress.Use`); `TransitionFaultsAndRecovery.cs:287` (`journal.RecordAsync(...).GetAwaiter().GetResult()`, ~5-6 boundary events/attempt), production-wired at `LoopRelayCompositionRoot.cs:435,462`
* Call path: streaming callbacks during codex turns

### Why It Matters
Blocks callback/thread-pool threads on SQLite I/O (each write also paying PERF-01) on the hottest streaming path. No deadlock in console context; cost is thread blocking and latency jitter.

### Required Semantics
Ordering (sequence numbers) and durability of each state advance **before** proceeding / before `ShouldInterrupt` fires — recovery depends on them.

### Remediation
Make the observer contract async, or enqueue advances to a channel drained by an awaited writer that preserves ordering and completes before the turn advances.

### Verification
Fault-injection certification suite; no `.GetAwaiter().GetResult()` on the turn path.

---

## `PERF-17 — Workflow resolved twice per cycle from identical immutable inputs`

### Classification
* Category: CPU, Memory allocation
* Severity: Medium
* Confidence: High
* Evidence status: Proven from code

### Locations
* Primary: `WorkflowChaining.cs:430` (chain runner) and `:304` (controller), same `(invocation, observation, definitions)`; third `InvocationModeResolver.Resolve` at `:423`
* Related: `WorkflowResolver.cs:153-236` scans products×requirements and full-history `TransitionRuns` per transition — cost tracks PERF-05's growth

### Remediation
Pass the `WorkflowResolutionResult` into `WorkflowControllerRequest`; controller resolves only if absent. Both call sites use the same observation instance, so inputs are provably identical. Verify decision-fact eligible/rejected lists unchanged.

---

## `PERF-18 — Projection manifest loaded twice and rewritten unconditionally per ensure`

### Classification
* Category: File I/O, Serialization
* Severity: Medium
* Confidence: High
* Evidence status: Proven from code

### Locations
* Primary: `ProjectContextProjectionService.cs:36,93`; `ProjectionManifestStore.cs:56-60` (`UpsertAsync` re-runs `LoadAsync` then always `SaveAsync`); `StructuredJsonDocumentStore.cs:15-43` (bypasses `FileSystemArtifactStore`'s deserialization cache)
* Call path: `DecisionSession.BuildProposalPromptAsync:283-285` → `EnsureDecisionProjectionAsync:1278`; `CompositionPromptExecutionOwner.cs:400`

### Current Behavior
Per ensure: manifest deserialized + validated twice; `projections-manifest.json` atomically rewritten even when byte-identical (steady state), churning mtime — which also invalidates the artifact store's signature cache for that path.

### Remediation
Pass the loaded manifest into an `UpsertAsync(manifest, entry)` overload; skip save when the upserted manifest equals the loaded one (record equality). Keep the write-before-throw behavior on the validation/freshness exception paths (`:78,84`).

### Verification
Steady-state loop shows no manifest mtime change between iterations.

---

## `PERF-19 — Unconditional persistence_state upsert per workflow-store open`

### Classification
* Category: Database I/O
* Severity: Medium
* Confidence: High
* Evidence status: Proven from code

### Locations
* Primary: `CanonicalWorkflowPersistenceStore.cs:1509-1515` (every `OpenAsync` of the busiest store commits a `workspace_metadata('persistence_state','canonical')` upsert); consumer `LoopWorkspaceDatabase.cs:51`

### Remediation
`ON CONFLICT(key) DO UPDATE ... WHERE value <> excluded.value` (no page dirtied when unchanged), or stamp once per process alongside PERF-01's memoized ensure. Preserve the 'imported' → 'canonical' transition on first canonical write (asserted by `CanonicalWorkflowPersistenceStoreTests:46-52,188`).

---

## `PERF-20 — Latest-recovery-attempt lookup via unindexable json_extract join; helper chain opens ~6 connections per logical operation`

### Classification
* Category: Database I/O
* Severity: Medium (status/recovery lifecycle; table grows for workspace life)
* Confidence: Medium
* Evidence status: Proven from code; Scale-dependent

### Locations
* Primary: `CanonicalDecisionRecoveryStore.cs:198-215` (join on `json_extract(document_json,'$.scopeId')` — parses every recovery action event per read); `:102-135,165,217-249` (RecordPlanAsync chain: ~6 open+ensure cycles)
* Call path: `CanonicalStatusSnapshotComposer.cs:28-30` (status), `DecisionSessionRecoveryCoordinator` (resume failures)

### Remediation
Persist `scope_id` as a column (or filter via indexed plan/case ids); share one connection across the helper chain. Preserve latest-wins ordering by event_id and CAS conflict semantics.

---

## `PERF-21 — Product validator re-reads each artifact 2–3× per attempt`

### Classification
* Category: File I/O
* Severity: Medium (Low absolute per-file cost, per-attempt multiplication)
* Confidence: High
* Evidence status: Proven from code

### Locations
* Primary: `CompositionProductValidatorOwner.cs:684` (emptiness read), `:724-727` (contract re-read), `:771-790` (`HashArtifactsAsync` re-reads everything again); same pattern `:458` vs `:564`

### Remediation
Read each artifact once into a map; validate and hash from memory. Preserve hash-over-sorted-paths format (`--- path ---` framing) and failure messages; verify identical causal identity on unchanged inputs.

---

## `PERF-22 — Completion-phase console markers each pay a full durable-effect write cycle`

### Classification
* Category: Database I/O, File I/O
* Severity: Medium (completion/failure lifecycle only)
* Confidence: Medium
* Evidence status: Proven from code

### Locations
* Primary: `CompositionPromptExecutionOwner.cs:1997-2107` (Record per Phase/Info/Warn; `FlushAsync` loops `WriteCandidateAsync` per message — full PERF-06 machinery each)

### Remediation
Batch pending markers into one append + one worker run (or one combined evidence file) — dependent on the PERF-06 targeted-access fix. Preserve per-marker paths/sequence if recovery consumes them individually (verify before merging files).

---

## `PERF-23 — Every observation re-hashes the full content of every artifact file; milestone files read twice`

### Classification
* Category: File I/O, CPU
* Severity: Medium
* Confidence: Medium-High
* Evidence status: Proven from code (structure); Scale-dependent (magnitude)

### Locations
* Primary: `RepositoryObserver.cs:896-912` (`HashExistingFiles`), `:81-98` (per product), `:644-658` (milestones — bytes read by `ExecutionMilestoneGate.cs:46` then re-read for hashing), `:728-745` (recursive enumeration of growing evidence/handoff/delta/decision dirs), `:669-689` (decision/recommendation pair re-read + re-parse)

### Current Behavior
SHA-256 over full bytes of every observed file, recomputed per observation (4–5×/cycle) though files change at most once per attempt.

### Remediation
First reduce observation count (PERF-03); then an (path, mtime, size)→hash cache inside the single production `RepositoryObserver` instance (`LoopRelayCompositionRoot.cs:385`), conservatively invalidated (any mtime/size change re-reads). Share milestone content between gate and hasher.

### Risks
Hash feeds freshness comparison — cache must be conservative; mtime-equal rewrites (rare, same-tick) would be missed, so include size and prefer re-hash on any doubt.

---

### Low

## `PERF-24 — Duplicate operational-delta file write and duplicate SelectChain per run`

### Classification
* Category: Unnecessary operation | Severity: Low | Confidence: High | Evidence status: Proven from code (per subsystem auditor)

### Locations
`DecisionSession.cs:908` and `:956` (identical content, same path, same transfer); `UnifiedCliRunner.cs:613,616` (two `SelectChain` calls per `RunWorkflowAsync`).

### Remediation
Drop the second write; reuse the line-613 result. Preserve: delta present on disk before the evolution operation reads it; identical chain object. Verify via existing transfer tests.

---

## `PERF-25 — Telemetry sinks re-run directory create, gitignore probe, schema ensure, and full JSONL sequence rescan per record`

### Classification
* Category: File I/O, Lifecycle | Severity: Low | Confidence: High | Evidence status: Proven from code

### Locations
`SqliteSessionTelemetrySink.cs:28-34,65-72`; `RotatingJsonlTelemetrySink.cs:29-51` (O(files-today) `FileInfo` probes per append).

### Remediation
Do directory/gitignore/schema work once per sink lifetime; remember the active JSONL file and roll forward. Preserve rotation thresholds and crash-safe append.

---

## `PERF-26 — Decision-session recovery machinery constructed per invocation, consumed only on first initialization`

### Classification
* Category: Memory allocation | Severity: Low | Confidence: High | Evidence status: Proven from code

### Locations
`CompositionPromptExecutionOwner.cs:1059-1107` (`recoveryMechanisms`, `RecoveryRuntime`, `LoopArtifacts`, router, dispatcher rebuilt every call; consumed only when `executeDecisionSession ??=` initializes).

### Remediation
Move construction inside `if (executeDecisionSession is null)`. Allocation-only waste; trivial fix.

---

## `PERF-27 — Grouped micro-findings: process-lifetime work re-done per call on approval/startup paths`

### Classification
* Category: CPU, Memory allocation, Serialization | Severity: Low | Confidence: High | Evidence status: Proven from code (items independently cited)

### Locations and Items
* `PermissionHandler.cs:19,23,60-91` — static `PermissionEvaluationFlow.Default` fully re-validated per Evaluate → validate once statically.
* `AgentRuntime.cs:56-58` + `CodexCompatibilityManifest.cs:64-95` — embedded manifest re-parsed per continuity negotiation (production passes no resolver); callers `DecisionSession.cs:384,453,528,792`, `CompositionPromptExecutionOwner.cs:818,1459` → static Lazy or construct the resolver once.
* `CodexAppServerSession.cs:647` + `CodexPermissionAdapter.cs:23` — same approval frame parsed up to 3×; `OperationPermissionHandler.cs:210-238` — 3 filesystem stats per path segment per approval, synchronously on the read pump (codex blocks awaiting the reply) → single `GetAttributes` per segment, pass parsed nodes through. The reparse-point security check itself must stay.
* `PermissionPolicyFactory.cs:16,23` — `MergeHardDeny` computed twice per merge → hoist (also under PERF-07).
* `LoopRelayCompositionRoot.cs:214,223,341,349` — `CliSettingsLoader.Load()` ×4 at startup (file read + full deserialize each) → load once, pass result.
* `RepositoryArtifactStore.cs:51-105` — repository root's physical target re-resolved per artifact op → cache the resolved root (invariant per process); the per-segment escape walk is a legitimate trust boundary and stays.

Each item is an independent one-to-five-line correction; none changes observable behavior.

---

## `PERF-28 — Dead retained state and unreachable projection code on production paths`

### Classification
* Category: Memory retention, Unnecessary operation | Severity: Low | Confidence: High | Evidence status: Proven from code (reachability lead-verified)

### Locations
* `WorkflowChaining.cs:268-283` — `WorkflowBoundaryEvidenceWriter` appends every boundary record to a process-lifetime in-memory list alongside the durable store; the composition property (`LoopRelayCompositionRoot.cs:179`) is never dereferenced (grep-verified: no consumers beyond construction/wiring).
* `LedgerEvidenceRetrieval.cs:18-26` + `CanonicalLedgerEvidenceProjection.cs:22-100` — no production callers (lead-verified: the retrieval class's only occurrences in `src/` are its own declaration and its use of the projection). Its fallback path would deserialize and re-hash every historical raw output per lookup — but it never executes.

### Remediation
Delete the in-memory list/property (tests can use the store); delete or deliberately wire the ledger-evidence retrieval pair. Compile + test sweep; check tests for consumers first.

---

### Measurement Required

## `PERF-29 — Unbounded workspace-lifetime data growth with no retention, compaction, or read-scoping backstop`

### Classification
* Category: Memory retention, Database I/O, Measurement gap
* Severity: Measurement Required
* Confidence: High (growth is structural); materiality of absolute sizes unmeasured
* Evidence status: Proven from code (growth mechanisms); Requires production telemetry (magnitudes)

### Locations
* Ledger tables (evidence `document_json` retains every raw codex output; agent_turns; gate evaluations; effect events) — writers in `TransitionRuntime.cs:160-178,302-317` et al.; no DELETE/compaction path exists outside explicit import/export.
* `.agents/evidence/**` trees re-enumerated and re-hashed per observation (PERF-23).
* `.LoopRelay/telemetry/*.jsonl` rotation exists, but the day's file set is re-probed per append (PERF-25).
* The repo's only built-in acknowledgment of growth is a warning knob: `policy.execution.operationalContextGrowthWarningStreak: 2` (consumed at `CompositionPromptExecutionOwner.cs:1105`) — it warns, never bounds.

### Current Behavior and Why It Matters
Every scale-sensitive finding above (PERF-02/05/08/11/14/23) has this as its multiplier. The system's cost trajectory over an epic is determined by these magnitudes, none of which are currently measured.

### Recommended Remediation
Measure first (§12-M6): row counts and DB size on mature workspaces, evidence-tree file counts, rollout-store size. Then decide whether read-scoping (already recommended) suffices or a retention/archival policy for raw-output documents is warranted. Do not add compaction speculatively — the ledger is evidence with recovery semantics.

### Verification
Production telemetry / fixture measurements per §12-M6; decision threshold in the plan.

---

## 6. Repeated-Work Matrix

| Repeated work | Occurrences | Current lifecycle | Validity lifecycle | Existing authority/result | Recommended correction |
| ------------- | ----------- | ----------------- | ------------------ | ------------------------- | ---------------------- |
| Schema shape verification (~180 probes) | Every store op (101+ sites) | Per-operation | Per-process per schema state | `schema_metadata` stamp (version + shape fingerprint) | Memoize keyed by (path, stamp) — PERF-01 |
| `CanonicalDataRepairSql` (5 UPDATEs + DROP) | Every `EnsureSchemaAsync` | Per-operation | Once, at migration | `structurallyComplete` branch condition | Migration branch only — PERF-01 |
| Legacy-resume probe/import | Every `EnsureSchemaAsync` | Per-operation | Once per legacy encounter | INSERT OR IGNORE idempotency | Migration branch only — PERF-01 |
| Full repository observation | 4–5×/cycle + 2 at startup | Per-call | Per-kernel-cycle | Cycle's own observation in `WorkflowControllerRequest.Observation` | Hand down; delete dead post-attempt observe — PERF-03 |
| DB file SHA-256 | 2× per verification ×4–5/cycle | Per-verification ×2 | ≤1× per verification | Inventory hash of the same file | Reuse inventory hash — PERF-04 |
| `git status --porcelain` | Per observation + per input-surface requirement per gate (`CompositionKernelOwners.cs:432`) + change detector | Per-call | Per gate evaluation / per cycle segment | One porcelain snapshot | Share snapshot per gate evaluation |
| Full-history snapshot load | Per observation; ~5×/attempt (state persists); per feature effect | Per-call | Keyed/current-run scope suffices | `run_id` PK; current-run ids | Keyed reads + scoped projection — PERF-02/05/13 |
| Workflow resolution | 2×/cycle (+1 mode resolve) | Per-call | Per-cycle | Chain runner's result | Pass result down — PERF-17 |
| Artifact content hashing | 4–5×/cycle per file | Per-observation | Per file change | (path, mtime, size) | Reduce observation count, then keyed cache — PERF-23 |
| Milestone file bytes | 2 reads/file/observation | Per-observation ×2 | 1 read | Gate's read content | Share content — PERF-23 |
| Attempts full-table read | 2–4×/cycle | Per-call | Zero (value in hand) | `CurrentAuthorization.Causality` | Reuse — PERF-11 |
| Effect plan hydration | Per pass/dependency/schedule | Per-call | Per coordination pass | Pass-scoped plan snapshot | Snapshot reuse + targeted SQL — PERF-06 |
| Effect item post-commit re-read | 2/effect step | Per-step | In-transaction knowledge suffices | Transaction's own writes | Return updated item — PERF-06 |
| Rendered-prompt table read-back | 1/prompt append | Per-attempt | Keyed/COUNT read | `rendered_prompt_id` | PERF-12 |
| `persistence_state` upsert | Every workflow-store open | Per-operation | Once per DB state change | Current row value | Conditional upsert — PERF-19 |
| Quota probe (process spawn) | Per turn | Per-turn | Minutes-scale | Last probe snapshot | Throttle/reuse session — PERF-09 |
| CODEX_HOME rollout scan | Per turn (path not cached by caller) | Per-turn | Per session | `cachedLogPath` parameter exists, unused | Filename-filtered lookup + cache — PERF-08 |
| Projection manifest load | 2×/ensure | Per-ensure | 1× | Loaded manifest object | Pass into upsert — PERF-18 |
| Manifest file write | Per ensure even when unchanged | Per-ensure | On change only | Record equality | Equality check — PERF-18 |
| Settings load + parse | 4× at startup | Startup ×4 | Once | First load's result | Load once — PERF-27 |
| Compatibility manifest parse | Per continuity negotiation | Per-negotiation | Per binary | Embedded resource | Static Lazy — PERF-27 |
| Evaluation-flow guard | Per permission Evaluate | Per-call | Process-lifetime | Static default flow | Validate once — PERF-27 |
| Read-guard shape classification | Per `ReadLatestAsync` / inspector call | Per-read | Per schema state | Stamp | `InspectStampedAsync` — PERF-15 |

---

## 7. Unnecessary-Operation Inventory

| Operation | Location | Presumed purpose | Actual consumer | Why unnecessary | Removal verification |
| --------- | -------- | ---------------- | --------------- | --------------- | -------------------- |
| Post-attempt full observation | `WorkflowChaining.cs:352-370` | Progression authority after attempt | `WorkflowControllerResult.ObservationAfter` — **no reader in src** (lead-verified) | Kernel re-observes independently at `OrchestrationKernel.cs:122`; stop decision computed before the observation | Grep + test sweep for `ObservationAfter`; multi-cycle behavior parity |
| `CanonicalDataRepairSql` on complete path | `LoopRelayWorkspaceDatabase.cs:152` | Repair legacy 'Blocked' vocabulary | None — v15-stamped DB cannot contain it | Obsolete safeguard at wrong lifecycle | Migration fixtures; healthy-DB trace shows no write txn |
| Second DB SHA-256 per verification | `WorkspaceStorageInspector.cs:28` | Byte-level identity | `byteHash` field | Inventory already hashed the same file | Compare emitted hashes are identical pre/post |
| Quota probe per turn | `SessionTelemetryRecorder.cs:43` | Fill 2 nullable telemetry columns | Telemetry rows only (usage-limit gating parses failure diagnostics instead) | Minutes-valid value fetched per turn via process spawn | Telemetry rows carry recent-enough percentages after throttling |
| Full rollout forensic read for a path | `SessionTelemetryRecorder.cs:44-49` | Rollout path for telemetry row | `exact.Location` only | Records/omissions/digest computed then discarded | Diagnosis-path fixtures unchanged; fast path returns same location |
| Duplicate operational-delta write | `DecisionSession.cs:956` | Delta on disk for evolution op | Same file already written at `:908` | Byte-identical duplicate | Transfer tests |
| Second `SelectChain` | `UnifiedCliRunner.cs:616` | Chain selection | Same args as `:613` | Pure function, identical result | Same chain object |
| In-memory boundary `records` list | `WorkflowChaining.cs:268-283` | Test observability | None in production (property never dereferenced) | Durable store holds the same data | Compile + test sweep |
| `LedgerEvidenceRetrieval` + `CanonicalLedgerEvidenceProjection` fallback | `LedgerEvidenceRetrieval.cs:18-26`; `CanonicalLedgerEvidenceProjection.cs:66-100` | Evidence lookup by hash | No production caller (lead-verified) | Unreachable; fallback would rescan all raw outputs if ever wired | Delete or wire deliberately; test sweep |
| Startup re-observation after no-op effect pass | `UnifiedCliRunner.cs:365-374` | Post-effect state | Workflow dispatch | `EffectWorkerResult` ignored; nothing changed when zero effects ran | Conditional re-observe; quiet-workspace startup shows one verification |
| Recovery machinery per invocation | `CompositionPromptExecutionOwner.cs:1059-1107` | Decision-session wiring | First initialization only | Rebuilt then discarded on subsequent calls | Behavior parity; allocation profile |

Non-runtime repo hygiene (not runtime work, noted for completeness): stale `Directory.Build.props` comment at `LoopRelay.Cli.csproj:8-9`; empty `src/LoopRelay.Plan.Cli`/`src/LoopRelay.Roadmap.Cli` shells; stale `CommandCenter.*` bin/obj ghosts and `.tmp/publish-roadmap-cli/`.

---

## 8. Redundant-Safeguard Inventory

| Safeguard | Failure prevented | Reachable? | Existing stronger mechanism | Current cost | Correct boundary | Recommendation |
| --------- | ----------------- | ---------- | --------------------------- | ------------ | ---------------- | -------------- |
| `CanonicalDataRepairSql` on complete branch | Legacy 'Blocked' vocabulary resurfacing | No (stamped-complete DB cannot contain it) | The migration itself + stamp | Write txn per store op | Migration only | **Obsolete safeguard** — move (PERF-01) |
| ~180-probe shape verification per open | Schema drift under a live process | Only via external mutation mid-run (undocumented scenario) | `schema_metadata` stamp written under the migration transaction | ~180 queries per op | First contact / stamp change | **Redundant safeguard** — memoize with stamp re-read (PERF-01) |
| Full shape classification on read guards | Reading a non-canonical DB | Yes (first contact) | Same stamp | ~180 queries per read | First contact | **Redundant safeguard** — stamped fast path (PERF-15) |
| `PRAGMA foreign_key_check` per observation | Referential corruption | Corruption is reachable in principle | Per-connection `PRAGMA foreign_keys=ON` prevents new violations; deep check exists in `storage verify` | Full FK walk ×4–5/cycle | Explicit verify / startup | **Misplaced safeguard** — tier (PERF-04) |
| Double DB hash per verification | Hash mismatch (none — same file) | N/A | The other hash | One full-file SHA-256 | Once | **Redundant safeguard** — delete (PERF-04) |
| Full persistence-tree hash per observation | Undetected file tampering between cycles | Codex could touch files; DB is the authority, tree is derived | Mutation-path guards; explicit verify | O(tree bytes) ×4–5/cycle | Explicit verify / cycle boundary | **Misplaced safeguard** — tier (PERF-04) |
| Rendered-prompt full read-back after append | Silent lost write | Extremely narrow (same connection just committed) | SQLite commit semantics; keyed re-read preserves the check | O(all prompts) per attempt | Keyed existence check | **Redundant implementation** of a legitimate check (PERF-12) |
| Post-commit effect item re-read | Torn lifecycle write | Same-transaction knowledge exists | Transaction atomicity | 1 open+ensure per step | In-transaction return | **Redundant safeguard** (PERF-06) |
| Evidence body SHA validation on path-only listings | Corrupt evidence served | Yes for content consumers | Keep for content reads | Hash of every row per listing | Content reads only | **Misplaced** for path consumers (PERF-14) |
| Freshness re-resolution at promotion (`SnapshotInputFreshnessValidator.cs:18-19`) | Concurrent input change mid-attempt | Yes | None equivalent | One observation per attempt | Exactly where it is | **Necessary boundary validation — keep** |
| Per-segment symlink-escape walk (`RepositoryArtifactStore.cs:51-105`) | Path escape via links appearing between calls | Yes (real trust boundary) | None | ~3 stats/segment/op | Per-operation is correct; root target is invariant | **Necessary defense in depth**; cache only the resolved root (PERF-27) |
| Reparse-point check per approval path arg | Symlinked write escape | Yes | None | 3 stats/segment on pump thread | Per-approval is correct | **Necessary defense in depth**; single-stat implementation (PERF-27) |
| Evaluation-flow guard per Evaluate | Mis-ordered evaluation pipeline | Only if flow becomes injected | Static readonly flow | Ordering scans per call | Static construction | **Cheap local assertion at wrong lifecycle** (PERF-27) |
| InvariantGuard after engine evaluation | Policy-violating allow | Yes — and currently the only component honoring configured hard-denies (see PERF-07) | None | Small | Where it is | **Necessary defense in depth — keep** |
| Workspace-identity validation per ensure | Identity swap under a process | External-mutation scenario only | Stamp + first-contact check | 1–2 queries per op | First contact / stamp change | **Redundant at per-op** — fold into memoized ensure (PERF-01) |

---

## 9. Architectural Root Causes

### AR-1 — Schema convergence and integrity enforcement at connection lifecycle
**Affected:** PERF-01, PERF-10, PERF-15, PERF-19; amplifies PERF-06, PERF-11, PERF-16, PERF-25.
**Description:** `LoopRelayWorkspaceDatabase` is the sole schema authority, but it exposes only a stateless per-connection `EnsureSchemaAsync`, so every store independently re-establishes a process-lifetime invariant. Local fixes (e.g., removing one store's ensure) would fragment the guarantee.
**Correct ownership:** the composition root (or a per-process database handle it owns) establishes schema state once per (process, database path, stamp) and hands stores connections that only set per-connection pragmas. Connection policy (pooling, WAL, busy_timeout) belongs to the same owner.
**Migration risk:** low — behavior on healthy databases is unchanged; legacy/migration branches keep their current semantics at first contact. The undocumented multi-process question (§15) is closed by a cheap stamp re-read per open.
**Verification:** statement-count traces; full migration/certification fixture sweep.

### AR-2 — Observation has no owner, no validity window, and no hand-down
**Affected:** PERF-03, PERF-04, PERF-05, PERF-17, PERF-23; PERF-11 is the same shape at attempt scope.
**Description:** "Current repository state" is re-derived from scratch by every layer that needs any part of it (kernel, chain runner, controller, product resolver, freshness validator, startup). Because the derivation is also maximally deep (PERF-04) and maximally wide (PERF-05), the multiplication is expensive twice over. Local caching would mask this and create staleness hazards; the correct fix is ownership: the kernel cycle observes once, passes the observation down, and only boundaries whose contract *is* re-observation (freshness validation, cycle start) derive fresh state.
**Correct ownership:** `OrchestrationKernel` owns the cycle's observation; `WorkflowControllerRequest` already carries it — extend the same hand-down to the product resolver; delete the consumerless post-attempt derivation.
**Migration risk:** moderate — every reuse decision must be justified against the freshness contract; the freshness validator must remain untouched.
**Verification:** ObserveAsync count per cycle; gate/decision-fact parity on identical state; freshness-conflict certification tests.

### AR-3 — Snapshot-shaped store APIs force global reconstruction for local questions
**Affected:** PERF-02, PERF-05, PERF-11, PERF-12, PERF-13, PERF-14, PERF-20; PERF-29 sets its growth rate.
**Description:** `CanonicalWorkflowPersistenceStore` and peers expose whole-ledger reads as the only read primitive, so every keyed question (one run row, one causality tuple, one ordinal, one product set, one path list) is answered by loading the full history. Because the ledger is append-only and the workspace outlives invocations, cost compounds quadratically.
**Correct correction:** add keyed/scoped read methods alongside the snapshot reads (which remain for status/recovery/import); consumers switch call-by-call.
**Migration risk:** low per call site — each switch is parity-testable in isolation.
**Verification:** per-operation row-count traces; parity tests per converted call site.

### AR-4 — Maximum-generality durable-effect machinery reused for known-target writes
**Affected:** PERF-06, PERF-22.
**Description:** The effect worker's design point is crash-safe settlement of arbitrary unsettled backlogs; evidence writes with a known intent identity pay the whole generality (global scan, in-memory filtering, whole-plan hydration, post-commit re-reads).
**Correct correction:** targeted SQL access inside the existing store (filter pushdown, single-JOIN hydration, in-transaction returns) — not a second write path.
**Migration risk:** low-moderate — the durable-barrier and lease semantics are subtle; cache strictly within a pass.

### AR-5 — No owner for connection-lifetime and journal policy
**Affected:** PERF-10; multiplies PERF-01/06/16/19/25.
**Description:** `Pooling=false` and default DELETE journal mode were set in one place and inherited everywhere; no component owns the decision, so no component revisits it or documents why the import/export file-swap flows require it (if they do).
**Correct correction:** same owner as AR-1; decide pooling/WAL deliberately with the export/import flows in the test matrix.

---

## 10. Quick Deletions and Low-Risk Corrections

Each item is high-confidence, small, correctness-preserving, and independently verifiable:

1. **Fix the permission engine's policy** — `PermissionEvaluatorEngine.cs:30`: call `EvaluateSingleCore` instead of `EvaluateSingle`. One line. *Note: deliberate behavior change for operators with custom policies (PERF-07); ship with the unit test and a release note.*
2. **Delete the second DB hash** — reuse the inventory's hash for `byteHash` (`WorkspaceStorageInspector.cs:28`). Output-identical.
3. **Delete the consumerless post-attempt observation** — `WorkflowChaining.cs:354` + the `ObservationAfter` field (`:106`), after a test sweep; or make the kernel consume it at `OrchestrationKernel.cs:122` (equal savings, zero deletion risk).
4. **Confine `CanonicalDataRepairSql` + legacy-resume import to the migration branch** — `LoopRelayWorkspaceDatabase.cs:145-154`. Deletes a write transaction from every store operation; migration fixtures prove the branch condition already guarantees convergence.
5. **Drop the duplicate delta write and duplicate `SelectChain`** — `DecisionSession.cs:956`; `UnifiedCliRunner.cs:616`.
6. **Conditional manifest save** — skip `SaveAsync` when the upserted manifest equals the loaded one (`ProjectionManifestStore.cs:56-60`); keep exception-path writes.
7. **Conditional `persistence_state` upsert** — add `WHERE value <> excluded.value` (`CanonicalWorkflowPersistenceStore.cs:1509-1515`); tests at `:46-52,188` still pass.
8. **Hoist `MergeHardDeny`** (`PermissionPolicyFactory.cs:16,23`); **validate the evaluation flow once** (`PermissionHandler.cs:19-23`); **cache the embedded compatibility manifest** (static Lazy, `CodexCompatibilityManifest.LoadEmbedded`); **load settings once** at composition (`LoopRelayCompositionRoot.cs:214-349`); **convert shape-requirement computed properties to `static readonly`** (`LoopRelayWorkspaceDatabase.cs:1250-1415`).
9. **Move decision-machinery construction under its first-init guard** — `CompositionPromptExecutionOwner.cs:1059-1107`.
10. **Delete dead code pending test sweep** — boundary `records` list (`WorkflowChaining.cs:268-283`); `LedgerEvidenceRetrieval` + `CanonicalLedgerEvidenceProjection` (or wire the intended consumer deliberately).
11. **Re-observe at startup only when the effect worker reports work** — `UnifiedCliRunner.cs:365-374`.

Not listed as quick because they are load-bearing despite being small: ensure memoization (PERF-01b — needs the multi-process stance from §15), keyed run reads (PERF-02 — needs a parity test), WAL/pooling (PERF-10 — needs the export/import matrix).

---

## 11. Lifecycle Corrections

| Work | Current lifecycle | Correct lifecycle | Reason | Affected paths | Expected benefit |
| ---- | ----------------- | ----------------- | ------ | -------------- | ---------------- |
| Schema verification | Per-operation | Per-process per (path, stamp) | Schema is stable within a process; stamp is the authority | All persistence | Removes ~180 probes/op (PERF-01) |
| Repair SQL + legacy import | Per-operation | Migration only | One-time convergence companion | All persistence | Removes a write txn/op (PERF-01) |
| Repository observation | Per-consumer (4–5×/cycle) | Per-kernel-cycle, handed down | State changes at attempt/effect boundaries, not between same-instant consumers | Kernel/chain/controller/resolver | ~2–3 fewer observations/cycle (PERF-03) |
| Deep storage verification | Per-observation | Explicit verify + startup (+ cycle boundary if desired) | Deep integrity is an operator concern; routine paths need health, not forensics | All observations | Removes hash sweep + FK walk from hot path (PERF-04) |
| Quota probe | Per-turn | Minutes-scale TTL / per-cycle | Quota validity ≫ turn cadence | Every turn | One fewer process spawn/turn (PERF-09) |
| Rollout path resolution | Per-turn | Per-session (cache), filename-first | Path is stable per thread | Every turn | Removes CODEX_HOME sweep (PERF-08) |
| Causality derivation | Per-planner-call (from DB) | Per-attempt (in-hand) | Value constructed at dispatch and immutable | 2–4×/cycle | Removes growing scan (PERF-11) |
| Effect-plan hydration | Per pass/dependency | Per coordination pass | Plan immutable within a pass; child state re-read between passes | Effect phase | Removes N+1 (PERF-06) |
| Telemetry sink init + JSONL scan | Per-record | Per-sink-lifetime / rolling pointer | Directory and schema state stable | Every turn | Removes per-record probes (PERF-25) |
| Settings load | ×4 at startup | Once | Immutable input | Startup | Minor (PERF-27) |
| Evaluation-flow guard / manifest parse / repo-root resolve | Per-call | Static / per-process | Process-lifetime invariants | Approval, negotiation, artifact ops | Minor each (PERF-27) |
| Manifest write | Per-ensure | On change | Content equality decidable | Decision sessions | Removes steady-state writes + cache churn (PERF-18) |

---

## 12. Measurement Plan

**M1 — Persistence statement count and wall time per attempt (PERF-01/10/19, gate for PERF-06).**
Entry: one `run` invocation on a mid-life fixture workspace (see M6 for fixture). Instrument: SQLite command tracing (connection `Trace`/interceptor) counting commands, transactions, and journal file creates per attempt; warm process. Baseline: current branch. Distortion: first-attempt migration work — discard attempt 1. Expected signal: hundreds-to-thousands of statements per attempt dominated by ensure probes; ≥2 write transactions per store op. Decision threshold: if ensure-attributable statements exceed ~30% of per-attempt statements (they will, structurally), implement PERF-01; re-measure, then benchmark WAL+pooling on the residual and adopt if persistence wall time drops measurably under the same trace.

**M2 — `PersistStateAsync` cost vs. history size (PERF-02/05).**
Entry: direct store harness over fixture DBs at N = 10² / 10³ / 10⁴ transition-evidence rows (generated by replaying attempt writes). Metric: wall time + rows read per `PersistStateAsync` and per `ProjectAsync`. Expected: linear growth in N; keyed read flat. Threshold: any supra-constant growth on the persist path confirms remediation (structural proof already exists; this sizes it).

**M3 — Observation census and cost (PERF-03/04/23).**
Entry: multi-cycle `run` on the M6 fixture with a counter on `RepositoryObserver.ObserveAsync` and a stopwatch per phase (verification / projection / hashing / git). Concurrency: n/a (single loop). Expected: 4–5 observations/cycle; verification+hashing the dominant share and growing with tree size. Threshold: post-fix target is 2 observations/cycle (cycle + freshness) with deep verification absent from both.

**M4 — Effect settlement query count (PERF-06).**
Entry: one Execute transition with ~10 evidence candidates on the fixture. Metric: SQLite trace grouped by store method; count connection opens and plan hydrations per settled effect. Expected: 4–6 opens and ≥2 full-plan hydrations per effect currently. Threshold: targeted-access rework should bring opens per effect to ≤2 and plan hydrations to 1 per pass.

**M5 — Per-turn telemetry overhead (PERF-08/09).**
Entry: a session of ≥10 turns with telemetry on, against a codex home populated with ≥1 GB of rollouts (copy of a real one or synthetic). Metric: process spawns per turn (ETW/Process Monitor), bytes read from CODEX_HOME per turn, turn wall-clock with/without `sessionTelemetry`. Expected: 1 spawn + full-store read per turn currently. Threshold: any per-turn latency delta over ~5% of mean turn time justifies the throttle + filename-filtered lookup regardless of absolute size.

**M6 — Workspace growth magnitudes (PERF-29; input to M1–M4).**
Collect from real epic workspaces (the repo dogfoods itself — `.LoopRelay/` and `.agents/` in working copies): DB file size, per-table row counts (`canonical_transition_evidence`, `agent_turns`, `canonical_transition_runs`, effect tables), evidence-tree file count/bytes, telemetry JSONL volume. If no aged workspace exists, generate one by scripted replay. Decision threshold: if evidence `document_json` dominates DB size (likely), prioritize read-scoping (already recommended) and only then evaluate retention/archival for raw outputs as a deliberate semantic decision.

---

## 13. Prioritized Remediation Sequence

Ordered by dependency and leverage, not by finding ID. Steps 1–3 are independent of each other and can proceed in parallel.

1. **PERF-07 permission-policy fix** (+ PERF-27 permission micro-items). No prerequisites. High value (policy fidelity), trivial risk with its unit test. Gate: permission suites + new custom-policy test. *Deliberate behavior change — release-note it.*
2. **PERF-01: confine repair SQL to migration; memoize ensure per (path, stamp); static requirement lists.** Prerequisite: decide the multi-process stance (§15 Q1) — the stamp-re-read variant is safe under either answer. Highest structural leverage: shrinks every other persistence cost and de-serializes the write lock. Gate: M1 trace + full migration/certification fixtures. Makes parts of PERF-15/19/25 cheaper or moot — re-check them after.
3. **Quick deletions (§10 items 2–6, 9–11).** Independent. Gate: compile + targeted tests each.
4. **PERF-03: delete/consume the post-attempt observation; hand the cycle observation to the product resolver; conditional startup re-observe.** Prerequisite: none, but land after the `ObservationAfter` test sweep. Gate: M3 census (target 2/cycle) + freshness-conflict and gate-outcome parity.
5. **PERF-04: single DB hash; verification tiering (light health per observation, deep verify at explicit command/startup).** Prerequisite: step 4 (fewer call sites to tier). Gate: storage certification + M3 phase timings. Coordinate with step 7's WAL side-files if reordered.
6. **AR-3 keyed reads: PERF-02 (`ReadTransitionRunAsync`), PERF-11 (reuse in-hand causality), PERF-12 (COUNT/keyed read-back), PERF-13 (products-only), PERF-14 (path-only listing).** Independent, parity-testable per call site. Gate: M2 + per-site parity tests. PERF-05 (scoped projection) last within this step — it has the widest consumer surface; use M6 data to size its urgency.
7. **PERF-10: WAL + busy_timeout + pooling decision.** Prerequisite: M1 residual measurement after step 2; export/import flows in the test matrix (WAL checkpoint before file swap; inventory hashing accounts for `-wal`/`-shm`). Gate: export/import round-trip + M1 re-run.
8. **PERF-06/22: effect-pipeline targeted access** (filter pushdown, single-JOIN hydration, in-transaction returns, pass-scoped plan snapshot; then batch completion markers). Prerequisite: step 2 (isolates the win), M4 baseline. Gate: effect-ordering certification + M4 target.
9. **PERF-08/09: telemetry — cache rollout path per session + filename-first lookup; throttle quota probe.** Prerequisite: M5 baseline. Gate: M5 targets; diagnosis-path fixtures unchanged.
10. **PERF-16 async observer contracts; PERF-17 pass resolution down; PERF-18 manifest equality; PERF-20 scope_id column; PERF-21 single-read validation; PERF-23 hash cache (only if M3 still shows hashing dominance after steps 4–5); PERF-25 sink lifetime init.** Each small and independent; order by residual M-plan signals.
11. **PERF-29: retention decision for raw-output documents** — only after M6, and only as a deliberate semantic decision (ledger is evidence).

Steps 2, 4, 5, and 6 each may make later micro-optimizations unnecessary; re-run the relevant measurement before starting steps 8+.

---

## 14. Reviewed Areas with No Material Findings

* **`FileSystemArtifactStore` (Core)** — signature-keyed byte and deserialization caches, atomic temp-file+ReplaceFile writes, bounded retries; exemplary. (Its cache is churned by PERF-18's mtime writes — fixed there.)
* **Codex stream processing** — `CodexAppServerSession` read pump and `CodexEventTurnBoundaryDetector`: exactly one JSON parse per stdout line, incremental turn detection, no transcript re-parsing; single-writer stdin loop; bounded 8 KB stderr tail (`AgentProcess.cs:11,105-115`); bounded exit waits.
* **Prompt rendering and transition tables** — `CompositionTransitionOwners.cs` static tables; `UnifiedPromptRenderer` allocations are the product; `CanonicalPromptComposer`.
* **Startup composition** — catalog/chain/policy built and validated once (`LoopRelayCompositionRoot.cs:387-402`); `CodexCompatibilityIdentityProbe` correctly `Lazy` once-per-process; `CliSurface` parsing trivial (settings ×4 noted in PERF-27).
* **`CommitTransitionAsync`** — products, gate, run/attempt completion, projections, and effect-intent enqueue in one IMMEDIATE transaction: the correct durability boundary, deliberately expensive.
* **Recovery, import, and storage commands** — `RecoveryRuntime`, `NativeForkRecoveryMechanism`, `RecoveryJournal/Planner/Serializer`, `CanonicalImportGateway`, `CanonicalStorageExportCodec`, `WorkspaceStorageApplicationService`: failure/admin-only lifecycles with proportionate cost.
* **Permissions data path** — parser/canonicalizer/fingerprint linear per cache miss; `InMemoryPermissionCache` unbounded but tiny-entried (noted, not material at CLI lifetime); `InvariantGuard` cheap and load-bearing.
* **Completion certification** — per-epic lifecycle, cost dominated by 2–3 deliberate agent prompts; single-pass parsers; per-candidate one-shot semantic confirmation already ledger-deduplicated (`NonImplementationSemanticConfirmer.cs:74-82`).
* **Projections** — not rebuilt from history: `ProjectContextProjectionService` regenerates only when stale/invalid; `ProjectionFreshnessEvaluator` pure (manifest write issue is PERF-18).
* **Diagnostics** — `InputWaitTurnTracker` bounded `PeriodicTimer` canceled on first output; `UsageLimitDetector` compiled regexes on a failure-only path; `TaskDelayScheduler` trivial.
* **Status path** — full re-hash and full-table reads acceptable at explicit per-invocation lifecycle (`CanonicalStatusSnapshotComposer`, `ReadReceiptStaleness`), apart from PERF-20's scan shape.
* **Shutdown** — single CTS; disposal chain (prompt executor → session registry → provider) bounded; no polling loops found in scope.
* **Kernel decision append** — one insert per cycle; root-run coordinator reads a table bounded at one row per invocation.

---

## 15. Evidence Gaps and Open Questions

1. **Is concurrent multi-process access to one workspace database a supported scenario?** No code comment or doc states it; no busy_timeout suggests it is not. Determines how aggressive PERF-01 memoization may be (the stamp-re-read variant is safe either way) and whether PERF-10 pooling needs cross-process consideration.
2. **Is `Pooling = false` deliberate for the import/export DB-file-swap flows?** Undocumented. PERF-10's remediation must scope pooling around those flows if so.
3. **Production magnitudes** — DB sizes, `canonical_transition_evidence`/`agent_turns` row counts, evidence-tree file counts on mature workspaces (M6). All Critical/High severities here rest on structural multiplication; magnitudes size the payoff, not the validity.
4. **Store operations per attempt in a representative run** — the 50–100 figure is a structural count of call sites × per-turn multipliers, not a trace (M1 resolves).
5. **Test-suite consumers of deletion targets** — `ObservationAfter`, `WorkflowBoundaryEvidenceWriter.Records`, `LedgerEvidenceRetrieval`: verified consumer-free in `src/` only; tests were out of audit scope and must be swept before deletion.
6. **`OperationalRuntimeComposition` usage-limit wait/retry internals** (`OperationalRuntimeComposition.cs:44,61`) and `CodexUsageProbe`'s documented second consumer ("gates the loop" per its doc comment) were not fully traced; if a loop gate also calls `QueryAsync` per iteration, PERF-09's frequency rises.
7. **Wall-clock share of observation vs. codex dispatch per cycle** — unknown; on attempt cycles provider latency may dominate, but gate-blocked/no-op/status cycles pay observation cost undiluted (M3).
8. **`ScanUnsettledAsync` global scope** — whether workspace-wide (rather than per-transition) unsettled scanning inside the coordinator is intentional cross-run progress or accidental breadth; affects PERF-06's filter-pushdown shape.
9. **Robustness note (out of audit scope, flagged in passing):** `CodexCompatibilityIdentityProbe.Run` reads stdout to EOF before stderr (`CodexCompatibilityIdentityProbe.cs:88-89`); a stderr-heavy child could stall until the 30 s bound.
