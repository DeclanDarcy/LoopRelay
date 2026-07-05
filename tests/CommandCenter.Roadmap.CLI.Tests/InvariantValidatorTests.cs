using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class InvariantValidatorTests
{
    [Fact]
    public async Task Rejects_multiple_active_ready_epics()
    {
        using var repo = new TempRepo();
        repo.SeedNorthStar();
        var lifecycle = new ArtifactLifecycleStore(repo.Artifacts);
        await lifecycle.UpsertAsync(".agents/epic.md", ArtifactLifecycleState.Ready);
        await lifecycle.UpsertAsync(".agents/epic-1.md", ArtifactLifecycleState.Executing);
        NorthStarContext northStar = await new NorthStarContextLoader(repo.Artifacts).LoadAsync();

        InvariantValidationResult result = await CreateValidator(repo, lifecycle).ValidateAsync(RoadmapState.ActiveEpicReady, northStar.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(RoadmapState.Failed, result.FailureState);
    }

    [Fact]
    public async Task Rejects_specs_that_belong_to_another_epic()
    {
        using var repo = new TempRepo();
        repo.SeedNorthStar();
        repo.Write(".agents/specs/a.md", "Epic Path: .agents/other-epic.md");
        NorthStarContext northStar = await new NorthStarContextLoader(repo.Artifacts).LoadAsync();

        InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(RoadmapState.MilestoneSpecsReady, northStar.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(RoadmapState.EvidenceBlocked, result.FailureState);
    }

    [Fact]
    public async Task Rejects_execution_without_required_artifacts()
    {
        using var repo = new TempRepo();
        repo.SeedNorthStar();
        NorthStarContext northStar = await new NorthStarContextLoader(repo.Artifacts).LoadAsync();

        InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(RoadmapState.ExecutionLoop, northStar.Hash);

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
            new NorthStarContextLoader(repo.Artifacts),
            projections,
            new PromptContractRegistry(projections),
            new ProjectionManifestStore(repo.Artifacts),
            lifecycle ?? new ArtifactLifecycleStore(repo.Artifacts),
            new SplitFamilyStore(repo.Artifacts));
    }
}
