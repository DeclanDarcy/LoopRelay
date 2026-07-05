using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class RoadmapStateMachineSelectionTests
{
    [Fact]
    public async Task Missing_completion_context_triggers_bootstrap_before_selection()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap");
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateRoadmapCompletionContext")),
            ScriptedAgentRuntime.Completed("# Roadmap Completion Context"),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(StrategicInvestigationSelection()));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Contains("# Roadmap Completion Context", repo.Read(RoadmapArtifactPaths.RoadmapCompletionContext), StringComparison.Ordinal);
        Assert.Equal(4, runtime.OneShotCalls);
    }

    [Fact]
    public async Task Existing_completion_context_skips_bootstrap()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "existing context");
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap");
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(StrategicInvestigationSelection()));

        RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(RoadmapOutcome.Paused, outcome);
        Assert.Equal(2, runtime.OneShotCalls);
        Assert.Equal("existing context", repo.Read(RoadmapArtifactPaths.RoadmapCompletionContext));
    }

    private static string StrategicInvestigationSelection() => """
        # Next Strategic Initiative Selection

        ## Recommendation Summary

        | Field | Value |
        |---|---|
        | Recommended Outcome | Strategic Investigation Required |
        | Recommended Initiative | Investigate A |
        | Initiative Type | Strategic Investigation |
        | Confidence | Medium |
        | Primary Reason | Evidence is insufficient |
        """;
}

internal static class StateMachineFactory
{
    public static RoadmapStateMachine Create(TempRepo repo, ScriptedAgentRuntime runtime)
    {
        var projections = new ProjectionRegistry();
        var contracts = new PromptContractRegistry(projections);
        var manifest = new ProjectionManifestStore(repo.Artifacts);
        var lifecycle = new ArtifactLifecycleStore(repo.Artifacts);
        var split = new SplitFamilyStore(repo.Artifacts);
        var loader = new ProjectContextLoader(repo.Artifacts);
        var runner = new RoadmapPromptRunner(runtime, repo.Repository, new TestConsole());
        var bridge = new FakeRoadmapExecutionBridge();
        var invariants = new InvariantValidator(repo.Artifacts, loader, projections, contracts, manifest, lifecycle, split);
        return new RoadmapStateMachine(
            repo.Artifacts,
            loader,
            contracts,
            manifest,
            new ProjectionCache(repo.Artifacts, projections, manifest, new ProjectionValidator(), runner),
            new RoadmapPromptContextBuilder(repo.Artifacts),
            new CompletionCertificationRouter(),
            runner,
            new RoadmapStateStore(repo.Artifacts),
            new DecisionLedgerStore(repo.Artifacts),
            new TransitionJournalStore(repo.Artifacts),
            lifecycle,
            new BundleFileExtractor(),
            new BundleManifestWriter(repo.Artifacts),
            split,
            new OperationalContextGenerator(repo.Artifacts, lifecycle),
            new ExecutionPromptGenerator(repo.Artifacts, lifecycle),
            new ExecutionCompatibilityMaterializer(repo.Artifacts),
            bridge,
            invariants,
            new TestConsole());
    }

    private sealed class FakeRoadmapExecutionBridge : IRoadmapExecutionBridge
    {
        public Task<RoadmapExecutionBridgeResult> RunAsync(CancellationToken cancellationToken) =>
            Task.FromResult(RoadmapExecutionBridgeResult.Completed("done"));
    }
}
