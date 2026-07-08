using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Tests.Services.State;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using EpicAuthoringOutputClassifier = LoopRelay.Roadmap.Cli.Services.ArtifactManagement.EpicAuthoringOutputClassifier;

namespace LoopRelay.Roadmap.Cli.Tests.Services.ArtifactManagement;

public sealed class ArtifactPromotionServiceTests
{
    [Fact]
    public async Task Successful_promotion_writes_authoritative_artifact_and_ready_lifecycle()
    {
        using var repo = new TempRepo();
        var lifecycle = new ArtifactLifecycleStore(repo.Artifacts);
        var service = new ArtifactPromotionService(repo.Artifacts, lifecycle);
        string candidate = RoadmapSamples.ValidEpic("Promoted Epic");

        ArtifactPromotionResult result = await service.PromoteAsync(Request(candidate));

        Assert.True(result.Promoted);
        Assert.Equal(candidate, repo.Read(RoadmapArtifactPaths.ActiveEpic));
        ArtifactLifecycleEntry entry = Assert.Single(await lifecycle.LoadAsync(), item => item.Path == RoadmapArtifactPaths.ActiveEpic);
        Assert.Equal(ArtifactLifecycleState.Ready, entry.State);
    }

    [Fact]
    public async Task Blocked_promotion_preserves_existing_authoritative_artifact_and_persists_exact_evidence()
    {
        using var repo = new TempRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, existing);
        var lifecycle = new ArtifactLifecycleStore(repo.Artifacts);
        var service = new ArtifactPromotionService(repo.Artifacts, lifecycle);
        string blocked = """
            # Create New Epic Blocked

            ## Reason

            The proposal requires roadmap revision first.
            """;

        ArtifactPromotionResult result = await service.PromoteAsync(Request(blocked));

        Assert.False(result.Promoted);
        Assert.Equal(ArtifactPromotionStatus.Blocked, result.Status);
        Assert.Equal(existing, repo.Read(RoadmapArtifactPaths.ActiveEpic));
        Assert.NotNull(result.EvidencePath);
        Assert.Equal(blocked, repo.Read(result.EvidencePath!));
        ArtifactLifecycleEntry evidence = Assert.Single(await lifecycle.LoadAsync(), item => item.Path == result.EvidencePath);
        Assert.Equal(ArtifactLifecycleState.Blocked, evidence.State);
    }

    [Fact]
    public async Task Ambiguous_promotion_preserves_existing_authoritative_artifact()
    {
        using var repo = new TempRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, existing);
        var service = new ArtifactPromotionService(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts));

        ArtifactPromotionResult result = await service.PromoteAsync(Request("I cannot safely decide what to write."));

        Assert.False(result.Promoted);
        Assert.Equal(ArtifactPromotionStatus.Ambiguous, result.Status);
        Assert.Equal(existing, repo.Read(RoadmapArtifactPaths.ActiveEpic));
    }

    [Fact]
    public async Task Structurally_invalid_promotion_preserves_existing_authoritative_artifact()
    {
        using var repo = new TempRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, existing);
        var service = new ArtifactPromotionService(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts));
        string invalid = """
            # Epic: Missing Structure

            ## Epic Metadata

            | Field | Value |
            |---|---|
            | Epic ID | EPIC-BAD |
            | Status | Authored |
            """;

        ArtifactPromotionResult result = await service.PromoteAsync(Request(invalid));

        Assert.False(result.Promoted);
        Assert.Equal(ArtifactPromotionStatus.StructurallyInvalid, result.Status);
        Assert.Equal(existing, repo.Read(RoadmapArtifactPaths.ActiveEpic));
    }

    [Fact]
    public async Task Successful_rewrite_replaces_existing_authoritative_artifact()
    {
        using var repo = new TempRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD");
        string replacement = RoadmapSamples.ValidEpic("Replacement Epic", "EPIC-NEW", "Realigned", "Realign");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, existing);
        var service = new ArtifactPromotionService(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts));

        ArtifactPromotionResult result = await service.PromoteAsync(Request(replacement));

        Assert.True(result.Promoted);
        Assert.Equal(replacement, repo.Read(RoadmapArtifactPaths.ActiveEpic));
    }

    private static ArtifactPromotionRequest Request(string candidate) =>
        new(
            RoadmapArtifactPaths.ActiveEpic,
            candidate,
            RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "active-epic-promotion",
            "active epic",
            new EpicAuthoringOutputClassifier(),
            new EpicArtifactValidator(),
            ArtifactLifecycleState.Ready,
            "Test promotion.");
}
