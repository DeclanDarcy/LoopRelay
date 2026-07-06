# Refactor: The Derivation Cache — Lazy, SQLite-Backed Derived Data with On-Demand Recovery

> Status: **Proposed — awaiting approval before any code is written.**
> Scope: backend-internal, shape-preserving at the API boundary. No UI/Rust changes.

---

## Executive summary

LoopRelay pays a ~30s cold start because four `IHostedService.StartAsync` methods run **eager per-repo derivation** before Kestrel binds: decision-session snapshot rebuilds, workflow timeline projection, workflow continuation, and execution-session recovery. The user's mandate is precise:

> "I don't want ANY of these to happen on startup. They should be stored in a DB and calculated when relevant. Recovery should be on-demand. I need an elegant and robust refactor. Use SQLite."

This document specifies **The Derivation Cache**: SQLite enters the system as a single, repo-local, **write-through cache for derived data plus a recovery-coordination ledger**, sitting *behind* the typed domain repositories. Source-of-truth evidence (decisions, reasoning, the decision-session registry, execution sessions) and the orchestrator's `.md` contract plane **stay exactly where they are** — on disk via `IArtifactStore`. The change is deliberately surgical: roughly four DI registration swaps plus one new project.

The design rests on one invariant verified directly in the source: **every eagerly-built snapshot splits into a *source-pure base* (safe to cache, keyed by a deterministic fingerprint) and a *time-dependent projection* (idle, growth rate, cache-miss risk, and the lifecycle Continue-vs-Transfer decision) that must be recomputed at read time from `base + now`.** The prior "skip rebuild when source unchanged" (A1) attempt failed precisely because it froze a boot-time `now` into the whole snapshot. The Derivation Cache makes the split structural: the base is cached and invalidated by `(source_fingerprint, formula_version)`; everything clock-dependent is recomputed on every read through an injected `TimeProvider`. Lazy is therefore *more* correct than today's startup pre-warm, not merely faster.

Outcome: Kestrel binds with **zero per-repo derivation work**; every wire shape and every `.md` file contract is preserved by construction; the ~1137-test suite stays reproducibly green; and the file-vs-DB boundary is made legible in the type system via an explicit `IDerivedSnapshotCache` distinct from `IArtifactStore`.

---

## The principle

Four rules, applied uniformly:

1. **No startup work.** Nothing per-repo is awaited before Kestrel binds. The three derivation hosted services are deleted; the one correctness-critical recovery (execution orphan reconciliation) is moved off the pre-bind path but kept deterministic.
2. **Lazy compute-on-read.** The first GET per repo computes-if-stale-else-returns-cached, behind a per-repo async gate that coalesces racing first-reads. The on-demand endpoints already exist (`GET /workflow/history` already calls `RecoverCurrentWorkflowAsync` inline; decision-sessions expose `GET/POST /recovery`).
3. **SQLite-backed cache.** Derived snapshots, workflow timelines, and recovery audit rows live in a per-repo SQLite DB, written through a single reusable `IDerivedSnapshotCache` keyed by `(repo, kind, source_fingerprint, formula_version)`. Atomic UPSERT replaces today's naive truncate-then-write.
4. **On-demand recovery.** Recovery becomes a per-repo, idempotent, fingerprint-keyed operation triggered by first access, never by boot. The one exception — execution orphan reconciliation — is a correctness state-fix, not a derivation, and stays eager-but-cheap.

**The hard rule that makes it robust: nothing time-dependent is ever cached.** The cached value is always the source-pure base; the wire record's clock-dependent fields are always recomputed from `base + TimeProvider.GetUtcNow()`.

---

## What moves to SQLite vs stays on disk — and why

The deciding test is a single question: **who is the consumer?**

### Stays on disk (genuine external contract — untouched)

The orchestrator `.md` plane enumerated in `OrchestrationArtifactPaths` (`src/LoopRelay.Orchestration/OrchestrationArtifactPaths.cs`):

- `plan.md`, `specs/epic.md` + `s{n}.md`, `operational_context.md`, `operational_delta.md`, `decisions/decisions.md` (+ rotated `decisions.NNNN.md`), `milestones/m*.md`, `handoffs/handoff.md` (+ rotated).

These are read/written directly by the orchestration loop and consumed by Codex provider turns. `decisions.md` is the canonical path every independent consumer reads (`ArtifactService.GetCurrentDecisionsAsync`, `DecisionContextService`, `ArtifactRotationService`, continuity). This is a **filesystem contract with the provider, not a cache.** `IArtifactStore`/`FileSystemArtifactStore` keeps its filesystem backing for this entire subset. **The migration boundary runs between the typed `.json` evidence and the `.md` projection/contract plane.**

### Stays on disk (source-of-truth evidence — out of scope for this refactor)

Decisions/candidates/proposals (`decision.json` + `history.json` per id), reasoning events/threads/relationships (`.json` + `.md` projection), operational-context proposals, the decision-session **registry** (`registry.json`), transfers, and continuity-artifacts.

**Why keep these on files:** the mandate is "don't do this on startup; store *derived* data in a DB; recovery on-demand" — not "rewrite the persistence layer." These already round-trip correctly. Moving them to SQLite would mean reproducing byte-identical `.json` *and* the `.md` projections that reasoning/decisions emit (which the orchestration loop reads as a contract), rewriting the `ParseSequence`-over-`ListDirectoriesAsync` id allocator, and redirecting the many tests that read raw `.agents` JSON directly. That is strictly more scope, more contract surface, and more migration risk than the ask requires. It remains a clean, clearly-separable follow-on (see **Open Decisions**).

*Confirmed safe to defer:* the Rust/Tauri shell (`src/LoopRelay.Shell/src/main.rs`) has zero filesystem/`.agents` access — it is a pure HTTP proxy. So these JSON files are not a Rust/UI contract; they could move later without breaking the shell. They simply don't need to move *now*.

### Moves to SQLite (internal-derived caches + recovery coordination)

Single in-process consumer: the backend's own observability projection. `DecisionSessionRouter.Evaluate` is pure no-I/O and never reads these.

- The **five decision-session derived snapshots**: metrics, economics, coherence, lifecycle-policy, transfer-eligibility (`src/LoopRelay.DecisionSessions/Persistence/DecisionSessionArtifactPaths.cs:12-20`). Consumed only by `DecisionSessionObservabilityService.GetProjectionAsync`.
- **Workflow timelines** (`FileSystemWorkflowRepository.SaveTimelineAsync` — the `.json`).
- **Decision-session recovery-result audit rows** + recovery **history** + the **metrics staleness stamp** (`DecisionSessionMetricsSnapshotStamp`).
- **Recovery coordination state** (a new per-repo ledger).

**Why these and only these:** they are pure `(repo)`-keyed upserts with frozen wire shapes, read by exactly one backend consumer. Storing the payload as a JSON column round-tripped through the *same* `System.Text.Json` options preserves every wire shape by construction. Moving them out of `.agents/decision-sessions/*` also makes the existing `DecisionSessionEndpointTests` byte-identical-listing assertion *pass trivially* (the directory stays empty), turning that test into proof the migration is clean.

> **Note on the `lifecycle-policy` and `transfer-eligibility` snapshots:** these are listed as "moving to SQLite" only in the sense that they stop being persisted as files. **They get no cached row at all** — they are entirely time-dependent (the Continue-vs-Transfer decision and eligibility status are functions of `GrowthRate` and `CacheMissRisk`) and are computed fresh on every read. See the next section.

---

## Concrete SQLite schema

**Layout.** One DB **per repo** at `<repo>/.agents/derived-cache.db` (co-located with the `.md` contract plane; DELETE-repo teardown drops the file with the `.agents` tree — satisfies m10). Plus **one global DB** at `%AppData%/LoopRelay/command-center.db` for the recovery ledger, which must survive even while a repo dir is being torn down. The DB path is resolved from the same env-var mechanism as `COMMAND_CENTER_CONFIGURATION_PATH` (e.g. `COMMAND_CENTER_DB_PATH`) so dev/test/prod DBs are separable — essential for the build-Release-while-Debug-runs constraint.

All connections, on open:

```sql
PRAGMA journal_mode = WAL;     -- concurrent readers + single writer
PRAGMA busy_timeout = 5000;    -- coalesce cross-process racers instead of throwing "database is locked"
PRAGMA synchronous = NORMAL;   -- WAL-safe durability
PRAGMA foreign_keys = ON;
```

### Per-repo DB (`<repo>/.agents/derived-cache.db`)

```sql
-- Derived cache: ONE row per (repo, kind). Stores the SOURCE-PURE BASE only.
-- No time-dependent field is ever written here.
CREATE TABLE derived_snapshot (
    repository_id      TEXT NOT NULL,
    kind               TEXT NOT NULL,   -- 'metrics-base' | 'economics-base' | 'coherence-base' | 'workflow-timeline'
    source_fingerprint TEXT NOT NULL,   -- composite content/row fingerprint of the source FAMILIES this kind depends on
    formula_version    TEXT NOT NULL,   -- e.g. 'decision-sessions.analysis.v1'; bump busts the cache
    schema_version     TEXT NOT NULL,   -- carries the envelope's SchemaVersion
    computed_at        TEXT NOT NULL,   -- ISO-8601; AUDIT only, never served as a live wire field
    payload_json       TEXT NOT NULL,   -- System.Text.Json of the PURE base record (no now()-derived fields)
    PRIMARY KEY (repository_id, kind)
) WITHOUT ROWID;

-- Per-source-family fingerprint cache: avoids re-hashing unchanged families on every read.
-- Recomputed only when row_count or max_updated_at shift.
CREATE TABLE source_fingerprint (
    repository_id  TEXT NOT NULL,
    family         TEXT NOT NULL,       -- 'decisions' | 'reasoning' | 'operational-context' | 'execution' | 'handoff' | 'git' | 'decision-session'
    fingerprint    TEXT NOT NULL,       -- content hash (NOT mtime)
    row_count      INTEGER NOT NULL,
    max_updated_at TEXT,
    computed_at    TEXT NOT NULL,
    PRIMARY KEY (repository_id, family)
);

-- Append-only audit: recovery results + decision-session recovery history.
CREATE TABLE recovery_result (
    repository_id TEXT NOT NULL,
    recovery_id   TEXT NOT NULL,
    occurred_at   TEXT NOT NULL,
    payload_json  TEXT NOT NULL,        -- the DecisionSessionRecoveryResult envelope, shape-identical
    PRIMARY KEY (repository_id, recovery_id)
);
CREATE INDEX ix_recovery_result_latest ON recovery_result(repository_id, occurred_at DESC);
```

**Key choices.**

- `derived_snapshot` rows are **upserts**: the base *is* the whole cache value. `WITHOUT ROWID` + composite PK gives O(1) lookup/upsert — replacing today's `ListTimelines` O(files) directory walk with a single keyed read.
- `lifecycle-policy` and `transfer-eligibility` have **no row**. They are never cached; the read-time projection computes them fresh.
- `source_fingerprint` is **per family**, so invalidation is scoped: `metrics-base` ← `{decisions, reasoning, operational-context}`; `coherence-base` ← `{reasoning}` only; `workflow-timeline` ← `{execution, handoff, decision, git, decision-session}`. A single global repo version would over-invalidate and defeat the cache.
- The envelope ownership/schema-version checks that today throw on read become `WHERE repository_id = @repo` (cross-repo reads return zero rows) plus a `formula_version`/`schema_version` guard.

### Global DB (`%AppData%/LoopRelay/command-center.db`)

```sql
-- Recovery coordination ledger: one row per repo, survives per-repo dir churn.
CREATE TABLE recovery_ledger (
    repository_id          TEXT PRIMARY KEY,
    execution_recovered_at TEXT,        -- the correctness state-fix; recorded once per process
    last_assessed_at       TEXT
);
```

Schema is versioned with `PRAGMA user_version` and applied idempotently (`CREATE TABLE IF NOT EXISTS` + a `user_version` bump) on first connection per DB. No EF migrations machinery; no startup cost (runs lazily on first repo touch).

---

## The lazy-compute + invalidation + time-field-recompute mechanism

Two small abstractions in a new `LoopRelay.Persistence.Sqlite` project. The seam is the **service layer** (endpoints already call services) — *not* `IArtifactStore`, which is path+glob-shaped and stays filesystem-backed.

```csharp
// The cache primitive. Generic over the PURE base type.
public interface IDerivedSnapshotCache
{
    Task<T?> TryGetAsync<T>(
        Guid repo, string kind, string fingerprint, string formulaVersion, CancellationToken ct);

    Task PutAsync<T>(
        Guid repo, string kind, string fingerprint, string formulaVersion, T baseValue, CancellationToken ct);
}

// Source fingerprinting — replaces the fragile mtime probe and the
// "run the whole projection just to get its fingerprint" anti-pattern.
public interface ISourceFingerprintProvider
{
    Task<string> ForFamiliesAsync(
        Repository repo, IReadOnlyList<SourceFamily> families, CancellationToken ct);
}
```

**The compute-if-stale-else-cached envelope** — one reusable helper every derived service calls:

```csharp
async Task<TLive> ReadDerivedAsync<TBase, TLive>(
    Repository repo,
    string kind,
    IReadOnlyList<SourceFamily> families,
    string formulaVersion,
    Func<CancellationToken, Task<TBase>> computeBase,    // SOURCE-PURE; the expensive part
    Func<TBase, DateTimeOffset, TLive> project,          // base + now -> wire record; cheap, I/O-free
    CancellationToken ct)
{
    using var _ = await _gate.AcquireAsync(repo.Id, kind, ct);     // per-(repo,kind) coalescing gate
    var fp = await _fingerprints.ForFamiliesAsync(repo, families, ct);

    var cached = await _cache.TryGetAsync<TBase>(repo.Id, kind, fp, formulaVersion, ct);
    if (cached is null)
    {
        // double-check after gate acquisition so concurrent first-readers coalesce to one compute
        cached = await _cache.TryGetAsync<TBase>(repo.Id, kind, fp, formulaVersion, ct)
              ?? await ComputeAndPutAsync(repo, kind, fp, formulaVersion, computeBase, ct);
    }

    return project(cached, _timeProvider.GetUtcNow());             // time-dependent fields ALWAYS recomputed
}
```

### Where it slots in

`DecisionSessionMetricsService.GetMetricsAsync` today does: *read snapshot (result discarded) → full evidence read → `BuildSnapshot` → write* (`src/LoopRelay.DecisionSessions/Services/DecisionSessionMetricsService.cs:18-42`). It becomes:

- `computeBase` = aggregate-query the counts/bytes/tokens + base timestamps. The base record is exactly `DecisionSessionMetrics` (pure counts, lines 63-74) plus `{sessionStartedAt, lastActivityAt, createdAt}`.
- `project(base, now)` = populate `Statistics` / `Activity` / `Growth` / `Cache` (lines 54-61, 75-78 — all `measuredAt`-relative) on every call.

`BuildSnapshot` (`:44`) is factored along the *existing record boundary*, so the wire record `DecisionSessionMetricsSnapshot` is byte-identical. Economics and coherence follow the same split:

- **Economics:** cache `{contextCost, reasoningCost, continuityBenefit, reusableCorpusScore}`; recompute `{transferValue, reuseValue, cacheBenefit}`.
- **Coherence:** cache `{coherenceScore, fragmentation, density, continuity, topology counts}` (including the expensive connected-components BFS — deterministic from source); recompute only `transferPressure`.

The cold-start cost itself ceases to exist even on a cache miss: `DecisionSessionEvidenceReader.ReadAsync`'s O(files) scan + JSON re-serialize-to-byte-count collapses to `SUM(byte_count) / SUM(char_count) / COUNT(*) / MAX(updated_at)` aggregates, with `byte_count`/`char_count` becoming stored columns written when each evidence row is persisted. *(In this phase, where evidence stays on files, the byte counts are computed once and memoized in the base; the full aggregate-column optimization lands only if/when evidence moves to SQLite — see Open Decisions.)*

### Invalidation

**Key = content/row fingerprint, never mtime.** Today's two keys are both weak: the decision-session mtime probe (`ProbeSourceMaxWriteUtc`, `DecisionSessionRecoveryService.cs:321-364`) is fragile (clock skew, touch-without-change, the documented self-invalidation hazard at lines 27-37); the workflow content hash is robust but requires running the full projection to compute it. `ISourceFingerprintProvider` hashes source content **per family**, memoized in `source_fingerprint` and recomputed only when `row_count`/`max_updated_at` shift — so the validity check is a cheap compare *before* any expensive derivation. This is what finally makes `WorkflowProjectionService.ProjectAsync` skippable: today `RecoverCurrentWorkflowAsync` calls it unconditionally just to obtain `rebuilt.Fingerprint` (`WorkflowRecoveryService.cs:26`); the fingerprint gate must wrap `ProjectAsync` itself, not merely the persist. `formula_version` (porting the existing `AnalysisOptionsVersion = "decision-sessions.analysis.v1"`) busts every dependent base when a formula/threshold changes.

### Time-field recompute

**Recomputed at read, full stop**, from the stored pure base + `TimeProvider.GetUtcNow()`:

| Field | Recompute rule |
|---|---|
| `idle` | `now − base.LastActivityAt` |
| `sessionAge`, `elapsed` | `now − base.{CreatedAt, SessionStartedAt}` |
| `activityRate`, `growthRate` | `count / Max(1, elapsedHours)` |
| `cacheRisk`, `cacheExpiresAt` | `f(elapsed, idle)`; `base.LastActivityAt + 1h` |
| `MeasuredAt` | `now` |
| economics `transferValue` / `reuseValue` / `cacheBenefit` | from recomputed metrics statistics |
| coherence `transferPressure` | from recomputed `growthRate` + `cacheRisk` |
| **lifecycle Continue-vs-Transfer decision** | `transferScore(now) > reuseScore` — **never stored** |
| transfer-eligibility (`status`, `checkedAt`, findings) | whole projection, computed on demand; no row |

This is the single most important correctness rule and the direct fix for why A1 failed: A1 cached the *whole* snapshot, serving a boot-time `now`. Here the base is cached but `now` is always fresh. **All `now` access routes through the injected `TimeProvider`** — never `DateTimeOffset.UtcNow` inline (note `WorkflowRecoveryService.cs:50` currently violates this and must be fixed). A frozen-time golden test would *not* catch a regression here, so we add an explicit **two-clock divergence test** asserting two reads at different injected clocks produce the expected `idle`/lifecycle-decision divergence.

---

## On-demand recovery design

**Trigger.** First repo access. The idempotent on-demand endpoints already exist and are exercised per-request — `GET /workflow/history` calls `RecoverCurrentWorkflowAsync` inline (`WorkflowEndpoints.cs:68`); decision-sessions have `GET /recovery` (persist:false) and `POST /recovery` (persist:true). The hosted services were pure pre-warm of these same operations.

**Idempotency.** Preserved by existing fingerprint-dedup. Workflow continuation already returns the existing event on `(fingerprint, fromStage, toStage, decision, reason)` match (`WorkflowContinuationService.cs:114-124`). Decision-session recovery is re-derivation of stateless findings (the registry reads files each call — there is no in-memory state to repair). The recovery-result audit row is the *only* non-reproducible artifact and is written once per explicit recovery (UPSERT by `recovery_id`).

**Concurrency.** Wrap each per-repo recovery method in a **per-repo `AsyncLazy<T>` keyed cache** (`ConcurrentDictionary<Guid, Lazy<Task>>`) so the first caller runs recovery exactly once per process and concurrent callers await the *same* Task. This closes the race that today has **no concurrency control** — `DecisionSessionRecoveryService` and `WorkflowRecoveryService` lack the `SemaphoreSlim` that `ExecutionSessionService` has, so two racing first-accesses currently tear a snapshot via naive truncate-then-write. At the cache layer, the per-`(repo,kind)` gate plus SQLite WAL `busy_timeout` plus atomic UPSERT make racing reads coalesce and racing writes last-writer-wins-atomic rather than torn.

**Preserved guarantees.**

| Guarantee (today) | Under the Derivation Cache |
|---|---|
| Registry validation (duplicate/zero/corrupt-active findings) | Reproduced by any `/diagnostics` GET; registry is stateless. No eager run needed. |
| Transfer-recovery classification | Runs in the on-demand `AssessAsync`; unchanged. |
| Recovery-result + recovery-event audit row | Written on explicit `POST /recovery` (and optionally on first-access for startup-parity history — see Open Decisions). Cadence preserved → `GetHistoryAsync` payload unchanged. |
| Lifecycle-policy snapshot as a **hard** transfer dependency (`ContinuityArtifactService.ReadRequiredPolicySnapshotAsync` throws if null) | Policy is now compute-on-read (never cached), so it is *always available and always fresh*. The eligibility/transfer flow materializes it before transfer-pending. This **removes** the stale-decision hazard rather than adding a gap. |
| Workflow stale-timeline discard | The source-version gate wraps `ProjectAsync`, so it runs only on source change; the timeline is advisory and consumers self-heal from the live projection. |

**The one exception — execution orphan recovery.** `ExecutionSessionService.RecoverAsync` (orphaned `Executing` → `Failed` / reattach) is a correctness **state mutation**, not a derived cache. It is time-sensitive: provider reattach needs live OS process handles, and a stale `Executing` would wrongly block `StartAsync`. It is **not** made lazy. It runs once, off the pre-Kestrel path, via an `IHostedLifecycleService.StartedAsync` hook (fires *after* Kestrel binds, so it does not block cold start) guarded by `recovery_ledger.execution_recovered_at`. It loads one global session file behind its existing `SemaphoreSlim` — cheap and deterministic.

---

## Removing the four hosted services

The derivation logic lives in the **services**, not the hosted classes; each hosted `StartAsync` is just a `foreach (repo) call ServiceMethod`. So:

- **Delete** `DecisionSessionRecoveryHostedService`, `WorkflowRecoveryHostedService`, `WorkflowContinuationHostedService` and their `AddHostedService<>()` registrations (`DecisionSessions/Extensions/ServiceCollectionExtensions.cs:33`, `Workflow/Extensions/ServiceCollectionExtensions.cs:27-28`). Their bodies survive verbatim as on-demand entrypoints (`RecoverAsync` / `RecoverCurrentWorkflowAsync` / `RunContinuationAsync`), now gated by `AsyncLazy` + fingerprint freshness.
- **Remove the 60s `PeriodicTimer` loop** in `WorkflowContinuationHostedService` entirely. Continuation/preparation become compute-on-demand; the existing fingerprint-dedup keeps it safe (no duplicate events).
- **Retarget, do not delete,** `ExecutionSessionRecoveryHostedService`: move it off pre-Kestrel `StartAsync` to the post-bind `StartedAsync` lifecycle hook described above.

Result: Kestrel binds with **zero per-repo derivation work**. Cold start collapses from ~30s to host-boot.

---

## EF Core vs Dapper

**Choose `Microsoft.Data.Sqlite` + Dapper. Not EF Core.**

1. The data is already immutable C# records serialized as JSON documents (`DecisionSessionArtifactDocument<T>`, `WorkflowTimeline`). We need keyed UPSERT of a JSON blob plus a few indexed columns — not change-tracking or navigation graphs.
2. Keeping `payload_json` as a `System.Text.Json` text column with the **exact existing `JsonSerializerOptions`** (`DecisionSessionJson.Options` etc.) preserves every wire shape byte-for-byte; the contract tests serialize identically regardless of storage.
3. EF's `DbContext`-per-scope lifetime fights the **all-Singleton** DI graph here (every service is `AddSingleton`; confirmed in each module's `ServiceCollectionExtensions`). Dapper over a `derived_snapshot` table is a near-mechanical swap.

---

## DI, migrations, and per-test SQLite isolation

**DI.** New `LoopRelay.Persistence.Sqlite` project exposing `ISqliteConnectionFactory` (per-repo `Data Source=<repo>/.agents/derived-cache.db`, path from env; plus the global DB), `IDerivedSnapshotCache`, `ISourceFingerprintProvider`, and the per-repo recovery gate — all `AddSingleton`. In each module's `ServiceCollectionExtensions`, the derivation services gain the cache dependency, and the three derivation `AddHostedService` lines go away. **`IArtifactStore` registration in `Program.cs` is untouched** (keeps the `.md` contract plane filesystem-backed). The connection factory must close/dispose the per-repo connection on m10 DELETE-repo teardown *before* the `.agents` tree is removed — otherwise Windows file locks block deletion.

**Migrations.** No EF migrations. A tiny idempotent `EnsureSchemaAsync` runs `CREATE TABLE IF NOT EXISTS` and bumps `PRAGMA user_version` on first connection per DB, lazily on first repo access. No migration runner, no startup cost.

**Per-test isolation** (must compose with `[Collection("ProcessEnvironment", DisableParallelization)]`):

- Provide `MemorySqliteSnapshotCache` — a `ConcurrentDictionary<(Guid, string), …>` test double exactly analogous to the existing `MemoryArtifactStore` / `InMemoryConfigurationStore`. The bulk of pure-service tests need no real DB and stay fully parallel **outside** the collection.
- Host-boot tests that need a real DB use a per-test temp file `Data Source={Path.GetTempPath()}/cc-{Guid}.db` deleted in `Dispose` (mirrors `DecisionSessionTestHarness.Create()`'s per-test temp-repo pattern), or `Data Source=:memory:` with a connection **held open for the test lifetime** (in-memory SQLite dies when the last connection closes). These stay inside `ProcessEnvironment`.
- **Tests never default to the prod DB path.** A single shared on-disk DB would re-introduce exactly the process-global contention `ProcessEnvironment` exists to serialize, and could spread it to the ~59 currently-parallel classes. Env-var-scoped paths + WAL keep the Release test host and a live Debug backend isolated (build-Release-while-Debug-runs constraint).

---

## API wire-shape preservation

The byte-stable surface is the endpoint DTO serialization boundary (`System.Text.Json` + `JsonStringEnumConverter`, Web defaults, indented) — **orthogonal to where bytes are stored.** The single read seam, `DecisionSessionObservabilityService.GetProjectionAsync` (`:36-85`), is repointed from `sessionRepository.Read*SnapshotAsync` (file reads) to the lazy compute-on-read providers. The DTO record shapes it emits are unchanged, so:

- Golden-fixture tests (`OrchestrationSnapshotContractTests`, `ContractOracleFixtureTests`), consumer-verification tests (Rust shell, TypeScript types), and freshness tests (`ContractGeneratedArtifactFreshnessTests`, SHA-pinned on `repositories.ts`) all pass — they inspect serialization, never persistence. **UI stays 420/420 by construction.**
- `payload_json` round-trips through the *same* serializer → casing, null-omission, property order, enum-as-string all identical.

**Two preservation hazards the design explicitly closes:**

1. **Passive → active read.** `GetProjectionAsync` today does *passive* `ReadNullableAsync` of pre-warmed snapshot files (`:37-45`), relying on the hosted services having materialized them. Once those services are deleted, this read **must become an active compute-on-read call**, or the projection returns null snapshots and the `warnings`/health-status arrays silently change shape — the exact mock/contract-divergence class in project memory. We pin this with a test asserting `GetProjectionAsync` after a cold cache (no pre-warm) yields populated snapshots and unchanged warning arrays.
2. **Null-on-unavailable semantics.** The lazy provider must return `null` (not throw) on first-read-before-compute where the old path returned null + a Warning, so no warning-string drift.

---

## Phased migration plan

Each phase builds in Release, keeps the ~1137-test suite reproducibly green (respecting `[Collection("ProcessEnvironment")]`), and is a single-registration or single-method revert.

### Phase 0 — Scaffold (no behavior change)

- **Change:** Add `LoopRelay.Persistence.Sqlite` (Dapper + `Microsoft.Data.Sqlite`), `ISqliteConnectionFactory`, `IDerivedSnapshotCache`, `ISourceFingerprintProvider`, `MemorySqliteSnapshotCache`, `EnsureSchemaAsync`. Register the factory; wire nothing into services.
- **Why safe:** Nothing consumes it.
- **Tests:** All green unchanged. Add unit tests for the cache double and schema-ensure.

### Phase 1 — Time-correctness first, storage unchanged

- **Change:** Refactor `DecisionSessionMetricsService.BuildSnapshot` into `BuildBase` (pure) + `project(base, now)`; route all `now` through `TimeProvider`. Persist the base via the **existing file repo** for now. Fix `WorkflowRecoveryService.cs:50` `DateTimeOffset.UtcNow` → `TimeProvider`. Add the **two-clock divergence test** and the **passive→active `GetProjectionAsync` cold-cache test**.
- **Why safe:** Lands the hardest correctness fix independently of SQLite; the wire record is unchanged.
- **Tests:** Metrics/economics/coherence tests green; audit now-sensitive assertions to ensure they use a pinned `TimeProvider`. New divergence + cold-cache tests added.

### Phase 2 — Fingerprint provider replaces the mtime probe

- **Change:** Implement `ISourceFingerprintProvider` (content hash per family, memoized). Rewrite `CanSkipDerivedRebuildAsync`/`ProbeSourceMaxWriteUtc` to use it.
- **Why safe:** Pure swap of the staleness key; behavior equivalent but deterministic.
- **Tests:** `RecoverySkipsRebuildWhenSourceUnchanged` (`DecisionSessionRecoveryTests.cs:152`) is **rewritten** to a stored-fingerprint staleness test in the same commit. All others green.

### Phase 3 — Flip derived snapshots to SQLite

- **Change:** Route metrics/economics/coherence bases through `IDerivedSnapshotCache` (compute-if-stale-else-cached). **Delete** lifecycle-policy and transfer-eligibility persistence (compute-on-read only). Repoint `GetProjectionAsync` to the providers. `.agents/decision-sessions/analysis/*` files stop being written.
- **Why safe:** DTOs unchanged; storing the base as a JSON column round-trips through the same serializer.
- **Tests:** `DecisionSessionEndpointTests` byte-identical-listing assertion now passes because the derived directory stays empty. Observability/contract tests green.

### Phase 4 — Workflow timeline cache + recovery-result audit to SQLite

- **Change:** Cache `workflow-timeline` base keyed by fingerprint; gate `ProjectAsync` behind the fingerprint check (the actual cold-start win for workflow). Move `recovery_result` rows to SQLite (`GetHistoryAsync` reads via the repo interface). Verify no external reader of the timeline `.md` before dropping it; if one exists, keep the `.md` file-backed and move only the `.json`.
- **Why safe:** Timeline is advisory; consumers self-heal from the live projection.
- **Tests:** Workflow projection/recovery tests green (they drive services).

### Phase 5 — Delete the hosted services (the payoff)

- **Change:** Remove the three derivation `AddHostedService` lines + classes; wrap entrypoints in the per-repo `AsyncLazy` gate. Move `ExecutionSessionRecoveryHostedService` to the post-Kestrel `StartedAsync` hook + `recovery_ledger` guard. Remove the continuation 60s loop. Guarantee policy-base materialization at transfer-pending.
- **Why safe:** Logic already lives in the services; smallest diff, fully revertible.
- **Tests:** Retarget the **three tests that `new` a hosted class** (`DecisionSessionRecoveryTests.cs:138`, `WorkflowProjectionServiceTests.cs:2657`/`2675`) to the on-demand entrypoint **in the same commit**. Cold-start measured to drop to host-boot.

Each phase ships independently. Phases 0–4 already deliver lazy, fresh, SQLite-backed derived data; Phase 5 delivers the zero-startup payoff and is a one-line-per-service revert if anything regresses.

---

## Honest risk register

- **Time-dependence is the real break, not storage.** Frozen-time golden tests stay green even if `project(base, now)` regresses a Continue/Transfer decision — the documented mock/contract-divergence class. *Mitigation:* mandatory injected `TimeProvider` + the two-clock pin test (Phase 1). Highest severity.
- **Passive→active observability read.** If `GetProjectionAsync` isn't converted to an active compute call when the hosted services are deleted, the `warnings`/health arrays silently change shape. *Mitigation:* the cold-cache projection test (Phase 1).
- **Fingerprint cost.** Naive per-read content hashing trades an O(files) scan for an O(files) hash. *Mitigation:* memoize `source_fingerprint`, recompute only when `row_count`/`max_updated_at` shift (Phase 2).
- **Cross-process WAL contention.** Release test host + Debug backend on the same DB file corrupts without WAL/env-scoping. *Mitigation:* env-var-scoped paths + WAL + per-test temp DBs. Operational discipline that is easy to violate.
- **`.md` dual-write consistency.** Where a regenerable `.md` accompanies a SQLite row, DB-commit and file-write are two media; a crash between them re-introduces inconsistency unless the `.md` is treated as a regenerable projection. *Mitigation:* verify no external `.md` reader (Phase 4) and treat any kept `.md` as derive-on-write.
- **Source-evidence durability is out of scope.** The naive truncate-then-write torn-file class remains for source/`.md` writes. Only derived writes become atomic. Accepted by design.

---

## OPEN DECISIONS FOR THE USER

Three genuine forks require your call before code is written. Each has a recommendation.

### 1. Source-of-truth scope: derived-only now, or also migrate evidence to SQLite?

This design moves **only derived data + recovery coordination** to SQLite and leaves source-of-truth evidence (decisions, reasoning, registry, execution sessions, config) on files. A larger alternative would make SQLite the *system of record* for all evidence — gaining atomic multi-part writes, `MAX(seq)+1` id allocation, and the single-active invariant as a partial UNIQUE index, but requiring byte-identical `.json`/`.md` reproduction, a one-time file→row backfill for every existing repo, and redirection of every test that reads raw `.agents` JSON.

> **Recommendation: derived-only now.** It fully satisfies the ask ("store *derived* data in a DB; recovery on-demand; no startup work") at roughly four DI swaps and the lowest contract/test risk. The evidence migration is a clean, clearly-separable follow-on if a durability/atomicity goal later emerges; this design is a strict subset of it, so nothing here blocks that path.

### 2. DB locality: per-repo DB + global ledger, or one global DB?

This design uses **per-repo `derived-cache.db` + a global `command-center.db`** for the recovery ledger. Per-repo co-locates data with `.agents` (clean DELETE-repo teardown = drop the file, no `repository_id` partitioning to get wrong); the global ledger survives per-repo dir churn. The alternative — one global DB with `repository_id` on every table — centralizes backup but complicates per-repo deletion and needs partitioning discipline everywhere.

> **Recommendation: per-repo + global ledger.** Cleanest teardown, no partition-key footguns, mirrors today's `.agents` locality. *Confirm:* m10 teardown closes/disposes the per-repo connection before `rm -rf .agents` (Windows file-lock).

### 3. First-access recovery-result emission: write an audit row on first GET, or only on explicit `POST /recovery`?

Today the hosted service writes a recovery-result/event audit row at boot for every repo, feeding `GetHistoryAsync`/health. On-demand, we can either (a) write a row on first repo access (startup-parity history) or (b) write only on explicit `POST /recovery`.

> **Recommendation: explicit `POST /recovery` only**, with the first-access guard emitting a row *iff* it performs a state-changing assessment. This keeps the `GetHistoryAsync` payload shape stable while avoiding a spurious audit row on every cold read. If you rely on a recovery-history entry appearing for every repo at startup, choose (a) instead — note it changes *when* history rows appear, not their shape.

---

## Implementation notes — deviations from this design (as built, Phases 0–5)

The refactor shipped with these deliberate, verified deviations. Each was driven by an adversarial-review finding that the green unit suite initially masked (file-path test constructors / stubbed fingerprints hid the SQLite-wired production path).

1. **The workflow timeline is NOT cached.** It folds in *live* git state (read from `git status`, not any `.agents` file) and operational-context, which a source-content fingerprint cannot track — caching it would serve a stale timeline after a commit/push and defeat `RecoverCurrentWorkflowAsync`'s persisted-vs-fresh reconciliation. The timeline is projected fresh on demand; since Phase 5 removed the startup path this is cheap. The Phase 4 `recovery_result` → SQLite move shipped as planned; the metrics/economics/coherence bases ARE cached as designed.
2. **Atomic file writes landed now (not deferred).** `FileSystemArtifactStore.WriteAsync` writes a same-directory temp file then `File.Replace`/`File.Move`, closing the truncate-then-write torn-read race for all file writes (the risk register's deferred hardening).
3. **The per-repo recovery gate is a serialize-and-rerun lock, not a once-per-process `Lazy`.** Live-read endpoints (`GET /workflow/history`, `/recovery`) must return fresh state, so the gate serializes concurrent same-repo recoveries but re-runs each call; the recovery endpoints route through the runners. Execution orphan recovery remains genuinely once-per-process via the `recovery_ledger`.
4. **Observability reads recovery history from SQLite.** `DecisionSessionObservabilityService` reads `recovery_result` rows from the SQLite store (file fallback), mirroring `GetHistoryAsync`, so the lifecycle projection/health and `/recovery/history` agree instead of going split-brain.

Final state: full backend suite **1167 passed / 1 skipped (live-Codex) / 0 failed**, independently re-run and adversarially verified. End-to-end integration tests (real host + SQLite wired) were added to close the gap that masked the defects above.
