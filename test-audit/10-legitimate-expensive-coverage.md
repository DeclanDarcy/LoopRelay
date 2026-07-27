# Legitimate Expensive Coverage and Rejected Candidates

## Expensive assurance to retain

| Test / suite | Cost (observed) | Assurance narrower tests cannot provide | Frequency | Safe cost reduction |
|---|---|---|---|---|
| `CliSurfaceDependencyTests.Published_cli_exercises_the_full_non_provider_command_matrix_without_untyped_failures` | 19.5–21.9 s (one test) | The **published** CLI boundary: process start, arg parsing, typed failure surfaces of the shipped binary — in-proc tests cannot see packaging/host regressions | every run (it is the only published-boundary check and there is no CI) | possibly reuse one spawned process for multiple commands if the CLI supports batching; otherwise none without weakening the boundary |
| `GitEffectExecutorsTests` (16.8 s/6), `GitObservationTests.ObserverReportsCleanDirtyDetachedAndAgentsTopologyFromRealGit` (5.3 s), `ReadReceiptTests` git cases | real `git` semantics (status porcelain, rev-list, detached HEAD, gitlinks) | git's actual behavior is the contract; fakes would test the fake | every run | share one initialized repo across cases *within* a class where mutations don't overlap; add hang-guard timeouts to untimed `WaitForExit()` |
| `OperationPermissionHandlerTests` junction/reparse-point denials (0.35 s each), `RepositoryArtifactStoreTests` junction case | Windows reparse-point **security boundary** — deny-write-through-junction is exactly the class of failure fakes cannot reproduce | every run | none needed (sub-second) |
| `FileSystemArtifactStoreTests.ConcurrentWritersAndReadersNeverObserveATornFile` (6.7 s) | real concurrent torn-file safety of the atomic-replace path | every run | none — stress duration is the assurance |
| `CanonicalTransitionPersistenceStoresTests` keyed-read canaries (10–29 s) | O(1)-read regression protection with the perf program as consumer | every run | bulk-seed setup → [findings/perf-canary-seeding-through-production-stores.md](findings/perf-canary-seeding-through-production-stores.md) |
| `FullChainLiveRunnerTests.Independent_repeatability_evidence_executes_two_equivalent_clean_runs` (6.4 s, real pwsh+git) | end-to-end repeatability evidence of the full-chain runner against real processes | every run today; consider aligning with docs/certification.md's stance that live campaigns are operator-run — if moved behind a gate, use honest Skip reporting ([findings/env-gated-noop-tests-report-passed.md](findings/env-gated-noop-tests-report-passed.md)) | none in-place |
| `WorkspaceMagnitudeHarness` (opt-in), `CodexAppServerCertificationTests` live path (opt-in) | scale measurement / live codex certification | opt-in only — correct | make non-execution report as Skipped (same finding) |
| Live certification campaign (`LoopRelay.Certification` executable) | post-epic operator campaign | per docs/certification.md: explicitly **not** part of "run all tests" — correct placement | out of audit scope |

## Rejected candidates (investigated, not findings)

- **`tests/LoopRelay.Plan.Cli.Tests` / `Roadmap.Cli.Tests` "orphan projects"** — appeared to be dead test projects referencing deleted src. Evidence: `git ls-files` empty, `git status --ignored` shows `!!`; only bin/obj husks from projects fully deleted at `1bd7797d`. They are untracked local build residue with zero build/test cost; local hygiene, not a repository finding.
- **`TestSupport` compile-linked file** — a single `MemoryArtifactStore.cs` compiled into 5 assemblies looked like duplicated infrastructure. Cost is a few ms of compilation each; converting to a shared project adds structure for no measurable gain. Proportionate as-is.
- **`coverlet.collector` in every csproj** — suspected passive coverage overhead. Inert without `--collect`; both audited runs show no collector activity. No cost.
- **`ProcessRunnerStderrDrainTests` 30 s delay** — looked like a fixed wait; it is a `WhenAny` race-loser deadlock guard that never elapses on the passing path. Deterministic, correct.
- **Teardown delete-retry loops** — looked like flakiness compensation; they synchronize with Windows file-lock release on real child processes/SQLite handles, are bounded (≤1 s), and have no failure history. Required synchronization.
- **`LiveRunnerDiagnosisIntegrationTests`** — name suggests a live integration suite; it is reflection-only constructor inspection, cheap, spawns nothing. No issue.
- **Certification runner 500 ms polling / 2-min timeouts (src)** — suspected test-wait amplification; these are production certification-campaign cadences, exercised in the routine pass only against in-proc fakes where the polls resolve immediately. Proportionate.
- **Agents.Compatibility.Tests as an "extra project"** — 6 cases, 50–76 ms; the compatibility boundary (manifest caching + opt-in live certification) is distinctly owned and costs nothing in routine passes. Proportionate.
