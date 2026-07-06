using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Services;
using Xunit;

namespace LoopRelay.Cli.Tests;

/// <summary>Real-filesystem tests (temp dir per test, like RotatingJsonlTelemetrySinkTests).</summary>
public sealed class FileDecisionSessionResumeStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cc-resume-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> warnings = new();

    private FileDecisionSessionResumeStore NewStore() =>
        new(new Repository { Id = Guid.NewGuid(), Name = "r", Path = root }, warnings.Add);

    private static DecisionSessionResumeState State(string threadId = "thread-1") =>
        new(threadId, 100, 5d, 2, 3d, 2d, 300_000d, 1, 500, 1);

    private string FilePath => Path.Combine(root, ".LoopRelay", "decision-session.json");

    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch { /* best-effort cleanup */ }
    }

    [Fact]
    public async Task WriteThenRead_RoundTripsEveryField_AndStampsSavedAtUtc()
    {
        FileDecisionSessionResumeStore store = NewStore();
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
        Assert.Equal(DecisionSessionResumeState.CurrentSchemaVersion, read.SchemaVersion);
        Assert.NotEqual(default, read.SavedAtUtc);
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task Read_WhenNoFile_ReturnsNullWithoutWarning()
    {
        Assert.Null(await NewStore().ReadAsync());
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task Read_CorruptJson_WarnsDeletesAndReturnsNull()
    {
        FileDecisionSessionResumeStore store = NewStore();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, "{not json");

        Assert.Null(await store.ReadAsync());

        Assert.False(File.Exists(FilePath));
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public async Task Read_WrongSchemaVersion_WarnsDeletesAndReturnsNull()
    {
        FileDecisionSessionResumeStore store = NewStore();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, """{"schemaVersion":999,"threadId":"thread-1"}""");

        Assert.Null(await store.ReadAsync());

        Assert.False(File.Exists(FilePath));
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public async Task Read_EmptyThreadId_WarnsDeletesAndReturnsNull()
    {
        FileDecisionSessionResumeStore store = NewStore();
        await store.WriteAsync(State(threadId: ""));

        Assert.Null(await store.ReadAsync());

        Assert.False(File.Exists(FilePath));
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public async Task Clear_IsIdempotent()
    {
        FileDecisionSessionResumeStore store = NewStore();
        await store.WriteAsync(State());

        await store.ClearAsync();
        await store.ClearAsync(); // deleting nothing is a no-op, never a warning

        Assert.False(File.Exists(FilePath));
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task Write_CreatesTheSelfIgnoringGitignore_AndNeverOverwritesAnExistingOne()
    {
        FileDecisionSessionResumeStore store = NewStore();
        await store.WriteAsync(State());

        string gitignore = Path.Combine(root, ".LoopRelay", ".gitignore");
        Assert.Equal("*\n", await File.ReadAllTextAsync(gitignore));

        await File.WriteAllTextAsync(gitignore, "custom");
        await store.WriteAsync(State("thread-2"));
        Assert.Equal("custom", await File.ReadAllTextAsync(gitignore));
    }

    [Fact]
    public async Task JsonOnDisk_IsCompactCamelCase()
    {
        FileDecisionSessionResumeStore store = NewStore();
        await store.WriteAsync(State());

        string json = await File.ReadAllTextAsync(FilePath);
        Assert.Contains("\"threadId\":\"thread-1\"", json);
        Assert.Contains("\"schemaVersion\":1", json);
        Assert.DoesNotContain("\n", json.TrimEnd());
    }

    [Fact]
    public async Task EnsureDirectoryProtection_CreatesTheSelfIgnoringDirectory_WithoutWritingState()
    {
        FileDecisionSessionResumeStore store = NewStore();

        store.EnsureDirectoryProtection();

        string gitignore = Path.Combine(root, ".LoopRelay", ".gitignore");
        Assert.Equal("*\n", await File.ReadAllTextAsync(gitignore));
        Assert.False(File.Exists(FilePath));
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task EnsureDirectoryProtection_NeverOverwritesAnExistingGitignore()
    {
        FileDecisionSessionResumeStore store = NewStore();
        store.EnsureDirectoryProtection();
        string gitignore = Path.Combine(root, ".LoopRelay", ".gitignore");
        await File.WriteAllTextAsync(gitignore, "custom");

        store.EnsureDirectoryProtection();

        Assert.Equal("custom", await File.ReadAllTextAsync(gitignore));
    }
}
