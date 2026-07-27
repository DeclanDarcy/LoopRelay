# Filesystem, Repository, Process, and CLI Costs

## Filesystem and workspaces

- Per-test temp workspaces everywhere (constructors create, `IDisposable` recursively deletes). `TempSandboxWorkspaceFactory` (`src/.../Sandboxing/TempSandboxWorkspaceFactory.cs:12-40`) creates `%TEMP%/LoopRelay.Sandbox/{guid}` per call; disposal swallows IO errors. Proportionate; local rebuildable state is correctly treated as disposable (no misclassification as an irreversible external effect was found).
- Delete-retry backoffs for Windows file locks: `CanonicalImportGatewayTests.cs:280` (5 × 50 ms after GC.Collect), `CodexAppServerCertificationTests.cs:269-273` (10 × 100 ms). Bounded cleanup resilience — classified in [07-waits-retries-and-flakiness.md](07-waits-retries-and-flakiness.md) as required synchronization with the OS, not flakiness debt.
- Production `FileSystemArtifactStore` retries transient IO during atomic replace (`:140/:251` maxAttempts=100 × 5 ms) — production behavior exercised, and stress-tested by the legitimate torn-file concurrency test (6.7 s, retained in [10-legitimate-expensive-coverage.md](10-legitimate-expensive-coverage.md)).

## Real processes spawned by the routine pass

| Spawner | What | Bound | Assessment |
|---|---|---|---|
| `GitWorkspace`, `ReadReceiptTests`, `GitEffectExecutorsTests`, `GitObservationTests`, certification runners | real `git init`/status/rev-list, bare remotes | some `WaitForExit()` **untimed** (`GitObservationTests.cs:67`, `OperationPermissionHandlerTests.cs:263`, `RepositoryArtifactStoreTests.cs:155`) | boundary-legitimate (see 10); untimed waits can hang the run on a broken git — noted in 07 |
| `CliSurfaceDependencyTests.cs:155` | spawns the **published CLI** via `dotnet` for the full non-provider command matrix (19.5–21.9 s single test) | `WaitForExitAsync()` | legitimate published-boundary assurance (see 10) |
| `OperationPermissionHandlerTests.cs:246`, `RepositoryArtifactStoreTests.cs:138` | `cmd.exe mklink /J` junctions | untimed | Windows-boundary security coverage; legitimate (see 10) |
| `FullChainLiveRunnerTests` | real `pwsh implement.ps1/verify.ps1` + git, **unconditionally** in the routine pass (6.4 s) | 2-min runner timeouts | modest cost; environment-dependent (pwsh) — retained with a gating note in 10 |
| `ProcessRunnerStderrDrainTests` | pwsh/cmd child processes | 30 s deadlock-guard race | legitimate; guard is a race-loser, not a wait |

CLI bootstrap amplification inside tests is dominated not by process spawns but by in-proc composition — [findings/composition-root-construction-repeated-per-test.md](findings/composition-root-construction-repeated-per-test.md).

## Local-only residue (no repo action)

`tests/LoopRelay.Plan.Cli.Tests/` and `tests/LoopRelay.Roadmap.Cli.Tests/` contain only git-ignored `bin/`/`obj/` from projects deleted at `1bd7797d` — untracked build husks, invisible to the test system. Deleting the local directories is hygiene, not a repository finding (recorded as a rejected candidate in [10-legitimate-expensive-coverage.md](10-legitimate-expensive-coverage.md)). `artifacts/cleanup-publish/` is unrelated published output. The opt-in magnitude harness writes ~62 MB of temp fixtures only when explicitly enabled and best-effort deletes them.
