using System.Globalization;
using System.Text.Json;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;

namespace LoopRelay.Cli.Services;

/// <summary>
/// Per-day / size-hybrid rotating JSONL sink. A new file begins each UTC calendar day; within a day the
/// active file rolls to the next 4-digit sequence once it crosses <c>maxBytes</c>. Files are NEVER deleted
/// (a separate visualizer manages pruning). One compact JSON object per line.
/// </summary>
internal sealed class RotatingJsonlTelemetrySink : ISessionTelemetrySink
{
    private const long DefaultMaxBytes = 5_242_880; // 5 MiB

    private readonly string directory;
    private readonly IClock clock;
    private readonly long maxBytes;
    private readonly object gate = new();

    public RotatingJsonlTelemetrySink(string directory, IClock clock, long maxBytes = DefaultMaxBytes)
    {
        this.directory = directory;
        this.clock = clock;
        this.maxBytes = maxBytes;
    }

    public void Append(SessionTelemetryRecord record)
    {
        string line = JsonSerializer.Serialize(record, SessionTelemetryJson.Options);
        lock (gate)
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(ResolveActiveFile(), line + "\n");
        }
    }

    private string ResolveActiveFile()
    {
        string date = clock.UtcNow.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        for (int sequence = 0; ; sequence++)
        {
            string candidate = Path.Combine(directory, $"sessions.{date}.{sequence:D4}.jsonl");
            var info = new FileInfo(candidate);
            if (!info.Exists || info.Length < maxBytes)
            {
                return candidate;
            }
        }
    }
}

