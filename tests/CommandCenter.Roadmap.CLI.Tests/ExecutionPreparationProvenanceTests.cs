using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class ExecutionPreparationProvenanceTests
{
    [Fact]
    public async Task Matching_provenance_allows_execution_preparation_reuse()
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);

        ExecutionPreparationReadiness readiness = await provenance.EvaluateReadinessAsync(
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
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic("Replacement Epic", "EPIC-NEW"));

        ExecutionPreparationReadiness readiness = await provenance.EvaluateReadinessAsync(
            requireSpecs: true,
            requireOperationalContext: true,
            requireExecutionPrompt: true,
            requireCompatibilityArtifacts: true);

        Assert.False(readiness.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.ActiveEpicDrift, readiness.Artifacts.SelectMany(result => result.Freshness.Reasons));
    }

    [Fact]
    public async Task Milestone_spec_change_invalidates_execution_preparation()
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        repo.Write(".agents/specs/a.md", Spec("Changed Spec", "- [ ] Changed work."));

        DerivedArtifactFreshness specs = await provenance.EvaluateMilestoneSpecsFreshnessAsync();

        Assert.False(specs.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.ArtifactHashDrift, specs.Reasons);
    }

    [Fact]
    public async Task Operational_context_change_invalidates_execution_prompt_and_compatibility()
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        repo.Write(RoadmapArtifactPaths.OperationalContext, "# Tampered Operational Context");

        DerivedArtifactFreshness prompt = await provenance.EvaluateExecutionPromptFreshnessAsync();
        DerivedArtifactFreshness compatibility = await provenance.EvaluateCompatibilityFreshnessAsync();

        Assert.False(prompt.IsFresh);
        Assert.False(compatibility.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.OperationalContextDrift, compatibility.Reasons);
    }

    [Fact]
    public async Task Execution_prompt_change_invalidates_compatibility_artifacts()
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        repo.Write(RoadmapArtifactPaths.ExecutionPrompt, "# Tampered Execution Prompt");

        DerivedArtifactFreshness compatibility = await provenance.EvaluateCompatibilityFreshnessAsync();

        Assert.False(compatibility.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.ExecutionPromptDrift, compatibility.Reasons);
    }

    [Fact]
    public async Task Deleted_execution_prompt_is_not_fresh()
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        await repo.Artifacts.DeleteAsync(RoadmapArtifactPaths.ExecutionPrompt);

        DerivedArtifactFreshness prompt = await provenance.EvaluateExecutionPromptFreshnessAsync();

        Assert.False(prompt.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.ArtifactMissing, prompt.Reasons);
    }

    [Fact]
    public async Task Decision_ledger_change_supports_partial_regeneration()
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        repo.Write(RoadmapArtifactPaths.DecisionLedger, "# Decision Ledger");

        DerivedArtifactFreshness operationalContext = await provenance.EvaluateOperationalContextFreshnessAsync();
        Assert.False(operationalContext.IsFresh);
        Assert.Contains(DerivedArtifactStaleReason.DecisionLedgerDrift, operationalContext.Reasons);

        await new OperationalContextGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        Assert.False((await provenance.EvaluateExecutionPromptFreshnessAsync()).IsFresh);

        await new ExecutionPromptGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        await new ExecutionCompatibilityMaterializer(repo.Artifacts, provenance).MaterializeAsync();

        ExecutionPreparationReadiness readiness = await provenance.EvaluateReadinessAsync(
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
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo, specCount: 2);
        Assert.True(await repo.Artifacts.ExistsAsync(".agents/milestones/m002.md"));

        await provenance.RecordMilestoneSpecsAsync([".agents/specs/a.md"]);
        await new OperationalContextGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        await new ExecutionPromptGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        await new ExecutionCompatibilityMaterializer(repo.Artifacts, provenance).MaterializeAsync();

        ExecutionPreparationReadiness readiness = await provenance.EvaluateReadinessAsync(
            requireSpecs: true,
            requireOperationalContext: true,
            requireExecutionPrompt: true,
            requireCompatibilityArtifacts: true);
        ExecutionPreparationManifest manifest = await new ExecutionPreparationManifestStore(repo.Artifacts).LoadAsync();
        DerivedArtifactManifestEntry oldMilestone = Assert.Single(
            manifest.Artifacts,
            entry => entry.ArtifactIdentity == ".agents/milestones/m002.md");

        Assert.True(readiness.IsFresh);
        Assert.Equal(DerivedArtifactProvenanceStatus.Superseded, oldMilestone.ProvenanceStatus);
    }

    [Fact]
    public async Task Resume_refuses_stale_execution_preparation()
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        ProjectContext context = await SeedProjectAsync(repo);
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic("Replacement Epic", "EPIC-NEW"));

        RoadmapResumePlan plan = await CreatePlanner(repo, provenance).PlanAsync(
            State(RoadmapState.ExecutionPromptReady, output: RoadmapArtifactPaths.ExecutionPrompt),
            context,
            CancellationToken.None);

        Assert.Equal(RoadmapResumeAction.Block, plan.Action);
        Assert.Contains("provenance is not fresh", plan.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("spec")]
    [InlineData("operational-context")]
    [InlineData("execution-prompt")]
    public async Task Invariant_rejects_stale_execution_preparation_before_execution(string staleArtifact)
    {
        using var repo = new TempRepo();
        ExecutionPreparationProvenanceService provenance = await SeedFullPreparationAsync(repo);
        ProjectContext context = await new ProjectContextLoader(repo.Artifacts).LoadAsync(CancellationToken.None);

        switch (staleArtifact)
        {
            case "spec":
                repo.Write(".agents/specs/a.md", Spec("Changed Spec", "- [ ] Changed work."));
                break;
            case "operational-context":
                repo.Write(RoadmapArtifactPaths.OperationalContext, "# Tampered Operational Context");
                break;
            case "execution-prompt":
                repo.Write(RoadmapArtifactPaths.ExecutionPrompt, "# Tampered Execution Prompt");
                break;
        }

        InvariantValidationResult result = await CreateInvariantValidator(repo, provenance)
            .ValidateAsync(RoadmapState.ExecutionPromptReady, context.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(RoadmapState.EvidenceBlocked, result.FailureState);
        Assert.Contains("provenance is not fresh", result.Error, StringComparison.Ordinal);
    }

    private static async Task<ExecutionPreparationProvenanceService> SeedFullPreparationAsync(
        TempRepo repo,
        int specCount = 1)
    {
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        await new ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(RoadmapArtifactPaths.ActiveEpic, ArtifactLifecycleState.Ready);

        var specPaths = new List<string> { ".agents/specs/a.md" };
        repo.Write(".agents/specs/a.md", Spec("Spec A", "- [ ] Do A."));
        if (specCount > 1)
        {
            specPaths.Add(".agents/specs/b.md");
            repo.Write(".agents/specs/b.md", Spec("Spec B", "- [ ] Do B."));
        }

        ExecutionPreparationProvenanceService provenance = await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, specPaths.ToArray());
        await new OperationalContextGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        await new ExecutionPromptGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();
        await new ExecutionCompatibilityMaterializer(repo.Artifacts, provenance).MaterializeAsync();
        return provenance;
    }

    private static async Task<ProjectContext> SeedProjectAsync(TempRepo repo)
    {
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap");
        return await new ProjectContextLoader(repo.Artifacts).LoadAsync(CancellationToken.None);
    }

    private static RoadmapResumePlanner CreatePlanner(
        TempRepo repo,
        ExecutionPreparationProvenanceService provenance)
    {
        var projections = new ProjectionRegistry();
        return new RoadmapResumePlanner(
            repo.Artifacts,
            new PromptContractRegistry(projections),
            new ProjectionManifestStore(repo.Artifacts),
            new ArtifactLifecycleStore(repo.Artifacts),
            new ProjectionProvenanceFactory(projections),
            provenance);
    }

    private static InvariantValidator CreateInvariantValidator(
        TempRepo repo,
        ExecutionPreparationProvenanceService provenance)
    {
        var projections = new ProjectionRegistry();
        return new InvariantValidator(
            repo.Artifacts,
            new ProjectContextLoader(repo.Artifacts),
            projections,
            new PromptContractRegistry(projections),
            new ProjectionManifestStore(repo.Artifacts),
            new ArtifactLifecycleStore(repo.Artifacts),
            new SplitFamilyStore(repo.Artifacts),
            provenance);
    }

    private static RoadmapStateDocument State(
        RoadmapState state,
        TransitionStatus status = TransitionStatus.Completed,
        RoadmapState? from = null,
        RoadmapState? to = null,
        string prompt = "None",
        string output = "None") =>
        new(
            state,
            [],
            new RoadmapTransitionSummary(from ?? state, to ?? state, prompt, "None", output, "Completed", status, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            [],
            "None",
            0,
            0,
            new ProjectionManifestCounts(0, 0, 0),
            RoadmapTransitionIntent.Empty(state),
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
