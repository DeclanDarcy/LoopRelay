using System.Text.Json;
using LoopRelay.Cli.Services.Decisions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Decisions;

public sealed class SqliteDecisionSessionResumeStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cc-sqlite-resume-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> warnings = new();

    private Repository Repository => new() { Id = Guid.NewGuid(), Name = "r", Path = root };

    private SqliteDecisionSessionResumeStore NewStore() =>
        new(Repository, warnings.Add);

    private static DecisionSessionResumeState State(string threadId = "thread-1") =>
        new(threadId, 100, 5d, 2, 3d, 2d, 300_000d, 1, 500, 1);

    private string LegacyFilePath => Path.Combine(root, ".LoopRelay", "decision-session.json");

    private string DatabasePath => LoopRelayWorkspaceDatabase.Resolve(Repository);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public async Task WriteThenRead_RoundTripsThroughSqlite_AndStampsSavedAtUtc()
    {
        SqliteDecisionSessionResumeStore store = NewStore();

        await store.WriteAsync(State());
        DecisionSessionResumeState? read = await store.ReadAsync();

        Assert.NotNull(read);
        Assert.Equal("thread-1", read!.ThreadId);
        Assert.Equal(100, read.OccupancyTokens);
        Assert.Equal(5d, read.ReuseCost);
        Assert.Equal(2, read.ReuseCycles);
        Assert.Equal(3d, read.LastCycleCost);
        Assert.Equal(2d, read.PrevCycleCost);
        Assert.Equal(300_000d, read.TransferCost);
        Assert.Equal(1, read.TransferCount);
        Assert.Equal(500, read.PreviousOperationalContextSize);
        Assert.Equal(1, read.OperationalContextGrowthStreak);
        Assert.NotEqual(default, read.SavedAtUtc);
        Assert.False(File.Exists(LegacyFilePath));
        Assert.Equal(1, await ResumeRowCountAsync());
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task Read_ImportsValidLegacyStateOnce_AndDeletesTheLegacyFile()
    {
        var legacy = new FileDecisionSessionResumeStore(Repository, warnings.Add);
        await legacy.WriteAsync(State("legacy-thread"));
        SqliteDecisionSessionResumeStore store = NewStore();

        DecisionSessionResumeState? read = await store.ReadAsync();

        Assert.NotNull(read);
        Assert.Equal("legacy-thread", read!.ThreadId);
        Assert.False(File.Exists(LegacyFilePath));
        Assert.Equal(1, await ResumeRowCountAsync());
        Assert.Equal("legacy-thread", await StoredThreadIdAsync());
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task Read_InvalidLegacyState_WarnsDeletesAndDoesNotCreateCanonicalState()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LegacyFilePath)!);
        await File.WriteAllTextAsync(LegacyFilePath, """{"schemaVersion":999,"threadId":"thread-1"}""");
        SqliteDecisionSessionResumeStore store = NewStore();

        DecisionSessionResumeState? read = await store.ReadAsync();

        Assert.Null(read);
        Assert.False(File.Exists(LegacyFilePath));
        Assert.False(File.Exists(DatabasePath));
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public async Task Clear_RemovesCanonicalAndLegacyState()
    {
        SqliteDecisionSessionResumeStore store = NewStore();
        await store.WriteAsync(State());
        Directory.CreateDirectory(Path.GetDirectoryName(LegacyFilePath)!);
        await File.WriteAllTextAsync(LegacyFilePath, "{}");

        await store.ClearAsync();
        await store.ClearAsync();

        Assert.Equal(0, await ResumeRowCountAsync());
        Assert.False(File.Exists(LegacyFilePath));
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task EnsureDirectoryProtection_CreatesSelfIgnoringRuntimeDirectoryAndSchemaWithoutState()
    {
        SqliteDecisionSessionResumeStore store = NewStore();

        store.EnsureDirectoryProtection();

        Assert.Equal("*\n", await File.ReadAllTextAsync(Path.Combine(root, ".LoopRelay", ".gitignore")));
        Assert.True(File.Exists(DatabasePath));
        Assert.Equal(0, await ResumeRowCountAsync());
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task EnsureDirectoryProtection_DoesNotOverwriteExistingGitignore()
    {
        SqliteDecisionSessionResumeStore store = NewStore();
        store.EnsureDirectoryProtection();
        string gitignore = Path.Combine(root, ".LoopRelay", ".gitignore");
        await File.WriteAllTextAsync(gitignore, "custom");

        store.EnsureDirectoryProtection();

        Assert.Equal("custom", await File.ReadAllTextAsync(gitignore));
        Assert.Empty(warnings);
    }

    private async Task<int> ResumeRowCountAsync()
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(DatabasePath);
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM decision_session_resume;";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<string?> StoredThreadIdAsync()
    {
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(DatabasePath);
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT document_json FROM decision_session_resume WHERE id = 1;";
        string json = Convert.ToString(await command.ExecuteScalarAsync())!;
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("threadId").GetString();
    }
}
