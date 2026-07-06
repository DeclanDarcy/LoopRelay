using LoopRelay.Roadmap.Cli;
using ExecutionCompatibilityMaterializer = LoopRelay.Roadmap.Cli.ExecutionCompatibilityMaterializer;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.ProjectContextLoader;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class ExecutionPreparationProvenanceTests
{
    [Fact]
    public async Task Matching_provenance_allows_execution_preparation_reuse()
    {
        using var repo = new TempRepo();
        Cli.ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);

        Cli.ExecutionPreparationReadiness readiness = await provenance.EvaluateReadinessAsync(
            requireSpecs: true,
            requireOperationalContext: true,
            requireExecutionPrompt: true,
            requireCompatibilityArtifacts: true);

        Assert.True(readiness.IsFresh);
    }

    [Fact]
    public async Task Active_epic_change_invalidates_downstream_provenance()
    {
        using var repo = new TempRepo();
        Cli.ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic("Replacement Epic", "EPIC-NEW"));

        Cli.ExecutionPreparationReadiness readiness = await provenance.EvaluateReadinessAsync(
            requireSpecs: true,
            requireOperationalContext: true,
            requireExecutionPrompt: true,
            requireCompatibilityArtifacts: true);

        Assert.False(readiness.IsFresh);
        Assert.Contains(Cli.DerivedArtifactStaleReason.ActiveEpicDrift, readiness.Artifacts.SelectMany(result => result.Freshness.Reasons));
    }

    [Fact]
    public async Task Milestone_spec_change_invalidates_execution_preparation()
    {
        using var repo = new TempRepo();
        Cli.ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        repo.Write(".agents/specs/a.md", Spec("Changed Spec", "- [ ] Changed work."));

        Cli.DerivedArtifactFreshness specs = await provenance.EvaluateMilestoneSpecsFreshnessAsync();

        Assert.False(specs.IsFresh);
        Assert.Contains(Cli.DerivedArtifactStaleReason.ArtifactHashDrift, specs.Reasons);
    }

    [Fact]
    public async Task Operational_context_change_invalidates_execution_prompt_and_compatibility()
    {
        using var repo = new TempRepo();
        Cli.ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        repo.Write(Cli.RoadmapArtifactPaths.OperationalContext, "# Tampered Operational Context");

        Cli.DerivedArtifactFreshness prompt = await provenance.EvaluateExecutionPromptFreshnessAsync();
        Cli.DerivedArtifactFreshness compatibility = await provenance.EvaluateCompatibilityFreshnessAsync();

        Assert.False(prompt.IsFresh);
        Assert.False(compatibility.IsFresh);
        Assert.Contains(Cli.DerivedArtifactStaleReason.OperationalContextDrift, compatibility.Reasons);
    }

    [Fact]
    public async Task Execution_prompt_change_invalidates_compatibility_artifacts()
    {
        using var repo = new TempRepo();
        Cli.ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        repo.Write(Cli.RoadmapArtifactPaths.ExecutionPrompt, "# Tampered Execution Prompt");

        Cli.DerivedArtifactFreshness compatibility = await provenance.EvaluateCompatibilityFreshnessAsync();

        Assert.False(compatibility.IsFresh);
        Assert.Contains(Cli.DerivedArtifactStaleReason.ExecutionPromptDrift, compatibility.Reasons);
    }

    [Fact]
    public async Task Deleted_execution_prompt_is_not_fresh()
    {
        using var repo = new TempRepo();
        Cli.ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        await repo.Artifacts.DeleteAsync(Cli.RoadmapArtifactPaths.ExecutionPrompt);

        Cli.DerivedArtifactFreshness prompt = await provenance.EvaluateExecutionPromptFreshnessAsync();

        Assert.False(prompt.IsFresh);
        Assert.Contains(Cli.DerivedArtifactStaleReason.ArtifactMissing, prompt.Reasons);
    }

    [Fact]
    public async Task Decision_ledger_change_supports_partial_regeneration()
    {
        using var repo = new TempRepo();
        Cli.ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        repo.Write(Cli.RoadmapArtifactPaths.DecisionLedger, "# Decision Ledger");

        Cli.DerivedArtifactFreshness operationalContext = await provenance.EvaluateOperationalContextFreshnessAsync();
        Assert.False(operationalContext.IsFresh);
        Assert.Contains(Cli.DerivedArtifactStaleReason.DecisionLedgerDrift, operationalContext.Reasons);

        await new Cli.OperationalContextGenerator(repo.Artifacts, new Cli.ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        Assert.False((await provenance.EvaluateExecutionPromptFreshnessAsync()).IsFresh);

        await new Cli.ExecutionPromptGenerator(repo.Artifacts, new Cli.ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        await new ExecutionCompatibilityMaterializer(repo.Artifacts, provenance).MaterializeAsync();

        Cli.ExecutionPreparationReadiness readiness = await provenance.EvaluateReadinessAsync(
            requireSpecs: true,
            requireOperationalContext: true,
            requireExecutionPrompt: true,
            requireCompatibilityArtifacts: true);
        Assert.True(readiness.IsFresh);
    }

    [Fact]
    public async Task Reduced_milestone_count_supersedes_old_compatibility_artifacts_without_reusing_them()
    {
        using var repo = new TempRepo();
        Cli.ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo, specCount: 2);
        Assert.True(await repo.Artifacts.ExistsAsync(".agents/milestones/m002.md"));

        await provenance.RecordMilestoneSpecsAsync([".agents/specs/a.md"]);
        await new Cli.OperationalContextGenerator(repo.Artifacts, new Cli.ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        await new Cli.ExecutionPromptGenerator(repo.Artifacts, new Cli.ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        await new ExecutionCompatibilityMaterializer(repo.Artifacts, provenance).MaterializeAsync();

        Cli.ExecutionPreparationReadiness readiness = await provenance.EvaluateReadinessAsync(
            requireSpecs: true,
            requireOperationalContext: true,
            requireExecutionPrompt: true,
            requireCompatibilityArtifacts: true);
        Cli.ExecutionPreparationManifest manifest = await new Cli.ExecutionPreparationManifestStore(repo.Artifacts).LoadAsync();
        Cli.DerivedArtifactManifestEntry oldMilestone = Assert.Single(
            manifest.Artifacts,
            entry => entry.ArtifactIdentity == ".agents/milestones/m002.md");

        Assert.True(readiness.IsFresh);
        Assert.Equal(Cli.DerivedArtifactProvenanceStatus.Superseded, oldMilestone.ProvenanceStatus);
    }

    [Fact]
    public async Task Resume_refuses_stale_execution_preparation()
    {
        using var repo = new TempRepo();
        Cli.ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        Cli.ProjectContext context = await SeedProjectAsync(repo);
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic("Replacement Epic", "EPIC-NEW"));

        Cli.RoadmapResumePlan plan = await CreatePlanner(repo, provenance).PlanAsync(
            State(Cli.RoadmapState.ExecutionPromptReady, output: Cli.RoadmapArtifactPaths.ExecutionPrompt),
            context,
            CancellationToken.None);

        Assert.Equal(Cli.RoadmapResumeAction.Block, plan.Action);
        Assert.Contains("provenance is not fresh", plan.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("spec")]
    [InlineData("operational-context")]
    [InlineData("execution-prompt")]
    public async Task Invariant_rejects_stale_execution_preparation_before_execution(string staleArtifact)
    {
        using var repo = new TempRepo();
        Cli.ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        Cli.ProjectContext context = await new ProjectContextLoader(repo.Artifacts).LoadAsync(CancellationToken.None);

        switch (staleArtifact)
        {
            case "spec":
                repo.Write(".agents/specs/a.md", Spec("Changed Spec", "- [ ] Changed work."));
                break;
            case "operational-context":
                repo.Write(Cli.RoadmapArtifactPaths.OperationalContext, "# Tampered Operational Context");
                break;
            case "execution-prompt":
                repo.Write(Cli.RoadmapArtifactPaths.ExecutionPrompt, "# Tampered Execution Prompt");
                break;
        }

        Cli.InvariantValidationResult result = await CreateInvariantValidator(repo, provenance)
            .ValidateAsync(Cli.RoadmapState.ExecutionPromptReady, context.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, result.FailureState);
        Assert.Equal("ExecutionPreparationStale", result.FailureCategory);
        Assert.Contains("provenance is not fresh", result.Error, StringComparison.Ordinal);
    }

    private static async Task<Cli.ExecutionPreparationProvenanceService> SeedFullPreparationAsync(
        TempRepo repo,
        int specCount = 1)
    {
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapFile, "roadmap");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        await new Cli.ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(Cli.RoadmapArtifactPaths.ActiveEpic, Cli.ArtifactLifecycleState.Ready);

        var specPaths = new List<string> { ".agents/specs/a.md" };
        repo.Write(".agents/specs/a.md", Spec("Spec A", "- [ ] Do A."));
        if (specCount > 1)
        {
            specPaths.Add(".agents/specs/b.md");
            repo.Write(".agents/specs/b.md", Spec("Spec B", "- [ ] Do B."));
        }

        Cli.ExecutionPreparationProvenanceService provenance = await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, specPaths.ToArray());
        await new Cli.OperationalContextGenerator(repo.Artifacts, new Cli.ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        await new Cli.ExecutionPromptGenerator(repo.Artifacts, new Cli.ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        await new ExecutionCompatibilityMaterializer(repo.Artifacts, provenance).MaterializeAsync();
        return provenance;
    }

    private static async Task<Cli.ProjectContext> SeedProjectAsync(TempRepo repo)
    {
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapFile, "roadmap");
        return await new ProjectContextLoader(repo.Artifacts).LoadAsync(CancellationToken.None);
    }

    private static Cli.RoadmapResumePlanner CreatePlanner(
        TempRepo repo,
        Cli.ExecutionPreparationProvenanceService provenance)
    {
        var projections = new Cli.ProjectionRegistry();
        var contextBuilder = new Cli.RoadmapPromptContextBuilder(repo.Artifacts, provenance);
        var inputResolver = new Cli.TransitionInputResolver(repo.Artifacts, provenance);
        var selectionProvenance = new Cli.SelectionProvenanceService(
            repo.Artifacts,
            new Cli.SelectionProvenanceManifestStore(repo.Artifacts),
            contextBuilder,
            inputResolver);
        return new Cli.RoadmapResumePlanner(
            repo.Artifacts,
            new Cli.PromptContractRegistry(projections),
            new Cli.ProjectionManifestStore(repo.Artifacts),
            new Cli.ArtifactLifecycleStore(repo.Artifacts),
            new Cli.ProjectionProvenanceFactory(projections),
            selectionProvenance,
            provenance);
    }

    private static Cli.InvariantValidator CreateInvariantValidator(
        TempRepo repo,
        Cli.ExecutionPreparationProvenanceService provenance)
    {
        var projections = new Cli.ProjectionRegistry();
        return new Cli.InvariantValidator(
            repo.Artifacts,
            new ProjectContextLoader(repo.Artifacts),
            projections,
            new Cli.PromptContractRegistry(projections),
            new Cli.ProjectionManifestStore(repo.Artifacts),
            new Cli.ArtifactLifecycleStore(repo.Artifacts),
            new Cli.SplitFamilyStore(repo.Artifacts),
            provenance);
    }

    private static Cli.RoadmapStateDocument State(
        Cli.RoadmapState state,
        Cli.TransitionStatus status = Cli.TransitionStatus.Completed,
        Cli.RoadmapState? from = null,
        Cli.RoadmapState? to = null,
        string prompt = "None",
        string output = "None") =>
        new(
            state,
            [],
            new Cli.RoadmapTransitionSummary(from ?? state, to ?? state, prompt, "None", output, "Completed", status, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            [],
            "None",
            0,
            0,
            new Cli.ProjectionManifestCounts(0, 0, 0),
            Cli.RoadmapTransitionIntent.Empty(state),
            [],
            []);

    private static string Spec(string title, string checklist) => $$"""
        # {{title}}

        | Field | Value |
        |---|---|
        | Epic Path | .agents/epic.md |

        ## Acceptance Criteria

        {{checklist}}
        """;
}
