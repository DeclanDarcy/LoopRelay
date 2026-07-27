# Waits, Polling, Retries, Timeouts, and Flakiness

Headline: the suite has **no test-retry machinery, no repeat attributes, no flakiness-compensation debt, and no history of flake-motivated commits** (git-log sweep: zero matches for flaky/flake/retry-test/serial motives). Fixed waiting totals well under 1 s per run. This is a notably clean waiting profile; the material items are classification, not volume.

Classification of every material wait (evidence: repo-wide sweep, file:line in the findings):

| Site | Construct | Classification |
|---|---|---|
| `CausalUlidTests.cs:44` | `Task.Delay(50)` to cross a ms boundary | Unnecessary delay (injectable clock exists) → [findings/wall-clock-dependent-assertions.md](findings/wall-clock-dependent-assertions.md) |
| `CodexUsageProbeTests.cs:113-118` | `Stopwatch` + `Assert(sw.Elapsed < 5 s)` | Wall-clock threshold assertion (flaky under load) → same finding |
| `CertificationFailureDiagnosisTests.cs:381` | 100 ms timed CTS | Wall-clock-dependent cancellation drive → same finding |
| `InputWaitProgressAgentRuntimeTests.cs:97,126` | `Task.Delay(25)` interleaving | Timing-dependent ordering → same finding |
| `CanonicalImportGatewayTests.cs:280`; `CodexAppServerCertificationTests.cs:269-273` | delete-retry backoffs (5×50 ms / 10×100 ms) | Required synchronization (Windows file-lock release) — retain |
| `ProcessRunnerStderrDrainTests.cs:91` | 30 s `Task.Delay` as `WhenAny` race-loser | Deadlock guard, deterministic — retain |
| `GitObservationTests.cs:67`, `OperationPermissionHandlerTests.cs:263`, `RepositoryArtifactStoreTests.cs:155` | **untimed** `WaitForExit()` on real git/cmd | Required synchronization with missing upper bound — can hang the run on a broken environment; low-cost hardening: add generous timeouts (minutes) as hang guards. Not a canonical finding (no observed cost; single-line hardening) |
| `Task.Delay(Timeout.Infinite, token)` fakes (3 sites) | blocks until cancellation | Deterministic scaffolding — retain |
| Certification runners (src) | 500 ms polling cadence, 2-min process timeouts, nested collection/diagnosis budgets (30 s/10 min) | External-system stabilization in *production* certification code; exercised in tests only against fakes/stubs — proportionate |

Production waiting semantics under test (`UsageLimitDetector` 30-min fallback, retry policies, `TaskDelayScheduler`) are tested via injected clocks and recorded delays — deterministic, zero real waiting; this is the pattern the four wall-clock sites above should adopt.

No test routinely consumes most of its timeout while passing; no readiness probes after synchronous initialization; no globally disabled parallelism *motivated by* flakiness (the two serialized assemblies protect static counters/env state — see [08-parallelization-and-contention.md](08-parallelization-and-contention.md)).

Latent flakiness risks (no observed failures in the audited runs): the four wall-clock sites, plus the env-var mutation hazard under parallelism → [findings/env-var-mutation-in-parallel-assembly.md](findings/env-var-mutation-in-parallel-assembly.md). The single observed failure in the suite is deterministic, not flaky → [findings/status-canary-red-at-head.md](findings/status-canary-red-at-head.md).
