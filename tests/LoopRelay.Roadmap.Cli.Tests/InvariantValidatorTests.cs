using LoopRelay.Roadmap.Cli;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.ProjectContextLoader;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class InvariantValidatorTests
{
    [Fact]
    public async Task Rejects_multiple_active_ready_epics()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        var lifecycle = new Cli.ArtifactLifecycleStore(repo.Artifacts);
        await lifecycle.UpsertAsync(".agents/epic.md", Cli.ArtifactLifecycleState.Ready);
        await lifecycle.UpsertAsync(".agents/epic-1.md", Cli.ArtifactLifecycleState.Executing);
        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        Cli.InvariantValidationResult result = await CreateValidator(repo, lifecycle).ValidateAsync(Cli.RoadmapState.ActiveEpicReady, projectContext.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(Cli.RoadmapState.Failed, result.FailureState);
        Assert.Equal("DuplicateActiveEpic", result.FailureCategory);
        Assert.StartsWith(Cli.RoadmapArtifactPaths.OrchestrationEvidenceDirectory, result.EvidencePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_project_context_drift()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        Cli.ProjectContext original = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        repo.Write(Cli.RoadmapArtifactPaths.ProjectContextSourceFiles[0], "changed project context");

        Cli.InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(Cli.RoadmapState.CoreReady, original.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(Cli.RoadmapState.Failed, result.FailureState);
        Assert.Equal("ProjectContextDrift", result.FailureCategory);
    }

    [Fact]
    public async Task Rejects_specs_that_belong_to_another_epic()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        repo.Write(".agents/specs/a.md", "Epic Path: .agents/other-epic.md");
        await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, ".agents/specs/a.md");
        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        Cli.InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(Cli.RoadmapState.MilestoneSpecsReady, projectContext.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, result.FailureState);
        Assert.Equal("SpecEpicMismatch", result.FailureCategory);
    }

    [Fact]
    public async Task Rejects_blocked_output_as_active_epic()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, """
                                                        # Create New Epic Blocked

                                                        ## Reason

                                                        The proposal requires strategic investigation.
                                                        """);
        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        Cli.InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(Cli.RoadmapState.ActiveEpicReady, projectContext.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, result.FailureState);
        Assert.Equal("ActiveEpicInvalid", result.FailureCategory);
        Assert.Contains("blocked", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_missing_execution_preparation_prerequisites()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        Cli.InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(Cli.RoadmapState.ExecutionPromptReady, projectContext.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, result.FailureState);
        Assert.Equal("ExecutionPreparationStale", result.FailureCategory);
    }

    [Fact]
    public async Task Rejects_existing_projection_with_unknown_provenance()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"], ProjectionSamples.Valid("SelectNextEpic"));
        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        Cli.InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(Cli.RoadmapState.CoreReady, projectContext.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(Cli.RoadmapState.Failed, result.FailureState);
        Assert.Equal("ProjectionManifestMissing", result.FailureCategory);
        Assert.Contains("manifest entry", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_projection_manifest_invalid_entry()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        const string runtimePrompt = "SelectNextEpic";
        repo.Write(Cli.RoadmapArtifactPaths.ProjectionPaths[runtimePrompt], ProjectionSamples.Valid(runtimePrompt));
        await SeedProjectionManifestAsync(repo, runtimePrompt, Cli.ProjectionValidationStatus.Invalid);
        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        Cli.InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(Cli.RoadmapState.CoreReady, projectContext.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, result.FailureState);
        Assert.Equal("ProjectionInvalid", result.FailureCategory);
    }

    [Fact]
    public async Task Rejects_stale_projection_provenance()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        const string runtimePrompt = "SelectNextEpic";
        repo.Write(Cli.RoadmapArtifactPaths.ProjectionPaths[runtimePrompt], ProjectionSamples.Valid(runtimePrompt));
        await SeedProjectionManifestAsync(repo, runtimePrompt, Cli.ProjectionValidationStatus.Valid);
        repo.Write(Cli.RoadmapArtifactPaths.ProjectContextSourceFiles[0], "changed project context");
        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        Cli.InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(Cli.RoadmapState.CoreReady, projectContext.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(Cli.RoadmapState.EvidenceBlocked, result.FailureState);
        Assert.Equal("ProjectionProvenanceStale", result.FailureCategory);
    }

    [Fact]
    public async Task Rejects_split_child_promotion_without_split_family()
    {
        using var repo = new TempRepo();

        Cli.InvariantValidationResult result = await CreateValidator(repo).ValidateSplitChildPromotionAsync(".agents/epic-1.md");

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Rejects_non_child_split_promotion_target()
    {
        using var repo = new TempRepo();

        Cli.InvariantValidationResult result = await CreateValidator(repo).ValidateSplitChildPromotionAsync(".agents/specs/not-a-child.md");

        Assert.False(result.IsValid);
        Assert.Contains("not a valid split child", result.Error, StringComparison.Ordinal);
    }

    private static Cli.InvariantValidator CreateValidator(TempRepo repo, Cli.ArtifactLifecycleStore? lifecycle = null)
    {
        var projections = new Cli.ProjectionRegistry();
        return new Cli.InvariantValidator(
            repo.Artifacts,
            new ProjectContextLoader(repo.Artifacts),
            projections,
            new Cli.PromptContractRegistry(projections),
            new Cli.ProjectionManifestStore(repo.Artifacts),
            lifecycle ?? new Cli.ArtifactLifecycleStore(repo.Artifacts),
            new Cli.SplitFamilyStore(repo.Artifacts),
            ExecutionPreparationTestSupport.CreateProvenance(repo));
    }

    private static async Task SeedProjectionManifestAsync(
        TempRepo repo,
        string runtimePrompt,
        Cli.ProjectionValidationStatus validationStatus)
    {
        Cli.ProjectContext context = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        Cli.ProjectionProvenance provenance = new Cli.ProjectionProvenanceFactory(new Cli.ProjectionRegistry())
            .Create(runtimePrompt, context);
        await new Cli.ProjectionManifestStore(repo.Artifacts).UpsertAsync(Cli.ProjectionManifestEntry.FromTrustedProvenance(
            provenance,
            Cli.RoadmapHash.Sha256(repo.Read(Cli.RoadmapArtifactPaths.ProjectionPaths[runtimePrompt])),
            DateTimeOffset.UtcNow,
            validationStatus,
            Cli.ProjectionFreshness.Fresh,
            validationStatus == Cli.ProjectionValidationStatus.Invalid ? "Invalid projection fixture." : null));
    }
}
