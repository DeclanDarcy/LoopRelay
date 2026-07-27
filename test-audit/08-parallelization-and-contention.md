# Parallelization and Contention

Scheduling reality at HEAD: test assemblies start concurrently; within assemblies, xUnit default collection parallelism applies except Core.Tests and Agents.Tests (whole-assembly single-thread via `xunit.runner.json`). The suite's wall clock is set entirely by one serial collection ([findings/cli-monolith-classes-serialize-suite-critical-path.md](findings/cli-monolith-classes-serialize-suite-critical-path.md)); machine parallelism (32 threads) is idle for most of the run.

Classification:

| Group | Class | Basis |
|---|---|---|
| `LoopRelayCompositionRootTests`, `UnifiedCliRunnerTests` (Cli.Tests) | **Incorrectly serialized** (by single-class structure, not by need) | per-test temp isolation; no shared state found; → Relocate finding |
| Core.Tests, Agents.Tests whole assemblies | **Incorrectly serialized** (over-broad mechanism) | shared state already guarded by `DisableParallelization` collections → [findings/assembly-serialization-redundant-with-collections.md](findings/assembly-serialization-redundant-with-collections.md) |
| `"WorkspaceDatabaseCounters"` (4 classes), `"ProcessEnvironment"` (1 class) | **Intentionally serialized — correct** | process-wide static counters / env+CWD mutation; documented in-code |
| `SessionTelemetryRecorderTests` (Cli.Tests) | **Parallelizable after specific isolation change** (needs a serialized collection) | mutates process env in a parallel assembly → [findings/env-var-mutation-in-parallel-assembly.md](findings/env-var-mutation-in-parallel-assembly.md) |
| Orchestration.Tests (573 cases) | **Safely parallel now** (demonstrated) | 214–229 s aggregate in 26–47 s wall against file SQLite/temp dirs |
| Perf canaries with large seeding | **Better optimized individually before parallelization** | contention-sensitive (3× duration variance observed) → seeding finding |
| Remaining assemblies | Safely parallel now | per-test temp state; observed clean under both runs |

Contention observations (measured): run-to-run assembly walls swing ±25–100% under concurrent assembly load (Core.Tests 25→45 s, Orchestration 26→47 s, the one-row-read canary 10.1→28.8 s) — CPU/IO contention among 11 concurrent test hosts on shared NVMe, not lock contention (no SQLite busy failures observed; busy_timeout=10000). Consequence for remediation ordering: parallelism must not be used to conceal the per-test setup waste — the lifecycle findings (template DB, seeding) reduce aggregate work; the split findings reduce idle time. Both are needed; neither substitutes for the other.

No sharding/orchestration layer exists (no CI); no ports, no network listeners, no `Directory.SetCurrentDirectory` in tests; shared log sinks are per-test files. Order dependence: none observed in two full runs plus isolated reruns; the split remediation's validation plans include repeated-run checks to smoke out latent coupling currently masked by serial execution.
