using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Cli.Tests.Services.Agents;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Services;
using Microsoft.Data.Sqlite;
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
    public async Task LoopHistoryStoreFactory_FallsBackToFileBackedStoreWhenDatabaseIsMissing()
    {
        var (_, store, repo) = New();

        ILoopHistoryStore history = LoopHistoryStoreFactory.Create(store, repo);

        Assert.IsType<FileBackedLoopHistoryStore>(history);
    }

    [Fact]
    public async Task LoopHistoryStoreFactory_UsesSqliteStoreForImportedWorkspaceDatabase()
    {
        using var repo = new TempFileRepo();
        await InitializeLoopHistoryDatabaseAsync(repo.Repository);

        ILoopHistoryStore history = LoopHistoryStoreFactory.Create(repo.Store, repo.Repository);

        Assert.IsType<SqliteLoopHistoryStore>(history);
    }

    [Fact]
    public async Task PersistDecisions_WritesLiveFileAndSqliteHistory()
    {
        using var repo = new TempFileRepo();
        await InitializeLoopHistoryDatabaseAsync(repo.Repository);
        var history = new SqliteLoopHistoryStore(repo.Repository);
        var artifacts = new LoopArtifacts(repo.Store, repo.Repository, history);

        await artifacts.PersistDecisionsAsync("D1\r\nopaque body", TestAgentConfiguration.Execution);

        Assert.Equal("D1\r\nopaque body", await repo.Store.ReadAsync(repo.Resolve(OrchestrationArtifactPaths.Decisions)));
        Assert.False(await repo.Store.ExistsAsync(repo.Resolve(OrchestrationArtifactPaths.HistoricalDecision(1))));
        LoopHistoryRecord latest = (await history.ReadLatestAsync(LoopHistoryKind.Decisions))!;
        Assert.Equal(1, latest.Sequence);
        Assert.Equal(OrchestrationArtifactPaths.HistoricalDecision(1), latest.RelativePath);
        Assert.Equal("D1\r\nopaque body", latest.Content);
    }

    [Fact]
    public async Task RotateLiveHandoff_WritesSqliteHistoryBeforeDeletingLiveFile()
    {
        using var repo = new TempFileRepo();
        await InitializeLoopHistoryDatabaseAsync(repo.Repository);
        var history = new SqliteLoopHistoryStore(repo.Repository);
        var artifacts = new LoopArtifacts(repo.Store, repo.Repository, history);
        await repo.Store.WriteAsync(repo.Resolve(OrchestrationArtifactPaths.LiveHandoff), "H1");

        string? rotated = await artifacts.RotateLiveHandoffAsync();

        Assert.Equal("H1", rotated);
        Assert.False(await repo.Store.ExistsAsync(repo.Resolve(OrchestrationArtifactPaths.LiveHandoff)));
        Assert.False(await repo.Store.ExistsAsync(repo.Resolve(OrchestrationArtifactPaths.HistoricalHandoff(1))));
        LoopHistoryRecord latest = (await history.ReadLatestAsync(LoopHistoryKind.Handoff))!;
        Assert.Equal(OrchestrationArtifactPaths.HistoricalHandoff(1), latest.RelativePath);
        Assert.Equal("H1", latest.Content);
    }

    [Fact]
    public async Task RotateOperationalDelta_WritesSqliteHistoryBeforeDeletingLiveFile()
    {
        using var repo = new TempFileRepo();
        await InitializeLoopHistoryDatabaseAsync(repo.Repository);
        var history = new SqliteLoopHistoryStore(repo.Repository);
        var artifacts = new LoopArtifacts(repo.Store, repo.Repository, history);
        await repo.Store.WriteAsync(repo.Resolve(OrchestrationArtifactPaths.OperationalDelta), "DELTA-1");

        string? rotated = await artifacts.RotateOperationalDeltaAsync();

        Assert.Equal("DELTA-1", rotated);
        Assert.False(await repo.Store.ExistsAsync(repo.Resolve(OrchestrationArtifactPaths.OperationalDelta)));
        Assert.False(await repo.Store.ExistsAsync(repo.Resolve(OrchestrationArtifactPaths.HistoricalDelta(1))));
        LoopHistoryRecord latest = (await history.ReadLatestAsync(LoopHistoryKind.OperationalDelta))!;
        Assert.Equal(OrchestrationArtifactPaths.HistoricalDelta(1), latest.RelativePath);
        Assert.Equal("DELTA-1", latest.Content);
    }

    [Fact]
    public async Task ReadLatestHandoff_PrefersLiveFileBeforeSqliteHistory()
    {
        using var repo = new TempFileRepo();
        await InitializeLoopHistoryDatabaseAsync(repo.Repository);
        var history = new SqliteLoopHistoryStore(repo.Repository);
        var artifacts = new LoopArtifacts(repo.Store, repo.Repository, history);
        await history.AppendAsync(LoopHistoryKind.Handoff, "numbered");

        var numbered = await artifacts.ReadLatestHandoffAsync();
        await repo.Store.WriteAsync(repo.Resolve(OrchestrationArtifactPaths.LiveHandoff), "live");
        var live = await artifacts.ReadLatestHandoffAsync();

        Assert.Equal("numbered", numbered.Content);
        Assert.Equal(OrchestrationArtifactPaths.HistoricalHandoff(1), numbered.RelativePath);
        Assert.Equal("live", live.Content);
        Assert.Equal(OrchestrationArtifactPaths.LiveHandoff, live.RelativePath);
    }

    [Fact]
    public async Task RotateLiveFile_WhenHistoryWriteFails_KeepsLiveFileAvailable()
    {
        var (_, store, repo) = New();
        var artifacts = new LoopArtifacts(store, repo, new ThrowingLoopHistoryStore());
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff), "H1");

        await Assert.ThrowsAsync<IOException>(() => artifacts.RotateLiveHandoffAsync());

        Assert.Equal("H1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.LiveHandoff)));
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
        await art.PersistDecisionsAsync("D1", TestAgentConfiguration.Execution);

        Assert.Equal("D1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions)));
        Assert.Equal("D1", await store.ReadAsync(Resolve(repo, OrchestrationArtifactPaths.HistoricalDecision(1))));
    }

    [Fact]
    public async Task PersistDecisions_DoesNotThrow_AndWritesBothPaths_AcrossMultipleCalls()
    {
        var (art, store, repo) = New();

        await art.PersistDecisionsAsync("D1", TestAgentConfiguration.Execution);
        // Delete the live decisions.md so NextSequence re-scans the directory correctly.
        await store.DeleteAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions));
        await art.PersistDecisionsAsync("D2", TestAgentConfiguration.Execution);

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

    private sealed class ThrowingLoopHistoryStore : ILoopHistoryStore
    {
        public Task<LoopHistoryRecord> AppendAsync(LoopHistoryKind kind, string content) =>
            throw new IOException("Configured history write failure.");

        public Task<LoopHistoryRecord?> ReadLatestAsync(LoopHistoryKind kind) =>
            Task.FromResult<LoopHistoryRecord?>(null);
    }

    private sealed class TempFileRepo : IDisposable
    {
        public TempFileRepo()
        {
            Root = Path.Combine(Path.GetTempPath(), "looprelay-cli-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Repository = new Repository
            {
                Id = Guid.NewGuid(),
                Name = "repo",
                Path = Root,
            };
            Store = new FileSystemArtifactStore();
        }

        public string Root { get; }

        public Repository Repository { get; }

        public FileSystemArtifactStore Store { get; }

        public string Resolve(string relativePath) =>
            ArtifactPath.ResolveRepositoryPath(Repository, relativePath);

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Persisted_decision_pair_loads_as_validated_execution_configuration()
    {
        var (artifacts, _, _) = New();
        await artifacts.PersistDecisionsAsync("exact prompt", TestAgentConfiguration.Execution);

        var validated = await artifacts.ReadValidatedExecutionRecommendationAsync();

        Assert.Equal("exact prompt", validated.Prompt);
        Assert.Equal(TestAgentConfiguration.Execution.Model, validated.Model);
        Assert.Equal(TestAgentConfiguration.Execution.Effort, validated.Effort);
    }

    [Fact]
    public async Task Missing_recommendation_blocks_execution_configuration_loading()
    {
        var (artifacts, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions), "prompt");

        await Assert.ThrowsAsync<InvalidDataException>(
            artifacts.ReadValidatedExecutionRecommendationAsync);
    }

    [Fact]
    public async Task Prompt_hash_mismatch_blocks_execution_configuration_loading()
    {
        var (artifacts, store, repo) = New();
        await artifacts.PersistDecisionsAsync("prompt one", TestAgentConfiguration.Execution);
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Decisions), "prompt two");

        await Assert.ThrowsAsync<InvalidDataException>(
            artifacts.ReadValidatedExecutionRecommendationAsync);
    }

    private static async Task InitializeLoopHistoryDatabaseAsync(Repository repository)
    {
        string databasePath = LoopWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_metadata(
                key text primary key,
                value text not null
            );

            CREATE TABLE IF NOT EXISTS workspace_metadata(
                key text primary key,
                value text not null
            );

            CREATE TABLE IF NOT EXISTS loop_history(
                kind text not null,
                sequence integer not null,
                logical_path text not null unique,
                body text not null,
                content_hash text not null,
                created_at text not null,
                primary key(kind, sequence)
            );

            CREATE INDEX IF NOT EXISTS idx_loop_history_kind_sequence_desc
            ON loop_history(kind, sequence desc);

            INSERT INTO schema_metadata (key, value)
            VALUES ('schema_version', '1')
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;

            INSERT INTO workspace_metadata (key, value)
            VALUES ('persistence_state', 'imported')
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        await command.ExecuteNonQueryAsync();
    }
}
