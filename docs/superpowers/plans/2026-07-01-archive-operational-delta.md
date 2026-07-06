# Archive operational_delta.md on Successful Context Update — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** After a Transfer successfully updates `operational_context.md`, rotate the consumed `.agents/operational_delta.md` into a numbered `.agents/deltas/operational_delta.NNNN.md` archive and remove the live file — in both the backend orchestrator and the CLI loop.

**Architecture:** Two independent Transfer implementations fold a delta into the operational context and today leave the live delta lingering. Add shared numbered-archive path helpers, then a rotate-then-delete step at the point each site has persisted the evolved context — strict (a failed archive fails the transfer). Backend uses the existing `IArtifactStore` + `HighestSequence` rotation idiom; CLI reuses the existing `LoopArtifacts.RotateAsync` move-semantics helper.

**Tech Stack:** C# / .NET, xUnit. Backend: `LoopRelay.Orchestration`, `LoopRelay.Backend.Tests`. CLI: `LoopRelay.CLI`, `LoopRelay.CLI.Tests`.

## Global Constraints

- **Archive location:** `.agents/deltas/operational_delta.NNNN.md` (zero-padded 4-digit, monotonic per repo, disk-derived so it is restart-safe).
- **Archive semantics:** move — write the numbered copy, then delete the live `.agents/operational_delta.md`.
- **Timing:** immediately after the evolved `operational_context.md` is written back (and health recorded), BEFORE the fresh Decision process reseeds.
- **Failure handling:** strict. Backend publishes a `failed` frame with `phase = "ArchiveOperationalDelta"` and returns `false`. CLI throws (`LoopStepException` on a missing delta; store exceptions propagate) so the loop step fails.
- **Do NOT** change the `transferred` stream event payload (it reports the artifact *identity* `.agents/operational_delta.md`, not the archived instance) — this keeps `decision-stream.golden.json` and `OrchestrationStreamContractTests` untouched.
- **Do NOT** extend `ArtifactRotationService` or the Core artifact taxonomy.
- **Commits:** the repo owner typically leaves work uncommitted for review. Commit steps are included per task per the plan format, but may be deferred at the user's discretion.
- **Build/test note (from project memory):** the Debug app may be running; run backend tests with `-c Release` to avoid file locks.

---

### Task 1: Shared archive path helpers

**Files:**
- Modify: `src/LoopRelay.Orchestration/OrchestrationArtifactPaths.cs`
- Test: `tests/LoopRelay.Backend.Tests/OrchestrationArtifactProtocolTests.cs` (extend the canonical-paths test at ~line 173)

**Interfaces:**
- Produces: `OrchestrationArtifactPaths.DeltasDirectory` (`".agents/deltas"`), `OrchestrationArtifactPaths.HistoricalDeltaSearchPattern` (`"operational_delta.*.md"`), `OrchestrationArtifactPaths.HistoricalDelta(int sequence) => ".agents/deltas/operational_delta.NNNN.md"`.

- [ ] **Step 1: Write the failing test** — add these assertions to the body of `All_seven_canonical_artifacts_resolve_to_their_documented_repository_relative_paths` in `OrchestrationArtifactProtocolTests.cs` (after the existing `Assert.Equal(".agents/operational_delta.md", OrchestrationArtifactPaths.OperationalDelta);` line):

```csharp
        Assert.Equal(".agents/deltas", OrchestrationArtifactPaths.DeltasDirectory);
        Assert.Equal(".agents/deltas/operational_delta.0001.md", OrchestrationArtifactPaths.HistoricalDelta(1));
        Assert.Equal(".agents/deltas/operational_delta.0042.md", OrchestrationArtifactPaths.HistoricalDelta(42));
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/LoopRelay.Backend.Tests -c Release --filter "FullyQualifiedName~OrchestrationArtifactProtocolTests.All_seven_canonical_artifacts"`
Expected: FAIL — compile error, `DeltasDirectory` / `HistoricalDelta` are not defined.

- [ ] **Step 3: Write minimal implementation** — in `OrchestrationArtifactPaths.cs`, immediately after the `OperationalDelta` constant (line 26), add:

```csharp
    /// <summary>Directory holding the archived operational deltas rotated out of the live <see cref="OperationalDelta"/>
    /// once a Transfer has folded each into the next <see cref="OperationalContext"/> revision (numbered history).</summary>
    public const string DeltasDirectory = ".agents/deltas";

    /// <summary>Glob matching the archived operational deltas (<c>operational_delta.0001.md</c>, ...) under
    /// <see cref="DeltasDirectory"/> but NOT the live single-dot <see cref="OperationalDelta"/>.</summary>
    public const string HistoricalDeltaSearchPattern = "operational_delta.*.md";

    /// <summary>Archived operational-delta path: <c>.agents/deltas/operational_delta.0001.md</c>, ... Each successful
    /// Transfer rotates the consumed live delta here (run-scoped 4-digit counter) after the context update succeeds.</summary>
    public static string HistoricalDelta(int sequence) => $".agents/deltas/operational_delta.{sequence:0000}.md";
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/LoopRelay.Backend.Tests -c Release --filter "FullyQualifiedName~OrchestrationArtifactProtocolTests.All_seven_canonical_artifacts"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/LoopRelay.Orchestration/OrchestrationArtifactPaths.cs tests/LoopRelay.Backend.Tests/OrchestrationArtifactProtocolTests.cs
git commit -m "feat: add .agents/deltas archive path helpers"
```

---

### Task 2: Backend orchestrator archives the delta

**Files:**
- Modify: `src/LoopRelay.Orchestration/Services/RepositoryOrchestrator.cs` (add helpers near `NextHandoffSequenceAsync` ~line 1359; insert the archive step in `PrepareTransferAsync` after `string newContext = evolvedContext;` ~line 1664)
- Test: `tests/LoopRelay.Backend.Tests/Orchestration/RepositoryOrchestratorTransferTests.cs`

**Interfaces:**
- Consumes: `OrchestrationArtifactPaths.HistoricalDelta`, `.DeltasDirectory`, `.HistoricalDeltaSearchPattern` (Task 1); existing `HighestSequence(IReadOnlyList<string>)`, `artifactStore`, `ArtifactPath.ResolveRepositoryPath`, `DecisionStream.Publish`, `Serialize`.
- Produces: private `Task ArchiveOperationalDeltaAsync(Repository repository)`, private `Task<int> NextDeltaSequenceAsync(Repository repository)`; a new `"ArchiveOperationalDelta"` phase in the transfer stream between `UpdateOperationalContext` and `StartDecisionSessionFromTransfer`.

- [ ] **Step 1: Write the failing tests** — add three tests to `RepositoryOrchestratorTransferTests.cs` (they use the file's existing helpers `SeedLoopAsync`, `SeedWarmDecisionSessionAsync`, `ScriptTransferTurns`, `Resolve`, `DrainDecisionTerminalsAsync`, `Field`):

```csharp
    [Fact]
    public async Task Transfer_archives_the_operational_delta_after_the_context_update()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "OPERATIONAL DELTA", rewrittenContext: "REWRITTEN", proposal: "NEXT");

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        // The consumed delta was rotated into the numbered archive and the live file removed.
        Assert.Equal("OPERATIONAL DELTA", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDelta(1))));
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)));
    }

    [Fact]
    public async Task Successive_transfers_archive_deltas_with_a_monotonic_sequence()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);

        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "DELTA ONE", rewrittenContext: "CTX1", proposal: "P1");
        await orchestrator.BeginSubmitDecisionsAsync(repository, "D1");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "DELTA TWO", rewrittenContext: "CTX2", proposal: "P2");
        await orchestrator.BeginSubmitDecisionsAsync(repository, "D2");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        Assert.Equal("DELTA ONE", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDelta(1))));
        Assert.Equal("DELTA TWO", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDelta(2))));
        Assert.False(await store.ExistsAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta)));
    }

    [Fact]
    public async Task Transfer_failed_delta_archive_fails_the_transfer_and_does_not_propose()
    {
        var runtime = new FakeAgentRuntime();
        var store = new FakeArtifactStore();
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new FakeDecisionSessionRouter { Route = DecisionRoute.Transfer };
        Repository repository = OrchestrationTestFactory.Repository();
        RepositoryOrchestrator orchestrator = OrchestrationTestFactory.Orchestrator(runtime: runtime, store: store, router: router, sandbox: sandbox);

        await SeedLoopAsync(orchestrator, store, repository);
        await SeedWarmDecisionSessionAsync(orchestrator, runtime, repository);
        ScriptTransferTurns(runtime, store, repository, sandbox, delta: "DELTA", rewrittenContext: "CTX2", proposal: "NEXT");
        // Force the archive write into .agents/deltas to fail.
        store.FailWriteOn = path => path.Contains("deltas", StringComparison.OrdinalIgnoreCase);

        await orchestrator.BeginSubmitDecisionsAsync(repository, "DECISIONS ONE");
        await orchestrator.ExecutionRunTask;
        await orchestrator.DecisionRunTask;

        OrchestratorStreamEvent failed = (await DrainDecisionTerminalsAsync(orchestrator.DecisionStream, 2))[^1];
        Assert.Equal("failed", failed.Type);
        Assert.Equal("ArchiveOperationalDelta", Field(failed, "phase"));
        // The context update already succeeded, but the transfer failed at the archive: no fresh proposal.
        Assert.Equal("CTX2", await store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalContext)));
        Assert.Equal("DECISIONS ONE", orchestrator.CurrentDecisions);
        Assert.False(orchestrator.IsDecisionRunActive);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/LoopRelay.Backend.Tests -c Release --filter "FullyQualifiedName~RepositoryOrchestratorTransferTests.Transfer_archives_the_operational_delta_after_the_context_update|FullyQualifiedName~RepositoryOrchestratorTransferTests.Successive_transfers_archive|FullyQualifiedName~RepositoryOrchestratorTransferTests.Transfer_failed_delta_archive"`
Expected: FAIL — archive never happens, so `HistoricalDelta(1)` reads null and the live delta still exists; no `ArchiveOperationalDelta` failed frame.

- [ ] **Step 3: Write the implementation**

3a. In `RepositoryOrchestrator.cs`, add these two private methods next to `NextHandoffSequenceAsync` (~line 1365):

```csharp
    // Next archived-delta number = (highest existing .agents/deltas/operational_delta.000N.md) + 1, from DISK
    // (restart-safe), mirroring NextHandoffSequenceAsync/NextDecisionSequenceAsync so the counter is monotonic
    // per repo across runs.
    private async Task<int> NextDeltaSequenceAsync(Repository repository)
    {
        IReadOnlyList<string> existing = await artifactStore.ListAsync(
            ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.DeltasDirectory),
            OrchestrationArtifactPaths.HistoricalDeltaSearchPattern).ConfigureAwait(false);
        return HighestSequence(existing) + 1;
    }

    // Archive the consumed operational delta once the context evolution has succeeded (m7): the live
    // .agents/operational_delta.md has now been folded into operational_context.md, so it is rotated into a
    // numbered .agents/deltas/ copy and the live file removed — no stale "pending" delta lingers. Move semantics
    // (write numbered, then delete live), matching the handoff/decision rotation. A missing live delta or a failed
    // store write/delete throws; PrepareTransferAsync treats that as a hard transfer failure.
    private async Task ArchiveOperationalDeltaAsync(Repository repository)
    {
        string livePath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.OperationalDelta);
        string? content = await artifactStore.ReadAsync(livePath).ConfigureAwait(false);
        if (content is null)
        {
            throw new InvalidOperationException(
                "Cannot archive the operational delta: .agents/operational_delta.md does not exist after the context update.");
        }

        int sequence = await NextDeltaSequenceAsync(repository).ConfigureAwait(false);
        await artifactStore.WriteAsync(
            ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.HistoricalDelta(sequence)),
            content).ConfigureAwait(false);
        await artifactStore.DeleteAsync(livePath).ConfigureAwait(false);
    }
```

3b. In `PrepareTransferAsync`, immediately after `string newContext = evolvedContext;` (~line 1664) and before the `// 4) Open a FRESH Decision process` comment, insert:

```csharp
        // 3.5) Archive the consumed operational delta now that operational_context.md is successfully updated:
        // rotate .agents/operational_delta.md into a numbered .agents/deltas/ copy and remove the live file. Hard
        // step — a failed archive fails the transfer (before the fresh process is opened).
        DecisionStream.Publish("phase", Serialize(new { phase = "ArchiveOperationalDelta" }));
        try
        {
            await ArchiveOperationalDeltaAsync(repository).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DecisionStream.Publish("failed", Serialize(new
            {
                phase = "ArchiveOperationalDelta",
                reason = "Failed to archive the operational delta after the context update.",
                detail = ex.Message,
            }));
            return false;
        }
```

- [ ] **Step 4: Update the existing tests broken by delta deletion**

The live delta no longer exists after a *successful* transfer, so these assertions must read the archived path instead. Change each listed line:

In `RepositoryOrchestratorTransferTests.cs`:
- Line ~50 (`Transfer_extracts_a_delta...`): change `store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.OperationalDelta))` → `store.ReadAsync(Resolve(repository, OrchestrationArtifactPaths.HistoricalDelta(1)))` (expected value `"OPERATIONAL DELTA"` unchanged).
- Line ~91 (`The_real_router_transfers_when_occupancy...`): same swap, expected `"DELTA"`.
- Line ~125 (`The_real_router_transfers_economically...`): same swap, expected `"DELTA"`.
- Line ~532 (`The_loop_continues_through_reuse_then_transfer`): same swap, expected `"DELTA TWO"` at `HistoricalDelta(1)` (only iteration 2 transfers, so it is the first archived delta).
- Line ~698 (`Recycle_resets_the_per_process_cost_accounting...`): same swap, expected `"DELTA"` at `HistoricalDelta(1)` (only iteration 1 transfers).
- Line ~294 (`Transfer_streams_its_phases...`): insert `"ArchiveOperationalDelta"` into the expected phase array between `"UpdateOperationalContext"` and `"StartDecisionSessionFromTransfer"`, so it reads:

```csharp
            new[] { "ProduceOperationalDelta", "UpdateOperationalContext", "ArchiveOperationalDelta", "StartDecisionSessionFromTransfer", "GetNextDecisions" },
```

In `tests/LoopRelay.Backend.Tests/OrchestrationArtifactProtocolTests.cs`:
- Line ~168 (`Transfer_writes_operational_delta_and_rewrites_operational_context`): change the `ReadAsync(...OperationalDelta)` assertion to `HistoricalDelta(1)` (expected `"OPERATIONAL DELTA"`). Leave line ~167 (`Assert.Contains(...OperationalDelta, store.WriteQueries)`) as-is — the live delta is still written first.

In `tests/LoopRelay.Backend.Tests/Orchestration/RepositoryOrchestratorFeatureFlagsTests.cs`:
- Line ~209 (`Transfer_only_fallback_forces_transfer...`): change the `ReadAsync(...OperationalDelta)` assertion to `HistoricalDelta(1)` (expected `"DELTA"`).

> Leave UNCHANGED (verified correct): `RepositoryOrchestratorTransferTests` lines ~455 (rewrite fails → archive never runs → live delta retained) and all `Assert.False(...ExistsAsync(...OperationalDelta))` cases (368/396/422/609 — never transferred); `OrchestrationRecoveryCertificationTests` line ~331 (context update fails → archive never runs); all provenance/identity/sandbox-delta assertions; `OrchestrationStreamContractTests` line ~98 (the `transferred` event identity payload, deliberately unchanged).

- [ ] **Step 5: Run the full backend orchestration suite to verify green**

Run: `dotnet test tests/LoopRelay.Backend.Tests -c Release --filter "FullyQualifiedName~Orchestration"`
Expected: PASS (new tests green; updated tests green; nothing else regressed).

- [ ] **Step 6: Commit**

```bash
git add src/LoopRelay.Orchestration/Services/RepositoryOrchestrator.cs tests/LoopRelay.Backend.Tests
git commit -m "feat: archive operational delta after backend transfer context update"
```

---

### Task 3: CLI `LoopArtifacts.RotateOperationalDeltaAsync`

**Files:**
- Modify: `src/LoopRelay.CLI/LoopArtifacts.cs` (add after `RotateLiveDecisionsAsync`, ~line 45)
- Test: `tests/LoopRelay.CLI.Tests/LoopArtifactsTests.cs`

**Interfaces:**
- Consumes: Task 1 path helpers; existing private `LoopArtifacts.RotateAsync(liveRelative, directoryRelative, searchPattern, baseName, historical)`.
- Produces: `public Task<string?> RotateOperationalDeltaAsync()` — returns the archived content, or `null` if no live delta existed.

- [ ] **Step 1: Write the failing tests** — add to `LoopArtifactsTests.cs` (uses the file's `New()` and `Resolve(r, rel)` helpers):

```csharp
    [Fact]
    public async Task RotateOperationalDelta_ArchivesNumberedAndDeletesLive()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta), "DELTA-A");

        string? rotated = await art.RotateOperationalDeltaAsync();

        Assert.Equal("DELTA-A", rotated);
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal("DELTA-A", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(1))));
    }

    [Fact]
    public async Task RotateOperationalDelta_WhenAbsent_ReturnsNull()
    {
        var (art, _, _) = New();
        Assert.Null(await art.RotateOperationalDeltaAsync());
    }

    [Fact]
    public async Task RotateOperationalDelta_SequenceIsDiskMaxPlusOne()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(1)), "old");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(2)), "old2");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta), "DELTA-3");

        await art.RotateOperationalDeltaAsync();

        Assert.Equal("DELTA-3", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(3))));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/LoopRelay.CLI.Tests -c Release --filter "FullyQualifiedName~LoopArtifactsTests.RotateOperationalDelta"`
Expected: FAIL — compile error, `RotateOperationalDeltaAsync` is not defined.

- [ ] **Step 3: Write minimal implementation** — in `LoopArtifacts.cs`, after `RotateLiveDecisionsAsync` (~line 45), add:

```csharp
    public Task<string?> RotateOperationalDeltaAsync() => RotateAsync(
        OrchestrationArtifactPaths.OperationalDelta,
        OrchestrationArtifactPaths.DeltasDirectory,
        OrchestrationArtifactPaths.HistoricalDeltaSearchPattern,
        "operational_delta",
        OrchestrationArtifactPaths.HistoricalDelta);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/LoopRelay.CLI.Tests -c Release --filter "FullyQualifiedName~LoopArtifactsTests.RotateOperationalDelta"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/LoopRelay.CLI/LoopArtifacts.cs tests/LoopRelay.CLI.Tests/LoopArtifactsTests.cs
git commit -m "feat: add LoopArtifacts.RotateOperationalDeltaAsync move-rotation"
```

---

### Task 4: CLI `DecisionSession.TransferAsync` archives the delta

**Files:**
- Modify: `src/LoopRelay.CLI/DecisionSession.cs` (insert in `TransferAsync` after the `EvolveOperationalContextAsync` call, ~line 148, before opening the fresh session ~line 151)
- Test: `tests/LoopRelay.CLI.Tests/DecisionSessionTests.cs`

**Interfaces:**
- Consumes: Task 3 `artifacts.RotateOperationalDeltaAsync()`; existing `console.Phase`, `LoopStepException`.
- Produces: an `"Decision: Transfer/ArchiveOperationalDelta"` phase + strict archive between context update and reseed.

- [ ] **Step 1: Write the failing test** — add to `DecisionSessionTests.cs`. Model the setup on the existing `Run_Transfer_EvolvesInSandbox_ThenCopiesBackAndCleansUp` (explicit `MemoryArtifactStore` + `LoopArtifacts` + `FakeAgentRuntime` + `FakeSandboxWorkspaceFactory` + small-window router). Also define a throwing decorator near the bottom of the file (mirrors `CountingStore` in `MilestoneGateTests.cs`) for the failure test:

```csharp
    [Fact]
    public async Task Run_Transfer_ArchivesTheDelta_AndRemovesTheLiveFile()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        var session = new DecisionSession(rt, router, art, con, repo, costModel: null, sandboxFactory: sandbox);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        // Round 1: seed + propose (occupancy 20 -> round 2 crosses the guard).
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        // Round 2: Transfer.
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));   // ProduceOperationalDelta
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>                                     // UpdateOperationalContext
        {
            s.WriteAsync(sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("reseeded")));      // reseed
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("D2")));            // propose
        await session.RunAsync(CancellationToken.None);

        // The delta was archived into .agents/deltas and the live file removed.
        Assert.Equal("DELTA-TEXT", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(1))));
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal("OPCTX-1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext)));
    }

    [Fact]
    public async Task Run_Transfer_FailedDeltaArchive_FailsTheTransfer()
    {
        var inner = new MemoryArtifactStore();
        var store = new ThrowOnDeltaArchiveStore(inner);
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var art = new LoopArtifacts(store, repo);
        var con = new RecordingLoopConsole();
        var rt = new FakeAgentRuntime(store);
        var sandbox = new FakeSandboxWorkspaceFactory();
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(ModelContextWindowTokens: 22, CapacityGuardFraction: 0.90));
        var session = new DecisionSession(rt, router, art, con, repo, costModel: null, sandboxFactory: sandbox);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "OPCTX-0");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("seeded")));
        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "D1", new AgentTokenUsage(10, 10))));
        await session.RunAsync(CancellationToken.None);

        rt.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => Turns.Completed("DELTA-TEXT")));
        rt.OneShotTurns.Enqueue(new ScriptedTurn((_, _, s) =>
        {
            s.WriteAsync(sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext), "OPCTX-1").Wait();
            return Turns.Completed("updated");
        }));
        // The reseed/propose turns are never reached because the archive throws first.

        await Assert.ThrowsAsync<LoopStepException>(() => session.RunAsync(CancellationToken.None));

        // The context update succeeded before the archive failed.
        Assert.Equal("OPCTX-1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext)));
    }
```

And add this test double at the bottom of the file (after the last test class), mirroring `CountingStore`:

```csharp
/// <summary>Forwards to an inner store but throws when a write targets the .agents/deltas archive — models a
/// failed delta archive so the strict transfer-fail path can be exercised.</summary>
internal sealed class ThrowOnDeltaArchiveStore(IArtifactStore inner) : IArtifactStore
{
    public Task<bool> ExistsAsync(string path) => inner.ExistsAsync(path);
    public Task<string?> ReadAsync(string path) => inner.ReadAsync(path);
    public Task WriteAsync(string path, string content) =>
        path.Replace('\\', '/').Contains("/deltas/", StringComparison.OrdinalIgnoreCase)
            ? throw new IOException("Configured archive write failure.")
            : inner.WriteAsync(path, content);
    public Task DeleteAsync(string path) => inner.DeleteAsync(path);
    public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern) => inner.ListAsync(path, searchPattern);
    public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path) => inner.ListDirectoriesAsync(path);
}
```

> Verify `IArtifactStore`'s member list when typing the decorator — implement exactly the interface methods (add any this project's interface declares that are missing above, forwarding to `inner`).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/LoopRelay.CLI.Tests -c Release --filter "FullyQualifiedName~DecisionSessionTests.Run_Transfer_ArchivesTheDelta|FullyQualifiedName~DecisionSessionTests.Run_Transfer_FailedDeltaArchive"`
Expected: FAIL — archive not wired yet: `HistoricalDelta(1)` is null / live delta still present; the failure test does not throw.

- [ ] **Step 3: Write the implementation** — in `DecisionSession.cs` `TransferAsync`, immediately after the `EvolveOperationalContextAsync` call (`(AgentTurnResult update, string newContext) = await EvolveOperationalContextAsync(...)`, ~line 148) and before `session = await runtime.OpenSessionAsync(...)` (~line 151), insert:

```csharp
        // Archive the consumed operational delta now that operational_context.md is successfully updated: rotate
        // .agents/operational_delta.md into a numbered .agents/deltas/ copy and remove the live file. Hard step —
        // a missing delta or a failed rotation fails the transfer (the old process is already closed above; no
        // session is open to tear down here).
        console.Phase("Decision: Transfer/ArchiveOperationalDelta");
        if (await artifacts.RotateOperationalDeltaAsync() is null)
        {
            throw new LoopStepException("Transfer produced no operational_delta.md to archive.");
        }
```

- [ ] **Step 4: Update the existing CLI tests broken by delta deletion**

In `DecisionSessionTests.cs`, these read the live delta after a *successful* transfer — swap to the archived path:
- Line ~177 (`Run_CapacityGuard...RecyclesViaTransfer`): `store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta))` → `...HistoricalDelta(1)` (expected `"DELTA-TEXT"`).
- Line ~211 (`Run_EconomicMarginalRule_RecyclesViaTransfer`): same swap, expected `"DELTA-TEXT"`.
- Line ~312 (`Run_Transfer_EvolvesInSandbox_ThenCopiesBackAndCleansUp`): same swap, expected `"DELTA-TEXT"`.

> Leave UNCHANGED (verified correct): line ~252 (`Run_SubThresholdProposals...` — no transfer, asserts the delta never existed); line ~288 (reads the *sandbox* delta, not the repo live delta); `Run_RepeatedGrowingTransfers_WarnOnTheContextRatchet` (asserts only the warn count — its three transfers now archive as 0001/0002/0003 with no delta assertion).

- [ ] **Step 5: Run the full CLI test suite to verify green**

Run: `dotnet test tests/LoopRelay.CLI.Tests -c Release`
Expected: PASS (new tests green; the three updated tests green; nothing else regressed). Note: any Rust failures are pre-existing and unrelated (project memory).

- [ ] **Step 6: Commit**

```bash
git add src/LoopRelay.CLI/DecisionSession.cs tests/LoopRelay.CLI.Tests/DecisionSessionTests.cs
git commit -m "feat: archive operational delta after CLI transfer context update"
```

---

### Task 5: Cross-consumer verification + docs

**Files:**
- (Verify only) `src/LoopRelay.UI/src/api/operationalContext.ts`, `src/LoopRelay.Middle/Continuity/OperationalContextGenerationService.cs`, `src/LoopRelay.Continuity/Services/FileSystemOperationalContextProposalStore.cs`
- Modify (docs): `docs/orchestration-loop-governance.md`, `docs/architecture.md` — add a one-line note that the live `operational_delta.md` is archived to `.agents/deltas/` after a successful context update (only if these docs already describe the delta lifecycle; otherwise skip).

- [ ] **Step 1: Confirm no consumer depends on the live delta persisting** — search for readers of the live delta path and confirm none treat its post-transfer absence as an error:

Run: `rg -n "operational_delta" src`
Expected: the only writers/readers of the *live* `.agents/operational_delta.md` are the two Transfer implementations (now archiving) and the sandbox seeding (which uses its own sandbox path). If any other consumer reads the live delta as current state, STOP and reconcile before proceeding.

- [ ] **Step 2: Update the delta-lifecycle prose (if present)** — if `docs/orchestration-loop-governance.md` or `docs/architecture.md` describes the `operational_delta.md` lifecycle, add: "After the context update succeeds, the live delta is rotated to `.agents/deltas/operational_delta.NNNN.md` and removed." If neither doc mentions the delta lifecycle, skip this step (no doc change needed).

- [ ] **Step 3: Full suite sanity + commit**

Run: `dotnet test tests/LoopRelay.Backend.Tests -c Release` then `dotnet test tests/LoopRelay.CLI.Tests -c Release`
Expected: PASS (backend and CLI green; pre-existing Rust failures unrelated).

```bash
git add docs
git commit -m "docs: note operational delta archival after transfer"
```

---

## Self-Review

**Spec coverage:**
- Trigger/placement (after context write, before reseed) → Task 2 step 3b, Task 4 step 3. ✓
- Archive semantics (write numbered + delete live) → Task 2 (`ArchiveOperationalDeltaAsync`), Task 3 (reuses `RotateAsync`). ✓
- New paths → Task 1. ✓
- Strict failure → Task 2 (failed frame + return false), Task 4 (throw). ✓
- Both sites (backend + CLI) → Tasks 2 and 4. ✓
- Tests (happy, monotonic, failure) → Tasks 2, 3, 4. ✓
- `transferred` event / goldens untouched → Global Constraints + the "leave unchanged" notes. ✓
- Consumer verification → Task 5. ✓

**Placeholder scan:** All code steps show full code. The one flagged redundancy (the ternary in Task 2 Step 1's third test) has an explicit correction note. No TBD/TODO.

**Type consistency:** `ArchiveOperationalDeltaAsync(Repository)` / `NextDeltaSequenceAsync(Repository)` / `RotateOperationalDeltaAsync()` / `HistoricalDelta(int)` / `DeltasDirectory` / `HistoricalDeltaSearchPattern` are used identically everywhere they appear. Phase string `"ArchiveOperationalDelta"` matches between the orchestrator publish, the failed-frame test assertion, and the phase-sequence test update.
