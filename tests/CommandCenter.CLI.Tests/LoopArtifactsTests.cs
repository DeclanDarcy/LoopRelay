using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class LoopArtifactsTests
{
    private static (LoopArtifacts Art, IArtifactStore Store, Repository Repo) New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        return (new LoopArtifacts(store, repo), store, repo);
    }

    private static string Resolve(Repository r, string rel) => ArtifactPath.ResolveRepositoryPath(r, rel);

    [Fact]
    public async Task RotateLiveHandoff_ArchivesNumberedAndDeletesLive()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        string? rotated = await art.RotateLiveHandoffAsync();

        Assert.Equal("H1", rotated);
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff)));
        Assert.Equal("H1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalHandoff(1))));
    }

    [Fact]
    public async Task RotateLiveHandoff_WhenAbsent_ReturnsNull()
    {
        var (art, _, _) = New();
        Assert.Null(await art.RotateLiveHandoffAsync());
    }

    [Fact]
    public async Task RotateLiveHandoff_SequenceIsDiskMaxPlusOne()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalHandoff(1)), "old");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalHandoff(2)), "old2");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H3");

        await art.RotateLiveHandoffAsync();

        Assert.Equal("H3", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalHandoff(3))));
    }

    [Fact]
    public async Task ReadLatestHandoff_PrefersLiveThenHighestNumbered()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalHandoff(1)), "n1");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalHandoff(2)), "n2");

        var numbered = await art.ReadLatestHandoffAsync();
        Assert.Equal("n2", numbered.Content);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "live");
        var live = await art.ReadLatestHandoffAsync();
        Assert.Equal("live", live.Content);
    }

    [Fact]
    public async Task RotateLiveDecisions_ArchivesNumberedAndDeletesLive()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions), "D1");

        string? rotated = await art.RotateLiveDecisionsAsync();

        Assert.Equal("D1", rotated);
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal("D1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDecision(1))));
    }

    [Fact]
    public async Task ReadLatestDecisions_PrefersLiveThenHighestNumbered()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDecision(1)), "n1");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDecision(2)), "n2");

        var numbered = await art.ReadLatestDecisionsAsync();
        Assert.Equal("n2", numbered.Content);

        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions), "live");
        var live = await art.ReadLatestDecisionsAsync();
        Assert.Equal("live", live.Content);
    }

    [Fact]
    public async Task RotateOperationalDelta_ArchivesNumberedAndDeletesLive()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta), "DELTA-A");

        string? rotated = await art.RotateOperationalDeltaAsync();

        Assert.Equal("DELTA-A", rotated);
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta)));
        Assert.Equal("DELTA-A", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(1))));
    }

    [Fact]
    public async Task RotateOperationalDelta_WhenAbsent_ReturnsNull()
    {
        var (art, _, _) = New();
        Assert.Null(await art.RotateOperationalDeltaAsync());
    }

    [Fact]
    public async Task RotateOperationalDelta_SequenceIsDiskMaxPlusOne()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(1)), "old");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(2)), "old2");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalDelta), "DELTA-3");

        await art.RotateOperationalDeltaAsync();

        Assert.Equal("DELTA-3", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDelta(3))));
    }

    [Fact]
    public async Task EnsureOperationalContext_WhenAlreadyExists_DoesNotOverwrite()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "EXISTING");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");

        await art.EnsureOperationalContextAsync();

        Assert.Equal("EXISTING", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext)));
    }

    [Fact]
    public async Task EnsureOperationalContext_WhenPlanAbsent_WritesNothing()
    {
        var (art, store, repo) = New();

        await art.EnsureOperationalContextAsync();

        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext)));
    }

    [Fact]
    public async Task PersistDecisions_WritesNumberedAndCanonical()
    {
        var (art, store, repo) = New();
        await art.PersistDecisionsAsync("D1");

        Assert.Equal("D1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal("D1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDecision(1))));
    }

    [Fact]
    public async Task PersistDecisions_DoesNotThrow_AndWritesBothPaths_AcrossMultipleCalls()
    {
        var (art, store, repo) = New();

        await art.PersistDecisionsAsync("D1");
        // Delete the live decisions.md so NextSequence re-scans the directory correctly.
        await store.DeleteAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions));
        await art.PersistDecisionsAsync("D2");

        Assert.Equal("D1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDecision(1))));
        Assert.Equal("D2", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDecision(2))));
        Assert.Equal("D2", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
    }

    [Fact]
    public async Task EnsureOperationalContext_CopiesPlanWhenMissing()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");

        await art.EnsureOperationalContextAsync();

        Assert.Equal("PLAN", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext)));
    }
}
