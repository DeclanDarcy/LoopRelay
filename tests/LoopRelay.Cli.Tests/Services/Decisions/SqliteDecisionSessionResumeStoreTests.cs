using LoopRelay.Cli.Services.Decisions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Models;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Decisions;

public sealed class SqliteDecisionSessionResumeStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cc-sqlite-resume-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> warnings = [];
    private Repository Repository => new() { Id = Guid.NewGuid(), Name = "r", Path = root };
    private string DatabasePath => LoopRelayWorkspaceDatabase.Resolve(Repository);
    private SqliteDecisionSessionResumeStore NewStore() => new(Repository, warnings.Add);
    private static DecisionSessionResumeState State() =>
        new("thread-1", 100, 5d, 2, 3d, 2d, 300_000d, 1, 500, 1);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Write_read_and_clear_round_trip_only_through_canonical_sqlite()
    {
        SqliteDecisionSessionResumeStore store = NewStore();
        await store.WriteAsync(State());

        DecisionSessionResumeState? read = await store.ReadAsync();

        Assert.NotNull(read);
        Assert.Equal("thread-1", read!.ThreadId);
        Assert.NotEqual(default, read.SavedAtUtc);
        Assert.False(File.Exists(Path.Combine(root, ".LoopRelay", "decision-session.json")));
        Assert.Equal(1, await ResumeRowCountAsync());
        await store.ClearAsync();
        Assert.Equal(0, await ResumeRowCountAsync());
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task Legacy_file_is_not_imported_or_deleted_after_adapter_exhaustion()
    {
        string legacy = Path.Combine(root, ".LoopRelay", "decision-session.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        await File.WriteAllTextAsync(legacy, "{}");

        DecisionSessionResumeState? read = await NewStore().ReadAsync();

        Assert.Null(read);
        Assert.True(File.Exists(legacy));
        Assert.False(File.Exists(DatabasePath));
    }

    [Fact]
    public async Task Ensure_directory_protection_creates_schema_without_legacy_side_files()
    {
        NewStore().EnsureDirectoryProtection();

        Assert.True(File.Exists(DatabasePath));
        Assert.False(File.Exists(Path.Combine(root, ".LoopRelay", ".gitignore")));
        Assert.Equal(0, await ResumeRowCountAsync());
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
}
