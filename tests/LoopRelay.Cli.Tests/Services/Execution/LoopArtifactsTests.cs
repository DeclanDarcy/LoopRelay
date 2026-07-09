using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Services;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Execution;

public class LoopArtifactsTests
{
    private static (LoopArtifacts Art, IArtifactStore Store, Repository Repo) New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        return (new LoopArtifacts(store, repo), store, repo);
    }

    private static string Resolve(Repository r, string rel) => ArtifactPath.ResolveRepositoryPath(r, rel);

    [Theory]
    [InlineData(nameof(LoopHistoryKind.Decisions), ".agents/decisions/decisions.0001.md")]
    [InlineData(nameof(LoopHistoryKind.Handoff), ".agents/handoffs/handoff.0001.md")]
    [InlineData(nameof(LoopHistoryKind.OperationalDelta), ".agents/deltas/operational_delta.0001.md")]
    public async Task FileBackedLoopHistoryStore_AppendsExpectedHistoricalPath(string kindName, string expectedRelativePath)
    {
        var (_, store, repo) = New();
        var history = new FileBackedLoopHistoryStore(store, repo);
        LoopHistoryKind kind = Enum.Parse<LoopHistoryKind>(kindName);

        LoopHistoryRecord record = await history.AppendAsync(kind, "BODY");

        Assert.Equal(kind, record.Kind);
        Assert.Equal(1, record.Sequence);
        Assert.Equal(expectedRelativePath, record.RelativePath);
        Assert.Equal("BODY", record.Content);
        Assert.Equal("BODY", await store.ReadAsync(Resolve(repo, expectedRelativePath)));
    }

    [Fact]
    public async Task FileBackedLoopHistoryStore_ReadLatestUsesHighestNumericSequence()
    {
        var (_, store, repo) = New();
        var history = new FileBackedLoopHistoryStore(store, repo);
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalHandoff(2)), "H2");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalHandoff(10)), "H10");

        LoopHistoryRecord? latest = await history.ReadLatestAsync(LoopHistoryKind.Handoff);

        Assert.NotNull(latest);
        Assert.Equal(10, latest.Sequence);
        Assert.Equal(OrchestrationArtifactPaths.HistoricalHandoff(10), latest.RelativePath);
        Assert.Equal("H10", latest.Content);
    }

    [Fact]
    public async Task ReadLatestDecisions_PrefersLiveBeforeHistoryStore()
    {
        var (_, store, repo) = New();
        var history = new RecordingLoopHistoryStore(
            new LoopHistoryRecord(LoopHistoryKind.Decisions, 1, OrchestrationArtifactPaths.HistoricalDecision(1), "numbered"));
        var art = new LoopArtifacts(store, repo, history);
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions), "live");

        var latest = await art.ReadLatestDecisionsAsync();

        Assert.Equal("live", latest.Content);
        Assert.Equal(OrchestrationArtifactPaths.Decisions, latest.RelativePath);
        Assert.Equal(0, history.ReadLatestCalls);
    }

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
    public async Task RetireLiveDecisions_DeletesLive_LeavingNumberedHistory()
    {
        var (art, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDecision(1)), "D1");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions), "D1");

        bool retired = await art.RetireLiveDecisionsAsync();

        Assert.True(retired);
        // The live pointer is dropped (so the next slice runs a fresh decision) but the numbered snapshot,
        // written at persist time, remains as the retained history — no re-archival, no duplicate.
        Assert.False(await store.ExistsAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal("D1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDecision(1))));
    }

    [Fact]
    public async Task RetireLiveDecisions_WhenAbsent_ReturnsFalse()
    {
        var (art, _, _) = New();
        Assert.False(await art.RetireLiveDecisionsAsync());
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

    private sealed class RecordingLoopHistoryStore(LoopHistoryRecord? latest) : ILoopHistoryStore
    {
        public int ReadLatestCalls { get; private set; }

        public Task<LoopHistoryRecord> AppendAsync(LoopHistoryKind kind, string content) =>
            Task.FromResult(new LoopHistoryRecord(kind, 1, ".agents/history.0001.md", content));

        public Task<LoopHistoryRecord?> ReadLatestAsync(LoopHistoryKind kind)
        {
            ReadLatestCalls++;
            return Task.FromResult(latest);
        }
    }
}
