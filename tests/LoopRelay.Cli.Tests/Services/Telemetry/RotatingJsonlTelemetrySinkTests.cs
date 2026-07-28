using System.Text.Json;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Telemetry;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Telemetry;

public class RotatingJsonlTelemetrySinkTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "cc-tel-" + Guid.NewGuid().ToString("N"));

    private static SessionTelemetryRecord Rec(string repo) =>
        new(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero), repo, null, "sid", "Decision", 1,
            10, 5, 0, 15.0, 89, 88);

    public void Dispose() { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }

    /// <summary>
    /// One fail-open retry, then propagate. The retry exists for a transient loss of the cached
    /// file; a directory path permanently occupied by a file is not transient, and swallowing it
    /// would leave a persistently broken sink silent. <c>SessionTelemetryRecorder</c> is the
    /// warn-and-continue boundary that keeps telemetry fail-open overall.
    /// </summary>
    [Fact]
    public void Append_WhenTheDirectoryPathIsAFile_PropagatesInsteadOfSwallowing()
    {
        File.WriteAllText(dir, "not a directory"); // occupies the sink's directory path
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero) };
        var sink = new RotatingJsonlTelemetrySink(dir, clock);

        Assert.Throws<IOException>(() => sink.Append(Rec("a")));

        File.Delete(dir);
    }

    [Fact]
    public void Append_WritesOneJsonLineToTodaysZeroSequenceFile_CreatingTheDirectory()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero) };
        var sink = new RotatingJsonlTelemetrySink(dir, clock);

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
        var sink = new RotatingJsonlTelemetrySink(dir, clock, maxBytes: 1); // any record exceeds 1 byte

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
        var sink = new RotatingJsonlTelemetrySink(dir, clock);
        sink.Append(Rec("day1"));

        clock.UtcNow = new DateTimeOffset(2026, 7, 2, 1, 0, 0, TimeSpan.Zero);
        sink.Append(Rec("day2"));

        Assert.True(File.Exists(Path.Combine(dir, "sessions.2026-07-01.0000.jsonl")));
        Assert.True(File.Exists(Path.Combine(dir, "sessions.2026-07-02.0000.jsonl")));
    }

#if DEBUG
    /// <summary>
    /// Load-bearing assertion for PERF task w2-t4: the O(files) candidate scan must run once per
    /// sink lifetime (until rollover or external deletion forces a re-scan), not once per append.
    /// Before the fix, this fails with <c>RescanCount == 2</c> because
    /// <see cref="RotatingJsonlTelemetrySink"/> re-ran the whole candidate-scanning loop on every
    /// <c>Append</c> call.
    /// </summary>
    [Fact]
    public void Append_TwoEvents_ScansForActiveFileExactlyOnce()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero) };
        var sink = new RotatingJsonlTelemetrySink(dir, clock);

        sink.Append(Rec("first"));
        sink.Append(Rec("second"));

        Assert.Equal(1, sink.RescanCount);
    }
#endif

#if DEBUG
    [Fact]
    public void Append_WhenActiveFileExceedsSizeCap_RescansExactlyOncePerRollover()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero) };
        var sink = new RotatingJsonlTelemetrySink(dir, clock, maxBytes: 1);

        sink.Append(Rec("first"));
        sink.Append(Rec("second"));

        // One scan resolves sequence 0000 on the first append; one more scan (triggered by the
        // cap crossing) resolves sequence 0001 on the second append.
        Assert.Equal(2, sink.RescanCount);
    }
#endif

    /// <summary>
    /// External deletion must be detected: if the active JSONL file is deleted out from under the
    /// sink, the next append must notice (via the cheap existence check, not by silently trusting
    /// stale in-memory state) and recover by recreating the same sequence file - not silently
    /// losing the line, and not skipping forward to the next sequence number as if the deleted
    /// file were still full.
    /// </summary>
    [Fact]
    public void Append_WhenActiveFileIsDeletedExternally_RecoversByRecreatingSameSequence()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero) };
        var sink = new RotatingJsonlTelemetrySink(dir, clock);
        sink.Append(Rec("first"));
        string file = Path.Combine(dir, "sessions.2026-07-01.0000.jsonl");
        Assert.True(File.Exists(file));

        File.Delete(file);
        sink.Append(Rec("second"));

        Assert.True(File.Exists(file));
        string[] lines = File.ReadAllLines(file);
        string line = Assert.Single(lines);
        Assert.Contains("second", line);
        Assert.False(File.Exists(Path.Combine(dir, "sessions.2026-07-01.0001.jsonl")));
    }

    /// <summary>
    /// An IO error that the one fail-open retry cannot clear (here, forced by occupying the sink's
    /// directory path with a plain file, so <c>Directory.CreateDirectory</c> can never succeed)
    /// propagates out of <c>Append</c> - on the first append and on every append after it, since
    /// nothing about the obstruction is transient. Telemetry remains fail-open at the
    /// <c>SessionTelemetryRecorder</c> boundary, which is where the warning belongs; swallowing
    /// here would make a permanently broken sink look identical to a working one.
    /// </summary>
    [Fact]
    public void Append_WhenDirectoryCanNeverBeCreated_PropagatesOnEveryAppend()
    {
        string blockingFile = Path.Combine(Path.GetTempPath(), "cc-tel-blocker-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blockingFile, "occupies the path a directory needs");
        try
        {
            string invalidDirectory = Path.Combine(blockingFile, "telemetry");
            var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero) };
            var sink = new RotatingJsonlTelemetrySink(invalidDirectory, clock);

            Assert.Throws<IOException>(() => sink.Append(Rec("first")));
            Assert.Throws<IOException>(() => sink.Append(Rec("second")));
        }
        finally
        {
            File.Delete(blockingFile);
        }
    }

    /// <summary>Concurrency guard: concurrent appends from multiple threads must all land as
    /// distinct lines with no corruption (no interleaved/partial JSON lines), proving the lock
    /// correctly serializes cached-state access.</summary>
    [Fact]
    public void Append_ConcurrentAppends_AllLinesWritten_WithoutCorruption()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero) };
        var sink = new RotatingJsonlTelemetrySink(dir, clock);
        const int threads = 8;
        const int perThread = 10;

        Parallel.For(0, threads, t =>
        {
            for (int i = 0; i < perThread; i++)
            {
                sink.Append(Rec($"t{t}-{i}"));
            }
        });

        string file = Path.Combine(dir, "sessions.2026-07-01.0000.jsonl");
        string[] lines = File.ReadAllLines(file);
        Assert.Equal(threads * perThread, lines.Length);
        foreach (string line in lines)
        {
            using JsonDocument document = JsonDocument.Parse(line); // throws if any line is corrupt/interleaved
            Assert.NotNull(document.RootElement.GetProperty("repoName").GetString());
        }
    }
}
