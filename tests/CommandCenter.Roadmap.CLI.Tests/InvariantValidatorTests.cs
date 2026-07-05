using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class InvariantValidatorTests
{
    [Fact]
    public async Task Rejects_multiple_active_ready_epics()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        var lifecycle = new ArtifactLifecycleStore(repo.Artifacts);
        await lifecycle.UpsertAsync(".agents/epic.md", ArtifactLifecycleState.Ready);
        await lifecycle.UpsertAsync(".agents/epic-1.md", ArtifactLifecycleState.Executing);
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        InvariantValidationResult result = await CreateValidator(repo, lifecycle).ValidateAsync(RoadmapState.ActiveEpicReady, projectContext.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(RoadmapState.Failed, result.FailureState);
    }

    [Fact]
    public async Task Rejects_specs_that_belong_to_another_epic()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        repo.Write(".agents/specs/a.md", "Epic Path: .agents/other-epic.md");
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(RoadmapState.MilestoneSpecsReady, projectContext.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(RoadmapState.EvidenceBlocked, result.FailureState);
    }

    [Fact]
    public async Task Rejects_blocked_output_as_active_epic()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.ActiveEpic, """
            # Create New Epic Blocked

            ## Reason

            The proposal requires strategic investigation.
            """);
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(RoadmapState.ActiveEpicReady, projectContext.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(RoadmapState.EvidenceBlocked, result.FailureState);
        Assert.Contains("blocked", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_execution_without_required_artifacts()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(RoadmapState.ExecutionLoop, projectContext.Hash);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Rejects_split_child_promotion_without_split_family()
    {
        using var repo = new TempRepo();

        InvariantValidationResult result = await CreateValidator(repo).ValidateSplitChildPromotionAsync(".agents/epic-1.md");

        Assert.False(result.IsValid);
    }

    private static InvariantValidator CreateValidator(TempRepo repo, ArtifactLifecycleStore? lifecycle = null)
    {
        var projections = new ProjectionRegistry();
        return new InvariantValidator(
            repo.Artifacts,
            new ProjectContextLoader(repo.Artifacts),
            projections,
            new PromptContractRegistry(projections),
            new ProjectionManifestStore(repo.Artifacts),
            lifecycle ?? new ArtifactLifecycleStore(repo.Artifacts),
            new SplitFamilyStore(repo.Artifacts));
    }
}
