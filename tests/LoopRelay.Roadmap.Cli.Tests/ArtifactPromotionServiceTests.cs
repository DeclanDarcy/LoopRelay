using LoopRelay.Roadmap.Cli;
using EpicAuthoringOutputClassifier = LoopRelay.Roadmap.Cli.EpicAuthoringOutputClassifier;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class ArtifactPromotionServiceTests
{
    [Fact]
    public async Task Successful_promotion_writes_authoritative_artifact_and_ready_lifecycle()
    {
        using var repo = new TempRepo();
        var lifecycle = new Cli.ArtifactLifecycleStore(repo.Artifacts);
        var service = new Cli.ArtifactPromotionService(repo.Artifacts, lifecycle);
        string candidate = RoadmapSamples.ValidEpic("Promoted Epic");

        Cli.ArtifactPromotionResult result = await service.PromoteAsync(Request(candidate));

        Assert.True(result.Promoted);
        Assert.Equal(candidate, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
        Cli.ArtifactLifecycleEntry entry = Assert.Single(await lifecycle.LoadAsync(), item => item.Path == Cli.RoadmapArtifactPaths.ActiveEpic);
        Assert.Equal(Cli.ArtifactLifecycleState.Ready, entry.State);
    }

    [Fact]
    public async Task Blocked_promotion_preserves_existing_authoritative_artifact_and_persists_exact_evidence()
    {
        using var repo = new TempRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, existing);
        var lifecycle = new Cli.ArtifactLifecycleStore(repo.Artifacts);
        var service = new Cli.ArtifactPromotionService(repo.Artifacts, lifecycle);
        string blocked = """
            # Create New Epic Blocked

            ## Reason

            The proposal requires roadmap revision first.
            """;

        Cli.ArtifactPromotionResult result = await service.PromoteAsync(Request(blocked));

        Assert.False(result.Promoted);
        Assert.Equal(Cli.ArtifactPromotionStatus.Blocked, result.Status);
        Assert.Equal(existing, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
        Assert.NotNull(result.EvidencePath);
        Assert.Equal(blocked, repo.Read(result.EvidencePath!));
        Cli.ArtifactLifecycleEntry evidence = Assert.Single(await lifecycle.LoadAsync(), item => item.Path == result.EvidencePath);
        Assert.Equal(Cli.ArtifactLifecycleState.Blocked, evidence.State);
    }

    [Fact]
    public async Task Ambiguous_promotion_preserves_existing_authoritative_artifact()
    {
        using var repo = new TempRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, existing);
        var service = new Cli.ArtifactPromotionService(repo.Artifacts, new Cli.ArtifactLifecycleStore(repo.Artifacts));

        Cli.ArtifactPromotionResult result = await service.PromoteAsync(Request("I cannot safely decide what to write."));

        Assert.False(result.Promoted);
        Assert.Equal(Cli.ArtifactPromotionStatus.Ambiguous, result.Status);
        Assert.Equal(existing, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
    }

    [Fact]
    public async Task Structurally_invalid_promotion_preserves_existing_authoritative_artifact()
    {
        using var repo = new TempRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, existing);
        var service = new Cli.ArtifactPromotionService(repo.Artifacts, new Cli.ArtifactLifecycleStore(repo.Artifacts));
        string invalid = """
            # Epic: Missing Structure

            ## Epic Metadata

            | Field | Value |
            |---|---|
            | Epic ID | EPIC-BAD |
            | Status | Authored |
            """;

        Cli.ArtifactPromotionResult result = await service.PromoteAsync(Request(invalid));

        Assert.False(result.Promoted);
        Assert.Equal(Cli.ArtifactPromotionStatus.StructurallyInvalid, result.Status);
        Assert.Equal(existing, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
    }

    [Fact]
    public async Task Successful_rewrite_replaces_existing_authoritative_artifact()
    {
        using var repo = new TempRepo();
        string existing = RoadmapSamples.ValidEpic("Existing Epic", "EPIC-OLD");
        string replacement = RoadmapSamples.ValidEpic("Replacement Epic", "EPIC-NEW", "Realigned", "Realign");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, existing);
        var service = new Cli.ArtifactPromotionService(repo.Artifacts, new Cli.ArtifactLifecycleStore(repo.Artifacts));

        Cli.ArtifactPromotionResult result = await service.PromoteAsync(Request(replacement));

        Assert.True(result.Promoted);
        Assert.Equal(replacement, repo.Read(Cli.RoadmapArtifactPaths.ActiveEpic));
    }

    private static Cli.ArtifactPromotionRequest Request(string candidate) =>
        new(
            Cli.RoadmapArtifactPaths.ActiveEpic,
            candidate,
            Cli.RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "active-epic-promotion",
            "active epic",
            new EpicAuthoringOutputClassifier(),
            new Cli.EpicArtifactValidator(),
            Cli.ArtifactLifecycleState.Ready,
            "Test promotion.");
}
