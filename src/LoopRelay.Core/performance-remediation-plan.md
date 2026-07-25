# LoopRelay.Core Performance Remediation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the per-operation schema-verification/repair overhead and connection-churn costs owned by `LoopRelayWorkspaceDatabase`, and add the targeted evidence-listing API that Completion consumes.

**Architecture:** All changes live in `Services/Persistence`. The schema-ensure fix is the highest-leverage change in the whole audit (PERF-01): every store in every project opens connections through this class, so fixing it here fixes all of them. No store signatures change.

**Tech Stack:** .NET 10, Microsoft.Data.Sqlite 10.0.0, xUnit (`tests/LoopRelay.Core.Tests`).

**Source findings:** PERF-01, PERF-10, PERF-14 (store side), PERF-15, PERF-27g in [production-code-performance-audit.md](../../production-code-performance-audit.md). Line references are against commit `0de6b5a8` — re-locate before editing.

## Global Constraints

- Fail-closed behavior is untouchable: `WorkspaceCompatibilityImportRequiredException` for `LegacyContinuity` schemas; hard failure on `Unknown` family and newer-than-supported versions (`LoopRelayWorkspaceDatabase.cs:115-129`).
- Migration path for pre-v15 databases must remain byte-identical in effect (same SQL, same transaction scope, same stamping).
- `PRAGMA foreign_keys = ON` must still run on **every** connection (it is per-connection state).
- Immutable workspace-identity validation must still reject identity swaps at first contact per process.
- Do not change durability of committed transactions.
- Multi-process stance (audit §15 Q1) is unresolved: the memoization design below therefore keeps a cheap per-open stamp re-read rather than trusting memory alone.

## Cross-Project Dependencies

- **Downstream beneficiaries (no action needed there):** every `OpenAsync` in LoopRelay.Orchestration.Primitives, LoopRelay.Completion, LoopRelay.Cli stores gets faster automatically.
- **Task 2 (WAL/pooling) blockers:** must be verified against the DB file-swap flows in LoopRelay.Orchestration.Primitives (`WorkspaceStorageApplicationService`, `CanonicalImportGateway`) and the inventory hashing in `WorkspaceStorageInspector` (a `-wal`/`-shm` side file appears in the persistence directory). Land Task 2 **after** LoopRelay.Orchestration.Primitives Task ORCH-3 or coordinate the two.
- **Task 4 (evidence listing) consumer:** `CompletionArtifacts` in LoopRelay.Completion (its plan Task COMP-1).

---

### Task 1: Confine repair SQL to the migration branch and memoize schema verification per (path, stamp) — PERF-01

**Files:**
- Modify: `src/LoopRelay.Core/Services/Persistence/LoopRelayWorkspaceDatabase.cs:109-194` (`EnsureSchemaAsync`), `:1250-1415` (shape-requirement computed properties), `:1465-1538` (`ReadSatisfiedRequirementsAsync`)
- Test: `tests/LoopRelay.Core.Tests` (new file `LoopRelayWorkspaceDatabaseEnsureTests.cs` or the existing schema test file if one exists — check first)

**Interfaces:**
- Consumes: existing `EnsureSchemaAsync(SqliteConnection, CancellationToken)` — signature unchanged; `connection.DataSource` supplies the memoization key.
- Produces: same public API; new `internal static void ResetSchemaVerificationCacheForTesting()` and `internal static int FullVerificationRuns` counter (test-only observability; Core already has an `InternalsVisibleTo` pattern in sibling projects — add `<InternalsVisibleTo Include="LoopRelay.Core.Tests" />` to `LoopRelay.Core.csproj` if absent).

**Current behavior (verified):** on a `CanonicalV15Complete` database, every call runs full `InspectSchemaAsync` (~180 probes), identity validation, a legacy-resume probe, then `BeginTransaction(deferred: false)` + `CanonicalDataRepairSql` (5 UPDATEs + `DROP TABLE IF EXISTS canonical_blockers`, `:3075-3082`) + commit — i.e., a write transaction per store operation (`:142-154`).

**Change contract:**
1. In the `structurallyComplete` branch, execute `ImportLegacyResumeAsync` only when `legacyResume is not null` (already conditional) and **stop executing `CanonicalDataRepairSql` there entirely**. When `legacyResume is null`, do not open a transaction at all — the branch becomes read-only. (A v15-stamped-complete DB cannot contain the legacy 'Blocked' vocabulary: the stamp is only written after the migration path that runs the repair, `:172,186`.)
2. Add a per-process memo: `private static readonly ConcurrentDictionary<string, (long Version, string ShapeFingerprint)> VerifiedSchemas` keyed by `Path.GetFullPath(connection.DataSource)`. On each call: read the stamped `schema_version` + shape fingerprint from `schema_metadata` (2 small SELECTs). If they match the memo entry → run only `PRAGMA foreign_keys = ON` and return. Otherwise run the full existing pipeline and, on success, store the stamp in the memo. First contact per process still pays full verification including identity validation.
3. Convert the shape-requirement computed properties (`:73-92, 1250-1415`) from `=>` LINQ chains to `static readonly` fields so the ~190-element lists and their fingerprint are built once per process.

- [ ] **Step 1: Read the full current `EnsureSchemaAsync`, `InspectSchemaAsync`, `ValidateExistingWorkspaceIdentityAsync`, `ReadLegacyResumeAsync`, and the stamp readers** to confirm which `schema_metadata` keys hold version and fingerprint, and confirm no other caller depends on the complete-branch transaction.
- [ ] **Step 2: Write failing tests** in `tests/LoopRelay.Core.Tests`:
  - `EnsureSchema_OnHealthyDb_SecondCallSkipsFullVerification`: create a fresh DB via first `EnsureSchemaAsync` (migration path), reset + capture `FullVerificationRuns`, call ensure twice more on new connections, assert counter increased by exactly 1 (first contact) then 0.
  - `EnsureSchema_OnHealthyDb_PerformsNoWrite`: after first contact, snapshot `PRAGMA data_version` from a *second open connection*, run ensure on a third connection, re-read `data_version` from the second and assert unchanged (data_version increments when another connection commits).
  - `EnsureSchema_StampMismatch_RerunsFullVerification`: after memoization, tamper the stamped fingerprint row directly via SQL, call ensure, assert `FullVerificationRuns` increments (and behavior matches current full-pipeline outcome for that state).
  - `EnsureSchema_LegacyContinuity_StillThrowsImportRequired`: existing fixture behavior — keep/port whatever test exists today; must still pass unmodified.
- [ ] **Step 3: Run the new tests, verify they fail** (`dotnet test tests/LoopRelay.Core.Tests --filter EnsureSchema`). Expected: counter/data_version assertions fail against current code.
- [ ] **Step 4: Implement the change contract** (repair-SQL confinement first, then memo, then static requirement lists — three commits are fine).
- [ ] **Step 5: Run the full Core test suite and the migration/certification fixtures** (`dotnet test tests/LoopRelay.Core.Tests`; then `dotnet test tests/LoopRelay.Orchestration.Primitives.Tests tests/LoopRelay.Cli.Tests` for downstream store coverage). Expected: all pass; legacy fixtures unchanged.
- [ ] **Step 6: Commit** — `perf(core): confine repair SQL to migration; memoize schema verification per (path, stamp)`

### Task 2: Enable WAL + busy_timeout and decide pooling — PERF-10

**Files:**
- Modify: `src/LoopRelay.Core/Services/Persistence/LoopRelayWorkspaceDatabase.cs:1588-1610` (the three connection factories, all currently `Pooling = false`)
- Test: `tests/LoopRelay.Core.Tests` + the export/import round-trip suites in `tests/LoopRelay.Orchestration.Primitives.Tests`

**Interfaces:**
- Produces: same three factory methods; behavior addition — first writable open per (process, path) executes `PRAGMA journal_mode=WAL; PRAGMA busy_timeout=<value>;` (fold into Task 1's first-contact step so it runs exactly once per process; busy_timeout must still be set per connection since it is connection state — a cheap single PRAGMA).

**Prerequisite (do not skip):** answer audit §15 Q2 — search git history and docs for why `Pooling=false` was chosen (`git log -S "Pooling = false" -- src/LoopRelay.Core`). If the DB file-swap flows (`WorkspaceStorageApplicationService`, `CanonicalImportGateway`) require exclusive closure, either (a) enable pooling globally and call `SqliteConnection.ClearPool(...)` before file swaps in those flows, or (b) keep `Pooling=false` and take only WAL + busy_timeout here. Option (b) is the safe default if the answer is unclear.

- [ ] **Step 1: Establish the pooling answer** per the prerequisite; record it in this file under a `## Decisions` heading.
- [ ] **Step 2: Write failing test** `Database_UsesWalAndBusyTimeout`: after first contact, assert `PRAGMA journal_mode` returns `wal` and `PRAGMA busy_timeout` returns the configured value on a fresh connection.
- [ ] **Step 3: Implement** (WAL once at first writable contact; busy_timeout per connection; pooling per the decision).
- [ ] **Step 4: Run export/import round-trip tests** in `tests/LoopRelay.Orchestration.Primitives.Tests` — a WAL database must survive export, import, and file replacement (checkpoint before copy: `PRAGMA wal_checkpoint(TRUNCATE)` in the swap flows if tests fail on side files).
- [ ] **Step 5: Coordinate with `WorkspaceStorageInspector` inventory hashing** (Orchestration plan Task ORCH-3): `-wal`/`-shm` files now appear under `.LoopRelay/persistence`; verify `storage verify` output remains stable (checkpoint first or exclude side files deliberately — pick one and test it).
- [ ] **Step 6: Commit** — `perf(core): enable WAL and busy_timeout for the workspace database`

### Task 3: Stamped fast-path for read-side schema inspection — PERF-15

**Files:**
- Modify: `src/LoopRelay.Core/Services/Persistence/LoopRelayWorkspaceDatabase.cs:196-406` (add `InspectStampedAsync`), `src/LoopRelay.Core/Services/Persistence/WorkspaceDatabaseInspection.cs:54-55` (migration executor's back-to-back double inspection)
- Callers to convert (separate projects, listed here for the interface contract): `LedgerLoopHistoryStore.cs:104-109` (LoopRelay.Cli), `WorkspaceStorageInspector.cs:33` (Orchestration, coordinated via its Task ORCH-3), `ImportPortfolioDetector.cs:44` (Orchestration)
- Test: `tests/LoopRelay.Core.Tests`

**Interfaces:**
- Produces: `public static Task<WorkspaceSchemaInspection> InspectStampedAsync(SqliteConnection, CancellationToken)` — returns the same `WorkspaceSchemaInspection` shape; trusts a well-formed stamp (reads `schema_metadata` only) and falls back to the full `InspectSchemaAsync` when the stamp is absent, malformed, or internally inconsistent. Full classification stays available and unchanged.

- [ ] **Step 1: Write failing tests**: `InspectStamped_OnStampedDb_IssuesNoTableProbes` (assert via the Task 1 counter pattern or a probe counter) and `InspectStamped_OnUnstampedDb_FallsBackToFullClassification` (result equals `InspectSchemaAsync` result field-for-field on a legacy fixture).
- [ ] **Step 2: Implement `InspectStampedAsync`.**
- [ ] **Step 3: Remove the redundant second inspection** in `WorkspaceSchemaMigrationExecutor` (`WorkspaceDatabaseInspection.cs:54-55`) — `EnsureSchemaAsync` already ran `VerifyCanonicalV15ShapeAsync` inside its transaction; return its outcome instead of re-inspecting. Read the executor fully first; preserve its reported inspection contents (construct them from the ensure path's data, not a fresh probe).
- [ ] **Step 4: Run Core tests; commit** — `perf(core): add stamped fast-path schema inspection`

### Task 4: Path-only evidence listing with SQL-side filtering — PERF-14 (store side)

**Files:**
- Modify: `src/LoopRelay.Core/Services/Persistence/SqliteExecutionEvidenceStore.cs:88-120` (`ListAsync`)
- Test: `tests/LoopRelay.Core.Tests`

**Interfaces:**
- Produces: `public Task<IReadOnlyList<ExecutionEvidencePath>> ListPathsAsync(string? globPattern, CancellationToken)` (or matching repo naming conventions — read the file's existing record types first) returning `(Stem, Sequence, LogicalPath)` only: `SELECT stem, sequence, logical_path` with the pattern translated to a SQL `GLOB`/`LIKE` filter, ordered by `(stem, sequence)`. **No `body` column fetched, no hash computed.**
- Consumes/preserves: existing `ListAsync` keeps full-body fetch + per-row `ValidateHash` for content consumers — do not weaken it; optionally push its glob into SQL too (same rows returned, hash validation retained for every returned row).

- [ ] **Step 1: Read `SqliteExecutionEvidenceStore.cs` fully** (schema, record types, existing consumers via `grep -r "ListAsync" src/`).
- [ ] **Step 2: Write failing tests**: `ListPaths_ReturnsSamePathsAsListAsync` (seed rows incl. non-matching ones; assert path-set equality with the filtered `ListAsync` projection) and `ListPaths_DoesNotReadBodies` (seed a row whose stored body is deliberately corrupted vs its hash; `ListPathsAsync` succeeds, `ListAsync` throws/flags — proving bodies are untouched).
- [ ] **Step 3: Implement; run tests.**
- [ ] **Step 4: Commit** — `perf(core): add path-only execution-evidence listing` (consumer switch happens in LoopRelay.Completion Task COMP-1).

---

## Verification Gate (whole plan)

- `dotnet build` clean; full solution test sweep: `dotnet test` per test project.
- Audit measurement M1 (SQLite statement count per attempt) re-run after Task 1: ensure-attributable statements should drop to first-contact-only; no write transaction on healthy-path store opens.
- No behavior change on: legacy-DB import-required signaling, migration fixtures, export/import round-trips.

## Decisions

- **Task 1, Part 1 (repair SQL confinement):** implemented as a deviation from the brief's literal wording. `CanonicalDataRepairSql` remains unconditional in `RunStructurallyCompleteBranchAsync` (fast path and full pipeline both), rather than being removed entirely as the brief specified. Reason: the brief's stated rationale ("a v15-stamped-complete DB cannot contain the legacy 'Blocked' vocabulary") is empirically false — `WorkflowResolverTests.Previously_latched_blocked_workflow_resolves_on_its_real_gate_condition_after_migration` (LoopRelay.Orchestration.Primitives.Tests) depends on every `EnsureSchemaAsync` call self-healing stray legacy `'Blocked'` labels, with no other recovery path. Removing the repair SQL there regressed that test. Human-reviewed and approved: the repair SQL's cost is an unavoidable full-table scan either way (none of `canonical_workflow_states`/`canonical_stage_states`/`canonical_transition_runs`'s `state`/`outcome` columns are indexed, so a pre-check `SELECT EXISTS` probe would cost the same as the `UPDATE ... WHERE` it would guard); the dominant win Task 1 targets (skipping the ~190-probe `InspectSchemaAsync` + identity validation) is fully preserved regardless. A true zero-transaction fast path would require adding an index — an out-of-scope schema change — and was rejected for this task. Commit `0fd1c21d`.

- **Task 2 (pooling decision, audit §15 Q2):** kept `Pooling=false` on all three connection factories (`OpenReadWriteCreate`, `OpenReadWrite`, `OpenReadOnly`); did **not** switch to pooled connections + `SqliteConnection.ClearPool(...)`. Evidence: `git log -S "Pooling = false" -- src/LoopRelay.Core` shows `Pooling = false` was present from the very first commit that introduced this class (`a1c3f1646`, "Move runtime persistence to SQLite"), with no explanatory commit message or code comment — the "why" is genuinely undocumented in this repo's history. However, a concrete mechanism makes the current setting load-bearing: the DB file-swap flows live in `src/LoopRelay.Cli/Services/Storage/WorkspaceStorageApplicationService.cs` (`File.Delete(staging)`, `File.Delete(sidecar)` — not `Orchestration.Primitives` as this file's Task 2 prose says; that attribution has drifted since the reference commit) and `src/LoopRelay.Cli/Services/Import/CanonicalImportGateway.cs` (`File.Delete(working)`). Both call `File.Delete` directly on a workspace database file. On Windows (this codebase's host platform), `File.Delete` fails with `IOException` if any process holds an open native handle to that file. Microsoft.Data.Sqlite's connection pooling (the default, `Pooling=true`) keeps idle native `sqlite3*` handles alive after `Dispose()` for reuse; `Pooling=false` forces the native handle closed immediately on `Dispose()`. Enabling pooling would risk these `File.Delete` calls intermittently failing whenever a pooled idle handle to the same path is still resident — a plausible, concrete reason to keep `Pooling=false`. Decision: option (b) — keep `Pooling=false` unchanged; add only `PRAGMA journal_mode=WAL` (once per process per path, folded into Task 1's cache-miss/first-contact branch in `EnsureSchemaAsync`) and `PRAGMA busy_timeout` (every connection, alongside the existing unconditional `PRAGMA foreign_keys = ON`). Verified the actual file-swap round-trip tests (`tests/LoopRelay.Cli.Tests`, which is where `CanonicalImportGatewayTests` and the storage init/migrate flows that call `File.Delete` on the workspace db actually live — see the attribution note above) pass unchanged with WAL enabled, 395/395; no `wal_checkpoint(TRUNCATE)` was needed in `WorkspaceStorageApplicationService.cs`/`CanonicalImportGateway.cs` because every connection in those flows is fully closed (and, with `Pooling=false`, its native handle released) before the corresponding `File.Delete`/copy runs.

  Two findings surfaced during verification, recorded here rather than silently worked around:
  1. **Cross-project storage-verify-stability gap (Orchestration.Primitives, out of scope for this task):** 4 tests in `tests/LoopRelay.Orchestration.Primitives.Tests` (`WorkspaceStorageAuthorityTests.Repeated_verify_is_byte_and_tree_inventory_preserving`, and 3 `WorkflowResolverTests.Repository_observation_*` "does not mutate the repo" tests) newly fail because `-wal`/`-shm` side files now appear in the persistence directory after a connection is opened against an already-WAL database, even on the read-mostly/cache-hit path, and are not always cleaned up by connection close. Confirmed via `git stash` that all 4 pass at HEAD (pre-Task-2) and fail only with WAL enabled — a genuine regression, not pre-existing flake. This is exactly the `WorkspaceStorageInspector` inventory-hashing coordination gap the plan's Cross-Project Dependencies section calls out as Orchestration.Primitives' own Task ORCH-3 to own; left unfixed here per that task boundary.
  2. **`busy_timeout=10000` measurably slows the LoopRelay.Cli.Tests suite:** full-suite timing (`dotnet test tests/LoopRelay.Cli.Tests`, 395 tests each run): HEAD baseline 7m11s; WAL-only (busy_timeout pragma temporarily disabled) 6m48s (noise-level, confirms WAL itself is not the cause); WAL + `busy_timeout=10000` 11m31s — a reproducible ~4m20s (~60%) increase attributable specifically to the busy_timeout pragma. Likely cause: tests that deliberately provoke `SQLITE_BUSY` (e.g. to exercise error/contention handling) now block inside SQLite's own busy-handler for up to the full 10s before returning, whereas previously (no explicit busy_timeout set) SQLite returned `SQLITE_BUSY` immediately and any retry was handled by Microsoft.Data.Sqlite's own faster ADO.NET-level logic. All 395 tests still pass — this is a latency cost, not a correctness break. `10000` was kept (not tuned down) because it matches existing production precedent (`LoopRelay.Certification.PersistenceLifecycleRunner`'s ad hoc `PRAGMA busy_timeout=10000;` for concurrent-writer contention) and changing it was outside this task's brief; flagged here for the task owner to decide whether a smaller production value is warranted given this cost.

- **2026-07-25, repair-transaction conditionality refinement (supersedes the Task 1, Part 1 entry above; addresses the Task 2 finding #1 regression):** `RunStructurallyCompleteBranchAsync` no longer opens `CanonicalDataRepairSql`'s write transaction unconditionally. It now runs a cheap, read-only `SELECT EXISTS(... UNION ALL ...)` probe (`HasRepairableLegacyBlockedVocabularyAsync`) for the exact legacy `'Blocked'` vocabulary the repair SQL targets across `canonical_workflow_states`, `canonical_stage_states`, `canonical_transition_runs`, and the presence of the legacy `canonical_blockers` table, and opens the write transaction only when that probe finds something, or when a legacy resume import is pending. On a genuinely healthy database this is now a true zero-transaction read, restoring the literal "read-only fast path" Task 1's original brief asked for — while the self-healing guarantee `WorkflowResolverTests.Previously_latched_blocked_workflow_resolves_on_its_real_gate_condition_after_migration` depends on is unchanged: a database that actually has stray legacy `'Blocked'` labels still gets repaired on the next `EnsureSchemaAsync` call. TDD: a new Core-level test-only counter, `LoopRelayWorkspaceDatabase.RepairTransactionsOpened` (mirroring the existing `FullVerificationRuns` pattern), backs `EnsureSchema_OnHealthyDb_MemoizedFastPath_OpensNoWriteTransaction` in `tests/LoopRelay.Core.Tests/Services/LoopRelayWorkspaceDatabaseEnsureTests.cs`; a second new test, `EnsureSchema_MemoizedFastPath_StillRepairsStrayLegacyBlockedVocabulary`, adds Core-level coverage of the self-healing path (a stray `'Blocked'` row inserted directly via SQL is still repaired on the next call, memoized fast path included).

  **Correction to this task's own premise, discovered during implementation:** the task brief that requested this refinement attributed Task 2 finding #1 (the 4 failing `tests/LoopRelay.Orchestration.Primitives.Tests` tests) to this exact write transaction perturbing `-wal`/`-shm` side files under WAL. That attribution does not hold up empirically and is a narrower re-reading than finding #1 above already recorded. A minimal reproduction (isolated console program, no test framework) shows: (a) a genuinely empty write transaction (`BEGIN IMMEDIATE`; an `UPDATE ... WHERE` matching zero rows; `COMMIT`) against a WAL database causes **zero** growth in the `-wal` file, both on close and while a concurrent reader is held open to block checkpointing — so the old, unconditional transaction was never actually the mechanism; (b) a single **read-only** connection (`Mode=ReadOnly`) against an already-WAL database is sufficient, by itself, to create a persistent `-wal` (0 bytes) + `-shm` (32768 bytes) pair that a read-only connection can never clean up on close — byte-for-byte identical to what the 4 failing tests observe. Reading `src/LoopRelay.Orchestration.Primitives/Storage/WorkspaceStorageInspector.cs`, `src/LoopRelay.Orchestration.Primitives/Persistence/CanonicalPersistenceReadModel.cs`, and `src/LoopRelay.Orchestration.Primitives/Resolution/RepositoryObserver.cs` confirms none of the 4 failing tests' exercised call paths ever invoke `EnsureSchemaAsync` (they use `InspectSchemaAsync` and other plain `OpenReadOnly` connections throughout) — so this refinement, having verified against the real suite, leaves all 4 tests failing exactly as before (502/506, same 4 named failures). This matches finding #1's own original scoping: the regression is Orchestration.Primitives' own read-only-observation-vs-WAL coordination gap (its own Task ORCH-3), not something a Core-only change can address. Re-confirmed rather than silently accepted: `dotnet test tests/LoopRelay.Orchestration.Primitives.Tests` after this refinement, 502 passed / 4 failed, same 4 names as finding #1.
