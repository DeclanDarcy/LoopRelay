using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Primitives.Projections;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.Splits;
using LoopRelay.Roadmap.Cli.Services.State;
using LoopRelay.Roadmap.Cli.Tests.Services.Execution;
using LoopRelay.Roadmap.Cli.Tests.Services.Projections;
using LoopRelay.Roadmap.Cli.Tests.Services.State;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.Services.Projections.ProjectContextLoader;

namespace LoopRelay.Roadmap.Cli.Tests.Services.ArtifactManagement;

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
        Assert.Equal("DuplicateActiveEpic", result.FailureCategory);
        Assert.StartsWith((string?)RoadmapArtifactPaths.OrchestrationEvidenceDirectory, result.EvidencePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_project_context_drift()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        ProjectContext original = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        repo.Write(RoadmapArtifactPaths.ProjectContextSourceFiles[0], "changed project context");

        InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(RoadmapState.CoreReady, original.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(RoadmapState.Failed, result.FailureState);
        Assert.Equal("ProjectContextDrift", result.FailureCategory);
    }

    [Fact]
    public async Task Rejects_specs_that_belong_to_another_epic()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        repo.Write(".agents/specs/a.md", "Epic Path: .agents/other-epic.md");
        await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, ".agents/specs/a.md");
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(RoadmapState.MilestoneSpecsReady, projectContext.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(RoadmapState.EvidenceBlocked, result.FailureState);
        Assert.Equal("SpecEpicMismatch", result.FailureCategory);
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
        Assert.Equal("ActiveEpicInvalid", result.FailureCategory);
        Assert.Contains("blocked", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_missing_execution_preparation_prerequisites()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.ActiveEpic, RoadmapSamples.ValidEpic());
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(RoadmapState.ExecutionPromptReady, projectContext.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(RoadmapState.EvidenceBlocked, result.FailureState);
        Assert.Equal("ExecutionPreparationStale", result.FailureCategory);
    }

    [Fact]
    public async Task Rejects_existing_projection_with_unknown_provenance()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        repo.Write(RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"], ProjectionSamples.Valid("SelectNextEpic"));
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(RoadmapState.CoreReady, projectContext.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(RoadmapState.Failed, result.FailureState);
        Assert.Equal("ProjectionManifestMissing", result.FailureCategory);
        Assert.Contains("manifest entry", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_projection_manifest_invalid_entry()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        const string runtimePrompt = "SelectNextEpic";
        repo.Write(RoadmapArtifactPaths.ProjectionPaths[runtimePrompt], ProjectionSamples.Valid(runtimePrompt));
        await SeedProjectionManifestAsync(repo, runtimePrompt, ProjectionValidationStatus.Invalid);
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(RoadmapState.CoreReady, projectContext.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(RoadmapState.EvidenceBlocked, result.FailureState);
        Assert.Equal("ProjectionInvalid", result.FailureCategory);
    }

    [Fact]
    public async Task Rejects_stale_projection_provenance()
    {
        using var repo = new TempRepo();
        repo.SeedProjectContext();
        const string runtimePrompt = "SelectNextEpic";
        repo.Write(RoadmapArtifactPaths.ProjectionPaths[runtimePrompt], ProjectionSamples.Valid(runtimePrompt));
        await SeedProjectionManifestAsync(repo, runtimePrompt, ProjectionValidationStatus.Valid);
        repo.Write(RoadmapArtifactPaths.ProjectContextSourceFiles[0], "changed project context");
        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync();

        InvariantValidationResult result = await CreateValidator(repo).ValidateAsync(RoadmapState.CoreReady, projectContext.Hash);

        Assert.False(result.IsValid);
        Assert.Equal(RoadmapState.EvidenceBlocked, result.FailureState);
        Assert.Equal("ProjectionProvenanceStale", result.FailureCategory);
    }

    [Fact]
    public async Task Rejects_split_child_promotion_without_split_family()
    {
        using var repo = new TempRepo();

        InvariantValidationResult result = await CreateValidator(repo).ValidateSplitChildPromotionAsync(".agents/epic-1.md");

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Rejects_non_child_split_promotion_target()
    {
        using var repo = new TempRepo();

        InvariantValidationResult result = await CreateValidator(repo).ValidateSplitChildPromotionAsync(".agents/specs/not-a-child.md");

        Assert.False(result.IsValid);
        Assert.Contains("not a valid split child", result.Error, StringComparison.Ordinal);
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
            new SplitFamilyStore(repo.Artifacts),
            ExecutionPreparationTestSupport.CreateProvenance(repo));
    }

    private static async Task SeedProjectionManifestAsync(
        TempRepo repo,
        string runtimePrompt,
        ProjectionValidationStatus validationStatus)
    {
        ProjectContext context = await new ProjectContextLoader(repo.Artifacts).LoadAsync();
        ProjectionProvenance provenance = new ProjectionProvenanceFactory(new ProjectionRegistry())
            .Create(runtimePrompt, context);
        await new ProjectionManifestStore(repo.Artifacts).UpsertAsync(ProjectionManifestEntry.FromTrustedProvenance(
            provenance,
            RoadmapHash.Sha256(repo.Read(RoadmapArtifactPaths.ProjectionPaths[runtimePrompt])),
            DateTimeOffset.UtcNow,
            validationStatus,
            ProjectionFreshness.Fresh,
            validationStatus == ProjectionValidationStatus.Invalid ? "Invalid projection fixture." : null));
    }
}
