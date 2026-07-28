using System.Globalization;
using System.Text.Json;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;

namespace LoopRelay.Cli.Services.Telemetry;

/// <summary>
/// Compatibility JSONL export sink. A new file begins each UTC calendar day; within a day the active file rolls
/// to the next 4-digit sequence once it crosses <c>maxBytes</c>. Files are NEVER deleted (a separate visualizer
/// manages pruning). One compact JSON object per line.
///
/// <para>
/// <b>Cached-state design (PERF task w2-t4):</b> the sink remembers the active file it last
/// resolved for the current UTC day (<see cref="_cachedDate"/>/<see cref="_cachedFile"/>) instead
/// of re-running the O(files) candidate scan on every append. Each append still does a single,
/// O(1) <see cref="FileInfo"/> check of that one remembered file - not the whole directory - both
/// to size-check for rollover and to notice external deletion; only when that check says the
/// cache no longer holds (day changed, file missing, or file full) does a full re-scan run,
/// exactly reproducing the original per-append algorithm so rotation thresholds are unchanged.
/// </para>
///
/// <para>
/// <b>Failure handling:</b> an append fails open exactly once - a stale cache is discarded and the
/// write retried against a full rescan - and then propagates. Telemetry stays fail-open overall
/// because <c>SessionTelemetryRecorder</c> catches and warns; swallowing here instead would make a
/// persistently broken sink indistinguishable from a working one.
/// </para>
/// </summary>
internal sealed class RotatingJsonlTelemetrySink : ISessionTelemetrySink
{
    private const long DefaultMaxBytes = 5_242_880; // 5 MiB

    private readonly string _directory;
    private readonly IClock _clock;
    private readonly long _maxBytes;
    private readonly object gate = new();

    private string? _cachedDate;
    private string? _cachedFile;

    /// <summary>Test-only observability: how many times this instance has run the O(files)
    /// candidate scan. The load-bearing assertion for this task is that two same-day appends
    /// (with no rollover) leave this at 1, not 2.</summary>
    internal int RescanCount { get; private set; }

    public RotatingJsonlTelemetrySink(string directory, IClock clock, long maxBytes = DefaultMaxBytes)
    {
        _directory = directory;
        _clock = clock;
        _maxBytes = maxBytes;
    }

    public void Append(SessionTelemetryRecord record)
    {
        string line = JsonSerializer.Serialize(record, SessionTelemetryJson.Options);
        lock (gate)
        {
            if (!TryAppendLocked(line, forceRescan: false))
            {
                // Fail open once: the cached file/size didn't work out (IO error, e.g. the
                // directory itself vanished). Forget the cache and retry exactly once with a
                // full rescan. A failure of the retry propagates so the recorder can warn -
                // a persistently broken sink must not stay invisible.
                AppendLocked(line, forceRescan: true);
            }
        }
    }

    private bool TryAppendLocked(string line, bool forceRescan)
    {
        try
        {
            AppendLocked(line, forceRescan);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void AppendLocked(string line, bool forceRescan)
    {
        try
        {
            string date = _clock.UtcNow.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string activeFile = ResolveActiveFileLocked(date, forceRescan);
            File.AppendAllText(activeFile, line + "\n");
        }
        catch (IOException)
        {
            InvalidateCacheLocked();
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            InvalidateCacheLocked();
            throw;
        }
    }

    private void InvalidateCacheLocked()
    {
        _cachedDate = null;
        _cachedFile = null;
    }

    private string ResolveActiveFileLocked(string date, bool forceRescan)
    {
        bool cacheMightBeValid = !forceRescan && _cachedFile is not null && _cachedDate == date;
        if (cacheMightBeValid)
        {
            var info = new FileInfo(_cachedFile!);
            if (info.Exists && info.Length < _maxBytes)
            {
                return _cachedFile!;
            }

            // Either the file was deleted out from under the sink (external deletion) or it just
            // crossed the size cap (rollover). Both are exactly the two conditions this design
            // re-scans for - fall through.
        }

        RescanCount++;
        Directory.CreateDirectory(_directory);
        string resolved = ScanForActiveFile(date);
        _cachedDate = date;
        _cachedFile = resolved;
        return resolved;
    }

    private string ScanForActiveFile(string date)
    {
        for (int sequence = 0; ; sequence++)
        {
            string candidate = Path.Combine(_directory, $"sessions.{date}.{sequence:D4}.jsonl");
            var info = new FileInfo(candidate);
            if (!info.Exists || info.Length < _maxBytes)
            {
                return candidate;
            }
        }
    }
}
