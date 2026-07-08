using LoopRelay.Infrastructure.Artifacts;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class HitlArtifactCaptureTests
{
    [Fact]
    public async Task Capture_returns_without_ledger_write_when_service_is_absent()
    {
        using var repo = new TempRepo();
        var capture = new Cli.HitlArtifactCapture(null);

        await capture.CaptureAsync(Cli.RoadmapArtifactPaths.Selection, HitlSource());

        Assert.False(await repo.Artifacts.ExistsAsync(NonImplementationReviewLedgerStore.LedgerPath));
    }

    [Fact]
    public async Task Capture_returns_before_validating_path_when_content_is_blank()
    {
        using var repo = new TempRepo();
        var ledger = new NonImplementationReviewLedgerStore(new RepositoryArtifactStore(repo.Store, repo.Repository));
        var capture = new Cli.HitlArtifactCapture(new ExplicitHitlNonImplementationRequestCaptureService(ledger));

        await capture.CaptureAsync(" ", " \r\n\t ");

        Assert.False(await repo.Artifacts.ExistsAsync(NonImplementationReviewLedgerStore.LedgerPath));
    }

    [Fact]
    public async Task Capture_scans_named_artifact_and_lets_capture_service_update_ledger()
    {
        using var repo = new TempRepo();
        var ledger = new NonImplementationReviewLedgerStore(new RepositoryArtifactStore(repo.Store, repo.Repository));
        var capture = new Cli.HitlArtifactCapture(new ExplicitHitlNonImplementationRequestCaptureService(ledger));

        await capture.CaptureAsync(Cli.RoadmapArtifactPaths.ActiveEpic, HitlSource());

        NonImplementationHitlRequestEntry request = Assert.Single((await ledger.LoadOrCreateAsync()).HitlRequests);
        Assert.Equal("docs/roadmap-note.md", request.DeliverablePathOrPattern);
        Assert.Equal(Cli.RoadmapArtifactPaths.ActiveEpic, request.SourceArtifactPath);
        Assert.Equal(NonImplementationHitlProvenanceKind.HitlRequested, request.HitlProvenanceKind);
        Assert.Equal("Human explicitly requested the note.", request.Rationale);
    }

    private static string HitlSource() => """
        # Source Artifact

        ## HITL-Requested Non-Implementation Deliverables

        | Path Or Pattern | Source | Source Hash | Rationale |
        | --- | --- | --- | --- |
        | docs/roadmap-note.md | user | abc | Human explicitly requested the note. |
        """;
}
