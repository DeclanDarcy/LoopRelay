# LoopRelay.Orchestration.Primitives Performance Remediation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make observation a once-per-boundary, health-tiered operation; give the persistence stores keyed/scoped reads so hot-path cost stops growing with workspace age; de-amplify the effect pipeline.

**Architecture:** This project owns two of the audit's three architectural root causes: AR-2 (observation has no owner/hand-down) and AR-3 (snapshot-shaped store APIs). Tasks are ordered so deletions land first, then keyed reads (independently parity-testable per call site), then the wider read-model scoping. The per-open schema-ensure cost this project's stores pay is fixed centrally in LoopRelay.Core (its Task 1) — do not duplicate that work here.

**Tech Stack:** .NET 10, Microsoft.Data.Sqlite, xUnit (`tests/LoopRelay.Orchestration.Primitives.Tests`), certification fixtures (`src/LoopRelay.Certification`, operator-run).

**Source findings:** PERF-02, PERF-03 (this project's share), PERF-04, PERF-05, PERF-06, PERF-12, PERF-16 (journal part), PERF-17, PERF-19, PERF-20, PERF-23, PERF-28 (this project's share) in [production-code-performance-audit.md](../../production-code-performance-audit.md). Line references are against commit `0de6b5a8`.

## Global Constraints

- **Freshness contract is sacred:** `SnapshotInputFreshnessValidator` (`Runtime/SnapshotInputFreshnessValidator.cs:18-19`) MUST keep performing its own fresh observation at promotion time. No task below may reuse an older observation there.
- A fresh observation at each kernel-cycle boundary stays (codex mutates the tree between cycles).
- Ledger append-only semantics, `CommitTransitionAsync`'s single-transaction durability boundary, lease CAS, row-version conflict detection, idempotency keys, effect lifecycle ordering, and the durable-barrier dependency check (`EffectWorker.cs:261-275`) are all untouchable.
- Corruption fail-closed: verification tiering may defer *deep* checks, never mutation guards.
- Tests referencing deleted members must be swept **before** deletion (audit §15 Q5: `ObservationAfter`, `WorkflowBoundaryEvidenceWriter.Records` were verified consumer-free in `src/` only).

## Cross-Project Dependencies

- LoopRelay.Core Task 1 (PERF-01) should land first — it changes the per-open cost baseline every measurement here is judged against.
- Task 2's product-resolver hand-down has its adapter in LoopRelay.Cli (`CompositionKernelOwners.cs:104-148`); the interface change originates here, the Cli plan (Task CLI-1) consumes it.
- Task 3 must coordinate with LoopRelay.Core Task 2 (WAL side files appear in the persistence inventory).
- Task 5's targeted-access store methods are consumed by LoopRelay.Cli Task CLI-8 (completion marker batching).

---

### Task 1: Delete the consumerless post-attempt observation — PERF-03 (part 1)

**Files:**
- Modify: `Chaining/WorkflowChaining.cs:352-370` (the `ObserveAsync` at `:354` and the positional `observed` argument at `:369`), `:106` (`ObservationAfter` record field)
- Test sweep: `grep -rn "ObservationAfter" tests/` first; update/remove any test consumers
- Test: `tests/LoopRelay.Orchestration.Primitives.Tests`

**Current behavior (verified):** the controller re-observes after every attempt solely to populate `WorkflowControllerResult.ObservationAfter`; the stop decision (`:355-363`) is computed without it, and the identifier's only occurrence in `src/` is its declaration. The kernel performs its own fresh `ObserveAsync` at `OrchestrationKernel.cs:122`.

**Change contract (choose exactly one):**
- **Option A (preferred, pure deletion):** remove the `ObserveAsync` call, the `observed` local, and the `ObservationAfter` field from `WorkflowControllerResult`. One full observation per attempt disappears.
- **Option B (zero-deletion fallback, equal savings):** keep the field, and make `OrchestrationKernel.RunAsync:122` consume `last.ControllerResult.ObservationAfter` instead of re-observing when `StopReason == TransitionCompleted` (ControllerResult is non-null in that case). Use B only if the test sweep finds consumers that are expensive to migrate.

Either way the kernel must still act on state observed **after** effect coordination — Option A satisfies this because the kernel's own `:122` observation happens after `CoordinateAsync` completed inside the controller; verify that ordering while reading.

- [ ] **Step 1: Sweep tests** for `ObservationAfter`; list consumers; pick Option A or B accordingly and record the choice under `## Decisions` at the bottom of this file.
- [ ] **Step 2: Write the behavioral test** (before changing code): a multi-cycle kernel run over an in-memory/fixture setup asserting the sequence of stop reasons and successor selections for a scripted set of attempt outcomes — this is the parity harness. If such a kernel-loop test already exists, extend it with an observation-count assertion (instrument `IRepositoryObservations` with a counting decorator in the test).
- [ ] **Step 3: Implement the chosen option.** Expected observation count per completed-attempt cycle drops by exactly 1 in the counting decorator.
- [ ] **Step 4: Run the Orchestration suite + Cli suite** (the composition wires these types): `dotnet test tests/LoopRelay.Orchestration.Primitives.Tests tests/LoopRelay.Cli.Tests`.
- [ ] **Step 5: Commit** — `perf(orchestration): remove consumerless post-attempt observation`

### Task 2: Hand the cycle's observation to the product resolver — PERF-03 (part 2)

**Files:**
- Modify: `Runtime/TransitionRuntime.cs:58-59` (attempt-start `IProductResolver.ResolveAsync` call) and the `IProductResolver` contract it consumes (locate the interface first: `grep -rn "IProductResolver" src/LoopRelay.Orchestration.Primitives/`)
- Modify (consumer, coordinated with the Cli plan Task CLI-1): `src/LoopRelay.Cli/Services/Cli/CompositionKernelOwners.cs:104-148` (`RepositoryObservationProductResolver`)
- Test: `tests/LoopRelay.Orchestration.Primitives.Tests`

**Change contract:** the controller already holds the cycle's observation (`WorkflowControllerRequest.Observation`, `WorkflowChaining.cs:96`). Thread it to the attempt-start product resolution so the resolver answers "which required input products are usable" from the ambient observation instead of rebuilding a global one. Mechanism options (pick after reading the interface): add an overload `ResolveAsync(RepositoryObservation ambient, ...)`, or construct a request-scoped resolver adapter that closes over the observation. The **promotion-time** freshness resolution stays on the fresh-observing path (Global Constraint 1) — this widens, never narrows, the concurrent-change detection window.

- [ ] **Step 1: Read `TransitionRuntime.RunAsync` fully** plus the resolver interface and the Cli adapter; map every `IProductResolver` call site (`grep -rn "ResolveAsync" src/ | grep -i product`).
- [ ] **Step 2: Write parity test:** for a fixed fixture state, product-resolution results from the ambient observation equal results from a fresh observation taken at the same instant (same products, same usability verdicts).
- [ ] **Step 3: Implement here (interface + runtime call);** the Cli adapter change lands with the Cli plan in the same PR or a stacked one — the build gate is the shared solution build.
- [ ] **Step 4: Run freshness-conflict certification/product-gate tests.** Expected: unchanged outcomes.
- [ ] **Step 5: Commit** — `perf(orchestration): resolve attempt-start products from the cycle observation`

### Task 3: Single DB hash + verification tiering in observation — PERF-04

**Files:**
- Modify: `Storage/WorkspaceStorageInspector.cs:10-105` (verified: `:28` hashes the DB that `InventoryAsync` at `:94-100` already hashed; `:33-34` full classification + `ForeignKeyViolationsAsync`; `:44-47` interrupted scans)
- Modify: `Resolution/RepositoryObserver.cs:54` (which verification tier observation requests) and the verifier adapter between them (`WorkspaceStorageVerifierAdapter` — locate and read it)
- Test: `tests/LoopRelay.Orchestration.Primitives.Tests` (storage suites)

**Change contract:**
1. **Deletion (do first, independently):** compute `byteHash` by looking up the database's entry in the already-built `inventory` instead of re-hashing (`:28`). Output-identical — the inventory hashed the same file.
2. **Tiering:** introduce a light verification mode for routine observation — file exists, stamped schema readable (use LoopRelay.Core's `InspectStampedAsync` from its Task 3), interrupted markers/rows, journal-artifact presence — and keep the deep mode (full inventory hashing, `foreign_key_check`, full classification) for explicit `storage verify`, startup, and (optionally) kernel-cycle boundaries. `StorageHealth` semantics for the storage *commands* must be produced by the deep mode exactly as today.
3. Coordinate with LoopRelay.Core Task 2: once WAL lands, `-wal`/`-shm` side files appear in the inventory — decide (checkpoint-before-verify vs. deliberate inclusion) and test `storage verify` stability.

- [ ] **Step 1: Land the byteHash deletion** with a test asserting the emitted `bytes-sha256` note (`:84`) is unchanged for a fixture DB. Commit separately — `perf(orchestration): hash workspace database once per verification`.
- [ ] **Step 2: Read all `VerifyAsync` consumers** (`grep -rn "VerifyAsync\|IWorkspaceStorageInspector" src/`) and classify each as routine-observation vs. explicit-command.
- [ ] **Step 3: Write tests for the tier split:** light mode detects a missing DB, an interrupted marker, and a malformed stamp (each → non-Healthy); deep mode output unchanged vs. today on healthy/corrupt fixtures.
- [ ] **Step 4: Implement tiering; wire routine observation to the light tier.** Corrupt-DB mutation refusal must still trigger — confirm which check the mutation guards rely on and keep it in the light tier.
- [ ] **Step 5: Run storage certification suites; commit** — `perf(orchestration): tier storage verification (light health per observation, deep on demand)`

### Task 4: Keyed transition-run read for state persistence — PERF-02

**Files:**
- Modify: `Persistence/CanonicalTransitionPersistenceStores.cs:152-171` (`ExistingOrFallbackAsync` — verified callers `PersistStateAsync:41`, `PersistCompletedAsync:58`)
- Modify: `Persistence/CanonicalWorkflowPersistenceStore.cs` — add `ReadTransitionRunAsync(Guid runId, CancellationToken)` (single `SELECT ... WHERE run_id = @runId` on a read-only connection; read the existing row-mapping code at `:1317-1377` and reuse the row mapper)
- Test: `tests/LoopRelay.Orchestration.Primitives.Tests`

**Change contract:** `ExistingOrFallbackAsync` currently loads the full nine-table snapshot (including every historical `document_json`) ~5×/attempt to `FirstOrDefault` one run row. Replace with the keyed read; preserve fallback-record construction when the row is absent, byte-for-byte. `LoadRecoveryAsync` (`:74-138`, failure-only) is out of scope here — leave it.

- [ ] **Step 1: Write the parity test:** seed a fixture with multiple runs/evidence; assert `PersistStateAsync`'s written row via the keyed path equals the row written via the current snapshot path for (a) existing run, (b) missing run (fallback). Use two store instances against copies of the same DB.
- [ ] **Step 2: Verify it fails** meaningfully (keyed method doesn't exist yet → compile-fail counts; then behavioral parity once implemented).
- [ ] **Step 3: Implement `ReadTransitionRunAsync` + switch `ExistingOrFallbackAsync`.**
- [ ] **Step 4: Run transition certification suites; query-count trace** (audit M2): rows read per `PersistStateAsync` must be O(1).
- [ ] **Step 5: Commit** — `perf(orchestration): keyed transition-run read in state persistence`

### Task 5: Effect pipeline targeted access — PERF-06

**Files:**
- Modify: `Persistence/CanonicalEffectWorkStore.cs:116-147` (scan N+1), `:155-178` (plan N+1), `:180-201` (`ReadRunAsync` connection-per-row), `:331,385` (post-commit re-reads), `:444-512` (full event-history per item read)
- Modify: `Effects/EffectWorker.cs:32-43` (in-memory `only:` filter after full hydration), `:248-278` (per-dependency re-reads)
- Modify: `Runtime/TransitionEffectCoordinator.cs:124-129` (full plan re-read per pass), `:77-83` (reconciler plan read)
- Modify: `Effects/DurableFilesystemWriteEffectPlanner.cs:24-34` (`ScheduleAsync` whole-plan read for one Started parent) — confirm actual path with `Glob **/DurableFilesystemWriteEffectPlanner.cs` first; the audit places it in this project or Cli
- Test: `tests/LoopRelay.Orchestration.Primitives.Tests` (effect ordering/settlement suites)

**Change contract (four independent sub-changes, in this order):**
1. **Filter pushdown:** `ScanUnsettledAsync` accepts the `only:` intent-identity filter as SQL (`WHERE effect_intent_id = @id` / set membership) so a candidate write hydrates one intent, not up to 128.
2. **In-transaction returns:** the lifecycle-append and receipt-record methods return the updated item from the same transaction (state/rowversion are known) instead of the post-commit full re-reads at `:331,385`.
3. **Single-JOIN plan hydration:** `ReadPlanAsync` becomes one query joining intents/receipts/events (or three set-based queries), replacing 1+~3N; add an id/state-only projection for dependency checks so full payloads (`definition_json` embeds file contents) load only where consumed.
4. **Pass-scoped plan snapshot:** the coordinator hydrates the plan once per pass and shares it with `RunOnceAsync` and the reconciler; the durable-barrier child-effect check must still read fresh child state **between** passes — cache strictly within a pass.

- [ ] **Step 1: Read all five files end-to-end** before touching anything — the lease/barrier semantics here are the subtlest in the repo (see `EffectWorker.cs:261-275` comment).
- [ ] **Step 2: Baseline query-count trace** (audit M4): opens + hydrations per settled effect on a 10-candidate Execute-transition fixture. Record numbers in `## Decisions`.
- [ ] **Step 3: For each sub-change: write/extend a settlement-order test → implement → run the effect certification suite → commit.** Suggested messages: `perf(orchestration): push effect scan filter into SQL`, `perf(orchestration): return settled effect state in-transaction`, `perf(orchestration): single-query effect plan hydration`, `perf(orchestration): pass-scoped effect plan snapshot`.
- [ ] **Step 4: Re-run the M4 trace.** Target: ≤2 opens per settled effect, 1 plan hydration per pass.

### Task 6: Keyed rendered-prompt read-back — PERF-12

**Files:**
- Modify: `Persistence/CanonicalTransitionPersistenceStores.cs:450-486` (verified: `AppendAsync` re-reads the whole prompt table to `FindIndex` the new row), `Persistence/CanonicalWorkflowPersistenceStore.cs:749-797` (full-text read used by it)
- Test: `tests/LoopRelay.Orchestration.Primitives.Tests`

**Change contract:** replace the full read-back with (a) ordinal via `SELECT COUNT(*) FROM canonical_rendered_prompts` in the same connection scope as the insert (or `WHERE rendered_prompt_id <= @id` ordering rule — read the ordinal's consumers first to pick the semantics that matches: `grep -rn "Ordinal\|index + 1" src/ | grep -i prompt`), and (b) existence via `SELECT 1 ... WHERE rendered_prompt_id = @id`, preserving the "not readable after append" `InvalidOperationException`. The in-memory `appended` cache behavior is unchanged.

- [ ] **Step 1: Determine what consumes the ordinal** (`persisted.Ordinal`-equivalent, the `index + 1` at `:482`) and pin its semantics with a test.
- [ ] **Step 2: Write parity test:** ordinals produced for a sequence of appends match current behavior; append followed by simulated read-back failure still throws.
- [ ] **Step 3: Implement; run suites; commit** — `perf(orchestration): keyed rendered-prompt read-back`

### Task 7: Scoped observation read model — PERF-05

**Files:**
- Modify: `Persistence/CanonicalWorkflowPersistenceStore.cs` read sites `:1179,1270,1317-1377,1418,1462,1576,1607,1707,1742,1780,1822` (no WHERE/LIMIT today) — via new scoped variants, not in-place edits
- Modify: `Persistence/CanonicalPersistenceReadModel.cs:49-77` (`ProjectAsync`) to consume the scoped reads
- Consumers to keep working unchanged: `Resolution/RepositoryObserver.cs:68-71,122-158,181-203`, `Resolution/WorkflowResolver.cs:186-189`, `Chaining/OrchestrationKernel.cs:159-171`
- Test: `tests/LoopRelay.Orchestration.Primitives.Tests`

**Change contract:** split "current state" from "full history": transition runs → latest-per-(workflow, transition) via a window/grouped query **plus** any rows needed by `WorkflowResolver`'s completed-run detection (read `:186-189` first — if it scans all historical runs for Completed detection, provide a dedicated `HasCompletedRun(workflow, transition)` style read instead of full rows); evidence/gate/effect/warning tables → current-run filters. Full-history snapshot reads remain available for status/recovery/import consumers. This is the widest-surface task in the plan — land it **last**, after audit measurement M6 sizes its urgency, and gate on parity of every observation-derived decision.

- [ ] **Step 1: Enumerate every field of `RepositoryObservation` and its source table + the consumer that reads it** (a table in `## Decisions`). This inventory is the safety net.
- [ ] **Step 2: Build an aged fixture** (scripted replay producing ≥10³ evidence rows) and snapshot the full decision surface: resolver outputs, gate verdicts, decision-fact JSON (`OrchestrationKernel.cs:159-171`) for a scripted cycle.
- [ ] **Step 3: Implement scoped reads behind the projection; assert the decision-surface snapshot is byte-identical.** The decision-fact JSON includes observation contents — if scoping changes its bytes, that is a **semantic decision** (evidence identity), not a silent optimization: stop and surface it before proceeding.
- [ ] **Step 4: Run everything; commit** — `perf(orchestration): scope observation read model to current state`

### Task 8: Small, independent corrections

**8a — Pass resolution down (PERF-17).** `Chaining/WorkflowChaining.cs:430` and `:304` resolve the workflow twice from the same `(invocation, observation, definitions)`; a third `InvocationModeResolver.Resolve` at `:423`. Add the `WorkflowResolutionResult` to `WorkflowControllerRequest`; controller resolves only if absent. Test: decision-fact eligible/rejected lists unchanged. Commit: `perf(orchestration): resolve workflow once per cycle`.

**8b — Conditional persistence_state upsert (PERF-19).** `Persistence/CanonicalWorkflowPersistenceStore.cs:1509-1515`: add `ON CONFLICT(key) DO UPDATE SET value = excluded.value WHERE value <> excluded.value` (or gate behind LoopRelay.Core Task 1's memoized first-open). Existing tests at `CanonicalWorkflowPersistenceStoreTests:46-52,188` pin the 'imported'→'canonical' transition — they must stay green. Commit: `perf(orchestration): skip no-op persistence_state upsert`.

**8c — Indexed recovery-scope lookup (PERF-20).** `Persistence/CanonicalDecisionRecoveryStore.cs:198-215` joins on `json_extract(document_json,'$.scopeId')`. Persist `scope_id` as a real column on `canonical_recovery_action_events` (schema change → coordinate with LoopRelay.Core's migration machinery: new schema version or additive `ALTER TABLE` inside the existing convergence path — follow whichever pattern v14→v15 used, read it first), backfill on migration, index it, and rewrite the query. Also share one connection across the `RecordPlanAsync` helper chain (`:102-135,165,217-249`). Preserve latest-wins-by-event_id and CAS conflict semantics. Commit: `perf(orchestration): indexed recovery scope lookup`.

**8d — Async boundary journal (PERF-16, this project's half).** `Runtime/TransitionFaultsAndRecovery.cs:287` blocks streaming callbacks with `journal.RecordAsync(...).GetAwaiter().GetResult()`. Make the observer contract async or drain through an awaited ordered channel; each boundary event must be durable **before** `ShouldInterrupt` fires, in sequence order. Gate: fault-injection certification suite. Commit: `perf(orchestration): async boundary journal writes`. (The `DecisionSession` half lives in the LoopRelay.Cli plan, Task CLI-6 — align the contract shape.)

**8e — Observer hash cache + single milestone read (PERF-23).** `Resolution/RepositoryObserver.cs:896-912,81-98,644-658,728-745`: only after Tasks 1–3 land and audit M3 still shows hashing dominance. `(path, mtime, size) → hash` cache inside the single production observer instance, conservatively invalidated; share milestone file content between `ExecutionMilestoneGate` (`Services/ExecutionMilestoneGate.cs:46`) and the hasher. Freshness comparisons consume these hashes — any doubt re-hashes. Commit: `perf(orchestration): cache unchanged-artifact hashes in observer`.

**8f — Delete dead retained state (PERF-28, this project's share).** `Chaining/WorkflowChaining.cs:268-283`: remove the in-memory `records` list and `Records` property from `WorkflowBoundaryEvidenceWriter` (durable store keeps the data; sweep tests first). `Persistence/CanonicalLedgerEvidenceProjection.cs`: delete together with its only caller `LedgerEvidenceRetrieval` (LoopRelay.Cli plan Task CLI-13) — one PR across both projects, or wire the intended consumer deliberately instead (decide with the repo owner; audit found zero production callers). Commit: `chore(orchestration): remove unread boundary-record retention`.

---

## Verification Gate (whole plan)

- Full solution build + all test projects green; certification suites for transitions, effects, storage, and fault injection.
- Audit measurements M2 (persist cost flat vs. history), M3 (observations/cycle = 2: kernel + freshness), M4 (effect settlement query counts at target).
- Decision-fact JSON parity on scripted cycles (Task 7 gate).

## Decisions

*(append decisions made during execution here — Task 1 option chosen, Task 5 baseline numbers, Task 7 observation-field inventory, Task 8c schema-change pattern)*
