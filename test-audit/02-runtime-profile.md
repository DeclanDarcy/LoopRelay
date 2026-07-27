# Runtime Profile

All measurements taken at revision `9285dc8c` (clean tree) on 2026-07-27, Windows 11 Home 10.0.26200, Intel i9-14900K (24C/32T), 63.7 GB RAM, NVMe SSDs, .NET SDK 10.0.301, Debug configuration, warm NuGet/build caches. Commands:

- Build: `dotnet build LoopRelay.slnx -c Debug` (incremental — binaries pre-existed; cold build **not measured**).
- Tests: `dotnet test LoopRelay.slnx -c Debug --no-build --logger trx` — two full runs (run 1, run 2). Per-test durations parsed from trx. Test assemblies start concurrently (identical trx start stamps); xUnit default collection parallelism applies inside each assembly except where disabled.
- Historical instrumented numbers: the repo's own opt-in `WorkspaceMagnitudeHarness`, recorded 2026-07-27 against `fd8065cd` (git `21c996d6:production-code-performance-audit.md` §12a). Production `src/` is unchanged between `fd8065cd` and HEAD (verified `git diff --stat`), so those numbers apply. The harness itself states run-to-run variance ≈ ±30%.

## Wall clock and aggregate (observed)

| Phase | Run 1 | Run 2 |
|---|---|---|
| Incremental build | 15 s | — |
| Test phase wall clock | 317 s | 244 s |
| Sum of per-test durations (aggregate) | 766.9 s | 805.0 s |
| Cases | 1,535 (1 failed) | 1,535 (1 failed, same test) |

Run-to-run wall variance ≈ ±25–30% under identical conditions — single observations, not percentiles.

## Per-assembly (observed, both runs)

| Assembly | Cases | Aggregate run 1 | Aggregate run 2 | Console wall run 1 | Console wall run 2 |
|---|---|---|---|---|---|
| Cli.Tests | 438 | 474.8 s | 446.4 s | **313 s** | **241 s** |
| Orchestration.Tests | 573 | 214.9 s | 229.1 s | 26 s | 47 s |
| Core.Tests | 103 | 25.2 s | 45.0 s | 25 s | 45 s |
| Infrastructure.Tests | 33 | 17.9 s | 25.0 s | 16 s | 20 s |
| Certification.Tests | 73 | 14.9 s | 20.6 s | 6 s | 7 s |
| Completion.Tests | 41 | 8.6 s | 25.7 s | 4 s | 13 s |
| Agents.Tests | 154 | 7.5 s | 8.8 s | 8 s | 9 s |
| Projections.Tests | 14 | 1.8 s | 2.8 s | 1 s | 2 s |
| Permissions.Tests | 94 | 1.2 s | 1.0 s | 0.5 s | 0.3 s |
| Application.Tests | 6 | 0.1 s | 0.5 s | 0.1 s | 0.3 s |
| Agents.Compatibility.Tests | 6 | 0.1 s | 0.1 s | 0.05 s | 0.05 s |

**Critical path (observed, both runs): the suite's wall clock equals `Cli.Tests`' wall, which equals the summed duration of the single class `LoopRelayCompositionRootTests`** (44 cases: 313.6 s in run 1 vs 313 s assembly wall; 241.3 s in run 2 vs 241 s assembly wall). One xUnit class = one collection = strictly serial. Everything else in the repository finishes at least 4× sooner. Second-longest serial chain: `UnifiedCliRunnerTests` (80.5 s / 92.3 s, one class).

Core.Tests and Agents.Tests aggregate ≈ wall — fully serialized by their `xunit.runner.json` (`maxParallelThreads:1`); Orchestration.Tests achieves ~5–8× internal parallelism.

## Cost concentration (observed, run 1)

| Duration bucket | Cases | Sum |
|---|---|---|
| < 10 ms | 851 | 1.4 s |
| 10–100 ms | 267 | 9.3 s |
| 100 ms–1 s | 282 | 99.8 s |
| 1–5 s | 101 | 229.0 s |
| ≥ 5 s | 34 | **427.6 s (56% of aggregate)** |

Slowest classes by aggregate (run 1 → run 2): `LoopRelayCompositionRootTests` 313.6→241.3 s (n=44); `UnifiedCliRunnerTests` 80.5→92.3 s (n=41); `CanonicalTransitionPersistenceStoresTests` 26.7→47.4 s (n=12); `CliSurfaceDependencyTests` 19.6→21.9 s (n=32); `SessionTelemetryCompositionTests` 18.3 s (n=3); `GitEffectExecutorsTests` 16.8 s (n=6); `CanonicalWorkflowPersistenceStoreTests` 14.7→20.8 s (n=30); `LoopRelayWorkspaceDatabaseSchemaV9Tests` 9.3→27.9 s (n=34). Slowest single tests: `Plan_workflow_transitions_run_through_canonical_runtime` 47.4/54.8 s; `Execute_workflow_transitions...` 30.7/33.6 s; `ReadTransitionRunAsync_reads_exactly_one_row_no_matter_how_large_the_history_table_is` 10.1/28.8 s (high variance). Full CSVs retained in the session scratchpad (`per-test-durations.csv`); not committed, reproducible via the commands above.

## Unit-cost decomposition (observed via the repo's own instrumented harness, `fd8065cd`, same production source)

- First `EnsureSchemaAsync` on a **new database file** (creates 79 tables + 148 indexes and fully verifies): **264 SELECT statements, ~478 ms**. `ShapeRequirementProbes` +182 and `FullVerificationRuns` +1 **per database created**, 0 per subsequent open.
- Re-open of an already-verified path: 12 statements, 3.6 ms; warm steady-state open: **0.95 ms** (mean of 20).
- Steady-state store operation: **~58–60 ms each** (18 store ops per attempt ≈ 1.05–1.09 s/attempt, flat across 100× history growth). The deleted audit's stated hypothesis (explicitly *not measured*): dominated by one synchronous rollback-journal flush per write transaction (`Pooling=false`, no WAL, per-op connection open).
- Empty-schema floor: 1,003,520 bytes per workspace DB.
- `ProjectAsync` grows linearly with history (4.2 ms at N=10² → 95.6 ms at N=10⁴); `PersistStateAsync` flat.

**Strong inference from these unit costs**: a 44-test class that creates fresh workspace DBs (~0.5 s each) and drives full workflow transitions (~1 s per attempt in store ops alone, plus Deep-tier observations at 182 probes + `foreign_key_check` per observation, plus artifact I/O and per-test `CreateForTests` DI-graph construction with SHA-256 hashing and settings loads) plausibly accounts for the observed 5–55 s per test. Exact per-test decomposition is **not measured** — the store connection exposes no observer seam (documented gap; see [12-measurement-gaps.md](12-measurement-gaps.md)).

## Overheads (separated)

- Runner/orchestration: assemblies launch concurrently; per-assembly host start + discovery ≈ console wall minus aggregate/parallelism — small (sub-seconds to a few seconds per assembly) relative to the 241–313 s critical path; not separately instrumented.
- Coverage: coverlet referenced, not active without `--collect` — zero cost in these runs.
- Logging/artifacts: trx logging added by this audit's commands, negligible.
- Fixed waits in tests: bounded and small (50 ms ULID delay, 25 ms interleaves, teardown delete-retry backoffs) — see [07-waits-retries-and-flakiness.md](07-waits-retries-and-flakiness.md); no material fixed-wait total.
- Failure: `StatusCanaryTests.CoverageLedgerIsProductionDerivedAndKeepsUncoveredSetVisible` fails deterministically at HEAD (full run ×2 and isolated rerun, 451 ms) — see [findings/status-canary-red-at-head.md](findings/status-canary-red-at-head.md).

## Limitations

- Two full-run samples; individual test durations vary up to ~3× between runs under concurrent assembly load (e.g., the one-row-read canary 10.1→28.8 s). Rankings are stable; single figures are not precise.
- Cold build, discovery-only time, and Release configuration were not measured.
- Per-operation statement counts inside composition-root tests are unmeasurable without a production seam (`CanonicalWorkflowPersistenceStore.OpenAsync` exposes no observer) — recorded as a measurement gap, not estimated.
