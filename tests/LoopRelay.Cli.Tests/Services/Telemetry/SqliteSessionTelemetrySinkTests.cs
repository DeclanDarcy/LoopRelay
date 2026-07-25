using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Telemetry;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Telemetry;

public sealed class SqliteSessionTelemetrySinkTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cc-sqlite-telemetry-" + Guid.NewGuid().ToString("N"));

    private Repository Repository => new() { Id = Guid.NewGuid(), Name = "repo", Path = root };

    private string DatabasePath => LoopRelayWorkspaceDatabase.Resolve(Repository);

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Append_WritesCanonicalSqliteEvent_AndProtectsRuntimeDirectory()
    {
        var sink = new SqliteSessionTelemetrySink(Repository);

        sink.Append(Record("repo", turnIndex: 1));

        Assert.Equal("*\n", await File.ReadAllTextAsync(Path.Combine(root, ".LoopRelay", ".gitignore")));
        TelemetryRow row = Assert.Single(await ReadRowsAsync());
        Assert.Equal(1, row.EventId);
        Assert.Equal("repo", row.RepoName);
        Assert.Equal("Decision", row.SessionType);
        Assert.Equal(1, row.TurnIndex);
        Assert.Equal(Sha256(row.DocumentJson), row.ContentHash);
        using JsonDocument document = JsonDocument.Parse(row.DocumentJson);
        Assert.Equal("repo", document.RootElement.GetProperty("repoName").GetString());
    }

    [Fact]
    public async Task Append_MultipleEvents_AreOrderedByAutoincrementId()
    {
        var sink = new SqliteSessionTelemetrySink(Repository);

        sink.Append(Record("repo", turnIndex: 2));
        sink.Append(Record("repo", turnIndex: 3));

        TelemetryRow[] rows = await ReadRowsAsync();
        Assert.Equal([1L, 2L], rows.Select(row => row.EventId).ToArray());
        Assert.Equal([2, 3], rows.Select(row => row.TurnIndex).ToArray());
    }

    /// <summary>
    /// Load-bearing assertion for PERF task w2-t4: directory create, gitignore probe, and schema
    /// ensure must run exactly once per sink lifetime, not once per append. Before the fix, this
    /// fails with <c>InitializationCount == 2</c> because <see cref="SqliteSessionTelemetrySink"/>
    /// re-ran that whole block on every <c>Append</c> call.
    /// </summary>
    [Fact]
    public void Append_TwoEvents_RunsFullInitializationExactlyOnce()
    {
        var sink = new SqliteSessionTelemetrySink(Repository);

        sink.Append(Record("repo", turnIndex: 1));
        sink.Append(Record("repo", turnIndex: 2));

        Assert.Equal(1, sink.InitializationCount);
    }

    /// <summary>
    /// Fail-open + retryability: a first-append init failure (here, forced by occupying the
    /// runtime directory's path with a plain file so <c>Directory.CreateDirectory</c> throws)
    /// must not throw out of <c>Append</c>, and must not permanently poison the sink - once the
    /// obstruction is removed, a later append must retry init and succeed.
    /// </summary>
    [Fact]
    public async Task Append_WhenFirstInitFails_DoesNotThrow_AndRetriesOnNextAppend()
    {
        Directory.CreateDirectory(root);
        string runtimeDirectoryPath = Path.Combine(root, ".LoopRelay");
        File.WriteAllText(runtimeDirectoryPath, "blocking file occupies the runtime directory path");
        var sink = new SqliteSessionTelemetrySink(Repository);

        Exception? firstAppendFailure = Xunit.Record.Exception(() => sink.Append(Record("repo", turnIndex: 1)));
        Assert.Null(firstAppendFailure);
        Assert.Equal(0, sink.InitializationCount);

        File.Delete(runtimeDirectoryPath);
        Exception? secondAppendFailure = Xunit.Record.Exception(() => sink.Append(Record("repo", turnIndex: 2)));
        Assert.Null(secondAppendFailure);

        TelemetryRow[] rows = await ReadRowsAsync();
        TelemetryRow row = Assert.Single(rows);
        Assert.Equal(2, row.TurnIndex);
        Assert.Equal(1, sink.InitializationCount);
    }

    /// <summary>Concurrency guard: concurrent appends from multiple threads must all land, and
    /// init must still run exactly once, proving the lock correctly serializes cached-state
    /// access without corrupting it or double-initializing destructively.</summary>
    [Fact]
    public async Task Append_ConcurrentAppends_AllRowsWritten_AndInitializationRunsOnce()
    {
        var sink = new SqliteSessionTelemetrySink(Repository);
        const int threads = 8;
        const int perThread = 5;

        Parallel.For(0, threads, t =>
        {
            for (int i = 0; i < perThread; i++)
            {
                sink.Append(Record("repo", turnIndex: (t * perThread) + i));
            }
        });

        TelemetryRow[] rows = await ReadRowsAsync();
        Assert.Equal(threads * perThread, rows.Length);
        Assert.Equal(1, sink.InitializationCount);
    }

    private static SessionTelemetryRecord Record(string repo, int turnIndex) =>
        new(
            new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero),
            repo,
            "/codex/log.jsonl",
            "sid",
            "Decision",
            turnIndex,
            10,
            5,
            1,
            14.1,
            89,
            88);

    private async Task<TelemetryRow[]> ReadRowsAsync()
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(DatabasePath);
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT event_id, repo_name, session_type, turn_index, document_json, content_hash
            FROM session_telemetry_events
            ORDER BY event_id;
            """;
        var rows = new List<TelemetryRow>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new TelemetryRow(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5)));
        }

        return rows.ToArray();
    }

    private static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private sealed record TelemetryRow(
        long EventId,
        string RepoName,
        string SessionType,
        int TurnIndex,
        string DocumentJson,
        string ContentHash);
}
