using System.Text.Json;
using LoopRelay.Cli;
using Xunit;

namespace LoopRelay.Cli.Tests;

public class RotatingJsonlTelemetrySinkTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "cc-tel-" + Guid.NewGuid().ToString("N"));

    private static Cli.SessionTelemetryRecord Rec(string repo) =>
        new(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero), repo, null, "sid", "Decision", 1,
            10, 5, 0, 15.0, 89, 88);

    public void Dispose() { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }

    [Fact]
    public void Append_WritesOneJsonLineToTodaysZeroSequenceFile_CreatingTheDirectory()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero) };
        var sink = new Cli.RotatingJsonlTelemetrySink(dir, clock);

        sink.Append(Rec("a"));

        string file = Path.Combine(dir, "sessions.2026-07-01.0000.jsonl");
        Assert.True(File.Exists(file));
        string[] lines = File.ReadAllLines(file);
        Assert.Single(lines);
        using JsonDocument doc = JsonDocument.Parse(lines[0]);
        Assert.Equal("a", doc.RootElement.GetProperty("repoName").GetString());
    }

    [Fact]
    public void Append_WhenActiveFileExceedsSizeCap_RollsToNextSequence_KeepingTheOld()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero) };
        var sink = new Cli.RotatingJsonlTelemetrySink(dir, clock, maxBytes: 1); // any record exceeds 1 byte

        sink.Append(Rec("first"));
        sink.Append(Rec("second"));

        string f0 = Path.Combine(dir, "sessions.2026-07-01.0000.jsonl");
        string f1 = Path.Combine(dir, "sessions.2026-07-01.0001.jsonl");
        Assert.True(File.Exists(f0));
        Assert.True(File.Exists(f1)); // rolled
        Assert.Contains("first", File.ReadAllText(f0));
        Assert.Contains("second", File.ReadAllText(f1));
    }

    [Fact]
    public void Append_OnANewDay_StartsAFreshZeroSequenceFile()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 7, 1, 23, 0, 0, TimeSpan.Zero) };
        var sink = new Cli.RotatingJsonlTelemetrySink(dir, clock);
        sink.Append(Rec("day1"));

        clock.UtcNow = new DateTimeOffset(2026, 7, 2, 1, 0, 0, TimeSpan.Zero);
        sink.Append(Rec("day2"));

        Assert.True(File.Exists(Path.Combine(dir, "sessions.2026-07-01.0000.jsonl")));
        Assert.True(File.Exists(Path.Combine(dir, "sessions.2026-07-02.0000.jsonl")));
    }
}
