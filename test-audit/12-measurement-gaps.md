# Measurement Gaps

Gaps that materially block an in-scope decision, with the exact experiment that closes each.

## 1. Per-test decomposition inside the composition-root chain

- Question: how do the 5–55 s composition-root tests split between store operations, Deep-tier observations, composition construction, artifact I/O, and git?
- Missing: per-operation counts/timings within a single test; the store connection exposes no observer seam (`CanonicalWorkflowPersistenceStore.OpenAsync` — constraint documented by the prior audit; closing it fully requires mirroring `CanonicalEffectWorkStore.ConnectionObserverForTesting`, a production change deliberately not made).
- Suspected amplification: store-op count × ~58 ms dominating, Deep observations second.
- Minimal experiment: sample one filtered test (`dotnet test --filter "…Plan_workflow_transitions…"`) under `dotnet-trace` (SampleProfiler, ~60 s capture); attribute inclusive time to `SqliteConnection.Open`/`Commit`, `WorkspaceStorageInspector`, `CreateForTests`. Context: same machine/config as [02-runtime-profile.md](02-runtime-profile.md); single-test isolation removes contention noise.
- Decision enabled: ranking between the template-DB, Deep-tier, and construction-caching remediations; go/no-go on [findings/composition-root-construction-repeated-per-test.md](findings/composition-root-construction-repeated-per-test.md).

## 2. Fresh-database creations per full run

- Question: exactly how many ~478 ms first-contact creations does one full pass pay?
- Missing: a per-run count; the static `FullVerificationRuns` counter exists but is per test-host process and unreported.
- Suspected: 300–500 (estimate in [findings/fresh-database-schema-creation-per-test.md](findings/fresh-database-schema-creation-per-test.md)).
- Minimal experiment: temporary diagnostic — after a full run, sum `FullVerificationRuns` per assembly via a trailing test or a one-off local instrumentation (throwaway, reverted); alternatively count distinct `looprelay.db` paths created under %TEMP% during a run with a filesystem watch. Units: creations/run per assembly.
- Decision enabled: sizes the template-copy remediation's payoff precisely (validates or corrects the 150–240 s estimate).

## 3. WAL/pooling residual (production)

- Question: is the ~58 ms store-op floor journal-flush-bound?
- Missing: the WAL-vs-rollback comparison the prior audit specified but did not run.
- Experiment: re-run harness M1 fixtures with `PRAGMA journal_mode=WAL` / `synchronous=NORMAL` vs baseline; per-write-transaction wall, same machine/fixtures, discard first attempt. (Requires a temporary local pragma toggle — production change territory; belongs to ORCH-3's owner.)
- Decision enabled: [findings/store-operation-durability-cost-no-wal-no-pooling.md](findings/store-operation-durability-cost-no-wal-no-pooling.md) go/no-go, unless the event-sourced direction moots it first.

## 4. `CreateForTests` per-call cost

- Question: milliseconds or seconds? (Gap 1's profile answers this as a by-product; listed separately because it alone gates a finding.)
- Experiment: stopwatch split at `CreateForTests` return across the 44 cases (temporary local instrumentation, reverted); report mean/median/range.
- Decision enabled: whether process-level caching of validated catalog + hashes is worth designing at all.

## 5. Post-split contention profile

- Question: after splitting the monolith classes, where does Cli.Tests' wall settle, and does NVMe/CPU contention erode the gain?
- Missing: any parallel-execution measurement of those 85 cases (never run concurrently to date).
- Experiment: after the split lands, two timed full runs + one isolated Cli.Tests run; compare against the 90–120 s estimate; watch for busy_timeout incidents in output.
- Decision enabled: whether further splitting or per-class balancing is warranted.

Not listed: cold-build time, Release-config timings, discovery overhead — measurable but no in-scope decision depends on them.
