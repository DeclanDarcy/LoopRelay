using LoopRelay.Cli;
using Xunit;

namespace LoopRelay.Cli.Tests;

public class FileSystemCodexRolloutLocatorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cc-codex-" + Guid.NewGuid().ToString("N"));

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }

    // Writes a rollout file whose first line is a codex session_meta record.
    private string WriteRollout(string name, string cwd, DateTimeOffset started)
    {
        string day = Path.Combine(root, "2026", "07", "01");
        Directory.CreateDirectory(day);
        string file = Path.Combine(day, name);
        string meta =
            "{\"timestamp\":\"" + started.UtcDateTime.ToString("o") + "\",\"type\":\"session_meta\"," +
            "\"payload\":{\"session_id\":\"" + Guid.NewGuid().ToString() + "\",\"cwd\":" +
            System.Text.Json.JsonSerializer.Serialize(cwd) + ",\"timestamp\":\"" + started.UtcDateTime.ToString("o") + "\"}}";
        File.WriteAllText(file, meta + "\n{\"type\":\"event\"}\n");
        return file;
    }

    [Fact]
    public void Resolve_ReturnsRolloutWhoseCwdMatchesAndStartedAfterOpen()
    {
        string cwd = Path.Combine(root, "work");
        string expected = WriteRollout("rollout-a.jsonl", cwd, new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));

        string? found = new Cli.FileSystemCodexRolloutLocator(root)
            .Resolve(cwd, new DateTimeOffset(2026, 7, 1, 9, 59, 0, TimeSpan.Zero));

        Assert.Equal(expected, found);
    }

    [Fact]
    public void Resolve_IgnoresRolloutsForADifferentCwd()
    {
        WriteRollout("rollout-a.jsonl", Path.Combine(root, "other"), new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));

        string? found = new Cli.FileSystemCodexRolloutLocator(root)
            .Resolve(Path.Combine(root, "work"), new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero));

        Assert.Null(found);
    }

    [Fact]
    public void Resolve_WhenMultipleMatch_ReturnsTheNewest()
    {
        string cwd = Path.Combine(root, "work");
        WriteRollout("rollout-old.jsonl", cwd, new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));
        string newer = WriteRollout("rollout-new.jsonl", cwd, new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.Zero));

        string? found = new Cli.FileSystemCodexRolloutLocator(root)
            .Resolve(cwd, new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(newer, found);
    }

    [Fact]
    public void Resolve_WhenRootMissing_ReturnsNull()
    {
        string? found = new Cli.FileSystemCodexRolloutLocator(Path.Combine(root, "nope"))
            .Resolve(root, DateTimeOffset.MinValue);
        Assert.Null(found);
    }

    [Fact]
    public void Resolve_SkipsFilesWithMalformedFirstLine()
    {
        string day = Path.Combine(root, "2026", "07", "01");
        Directory.CreateDirectory(day);
        File.WriteAllText(Path.Combine(day, "rollout-bad.jsonl"), "not json\n");

        string? found = new Cli.FileSystemCodexRolloutLocator(root).Resolve(root, DateTimeOffset.MinValue);
        Assert.Null(found);
    }
}
