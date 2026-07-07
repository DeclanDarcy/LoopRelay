using LoopRelay.Agents.Abstractions;
using LoopRelay.Roadmap.Cli;
using BundleFileExtractor = LoopRelay.Roadmap.Cli.BundleFileExtractor;
using DecisionLedgerStore = LoopRelay.Roadmap.Cli.DecisionLedgerStore;
using ExecutionCompatibilityMaterializer = LoopRelay.Roadmap.Cli.ExecutionCompatibilityMaterializer;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.ProjectContextLoader;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.RoadmapStateStore;
using SplitEpicBundleInterpreter = LoopRelay.Roadmap.Cli.SplitEpicBundleInterpreter;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapStateMachineSelectionTests
{
    [Fact]
    public async Task Missing_completion_context_triggers_bootstrap_before_selection()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateRoadmapCompletionContext")),
            ScriptedAgentRuntime.Completed("# Roadmap Completion Context"),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(StrategicInvestigationSelection()));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Contains("# Roadmap Completion Context", repo.Read(Cli.RoadmapArtifactPaths.RoadmapCompletionContext), StringComparison.Ordinal);
        Assert.Equal(4, runtime.OneShotCalls);
        Assert.Contains(runtime.Prompts, prompt => prompt.Contains("No completed epic markdown files were found under `.agents/archive/epics/*.md`.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Missing_completion_context_bootstrap_passes_archived_epic_evidence()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        repo.Write(".agents/archive/epics/001-done.md", """
            # Epic: Archived Capability

            ## Epic Metadata

            | Field | Value |
            |---|---|
            | Epic ID | EPIC-DONE |

            ## Completion Evidence

            Implemented and verified.
            """);
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("CreateRoadmapCompletionContext")),
            ScriptedAgentRuntime.Completed("# Roadmap Completion Context"),
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(StrategicInvestigationSelection()));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        string bootstrapPrompt = runtime.Prompts.Single(prompt => prompt.Contains(".agents/archive/epics/001-done.md", StringComparison.Ordinal));
        Assert.Contains("Archived Capability", bootstrapPrompt, StringComparison.Ordinal);
        Assert.Contains("| Epic ID | EPIC-DONE |", bootstrapPrompt, StringComparison.Ordinal);
        Assert.Contains("| Evidence Quality | Strong |", bootstrapPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_completion_context_skips_bootstrap()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "existing context");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap");
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed(ProjectionSamples.Valid("SelectNextEpic")),
            ScriptedAgentRuntime.Completed(StrategicInvestigationSelection()));

        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(repo, runtime).RunAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Equal(2, runtime.OneShotCalls);
        Assert.Equal("existing context", repo.Read(Cli.RoadmapArtifactPaths.RoadmapCompletionContext));
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
    public static Cli.RoadmapStateMachine Create(
        TempRepo repo,
        IAgentRuntime runtime,
        Cli.IRoadmapExecutionBridge? bridge = null)
    {
        var projections = new Cli.ProjectionRegistry();
        var contracts = new Cli.PromptContractRegistry(projections);
        var manifest = new Cli.ProjectionManifestStore(repo.Artifacts);
        var executionPreparationManifest = new Cli.ExecutionPreparationManifestStore(repo.Artifacts);
        var executionPreparation = new Cli.ExecutionPreparationProvenanceService(repo.Artifacts, executionPreparationManifest);
        var lifecycle = new Cli.ArtifactLifecycleStore(repo.Artifacts);
        var stateStore = new RoadmapStateStore(repo.Artifacts);
        var split = new Cli.SplitFamilyStore(repo.Artifacts);
        var loader = new ProjectContextLoader(repo.Artifacts);
        var runner = new Cli.RoadmapPromptRunner(runtime, repo.Repository, new TestConsole());
        var contextBuilder = new Cli.RoadmapPromptContextBuilder(repo.Artifacts, executionPreparation);
        var inputResolver = new Cli.TransitionInputResolver(repo.Artifacts, executionPreparation);
        var selectionProvenance = new Cli.SelectionProvenanceService(
            repo.Artifacts,
            new Cli.SelectionProvenanceManifestStore(repo.Artifacts),
            contextBuilder,
            inputResolver);
        Cli.IRoadmapExecutionBridge executionBridge = bridge ?? new FakeRoadmapExecutionBridge();
        var invariants = new Cli.InvariantValidator(repo.Artifacts, loader, projections, contracts, manifest, lifecycle, split, executionPreparation);
        var resumePlanner = new Cli.RoadmapResumePlanner(repo.Artifacts, contracts, manifest, lifecycle, new Cli.ProjectionProvenanceFactory(projections), selectionProvenance, executionPreparation);
        var unblockPlanner = new Cli.RoadmapUnblockPlanner(repo.Artifacts, loader, contracts, resumePlanner, new Cli.CompletionCertificationPolicy(), new Cli.CompletionCertificationRouter(), executionPreparation);
        return new Cli.RoadmapStateMachine(
            repo.Artifacts,
            loader,
            contracts,
            manifest,
            new Cli.ProjectionCache(repo.Artifacts, projections, manifest, new Cli.ProjectionValidator(), runner),
            contextBuilder,
            inputResolver,
            new Cli.CompletionCertificationPolicy(),
            new Cli.CompletionCertificationRouter(),
            runner,
            stateStore,
            new Cli.RoadmapStartupPlanner(),
            resumePlanner,
            unblockPlanner,
            selectionProvenance,
            new DecisionLedgerStore(repo.Artifacts),
            new Cli.TransitionJournalStore(repo.Artifacts),
            lifecycle,
            new Cli.ArtifactPromotionService(repo.Artifacts, lifecycle),
            new BundleFileExtractor(),
            new SplitEpicBundleInterpreter(),
            new Cli.BundleManifestWriter(repo.Artifacts),
            split,
            executionPreparation,
            new Cli.OperationalContextGenerator(repo.Artifacts, lifecycle, executionPreparation),
            new Cli.ExecutionPromptGenerator(repo.Artifacts, lifecycle, executionPreparation),
            new ExecutionCompatibilityMaterializer(repo.Artifacts, executionPreparation),
            executionBridge,
            new Cli.RoadmapExecutionOutcomeInterpreter(),
            invariants,
            new TestConsole());
    }

    private sealed class FakeRoadmapExecutionBridge : Cli.IRoadmapExecutionBridge
    {
        public Task<Cli.RoadmapExecutionTransportResult> RunAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Cli.RoadmapExecutionTransportResult.Completed("""
                                                                          # Execution Report

                                                                          ## Execution Disposition

                                                                          | Field | Value |
                                                                          |---|---|
                                                                          | Status | Epic Complete |
                                                                          | Confidence | High |
                                                                          | Evidence Summary | Test bridge explicitly claims epic completion. |
                                                                          | Next Step | EvaluateEpicCompletionAndDrift |
                                                                          """));
    }
}
