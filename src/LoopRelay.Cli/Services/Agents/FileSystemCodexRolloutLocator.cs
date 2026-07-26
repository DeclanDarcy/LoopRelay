using System.Globalization;
using System.Text.Json;
using LoopRelay.Cli.Abstractions;

namespace LoopRelay.Cli.Services.Agents;

/// <summary>
/// Finds codex's own rollout log under <c>~/.codex/sessions/YYYY/MM/DD/rollout-*.jsonl</c>. Codex never tells
/// LoopRelay its session id, so we match on the rollout's first-line <c>session_meta</c> (cwd == the session's
/// working directory) and pick the newest whose start timestamp is at/after the session opened. Fails to null —
/// the log row is still valuable without the path.
/// </summary>
/// <remarks>
/// The scan is bounded twice so it costs the same on a store holding years of rollouts as on an empty one:
/// day directories that end before the session are never descended into, and within the surviving directories
/// a file whose own timestamps predate the session is skipped without being opened. Only what survives both
/// bounds is opened and parsed.
/// </remarks>
internal sealed class FileSystemCodexRolloutLocator : ICodexRolloutLocator
{
    /// <summary>
    /// How far before <c>openedAtUtc</c> a rollout may have started and still be considered this session's.
    /// It must be generous: codex creates the rollout while its app-server is still starting and the session's
    /// open timestamp is only taken once that returns, so the rollout is *always* slightly older than the
    /// session it belongs to. Filesystem timestamp granularity and clock skew widen the gap further. Being too
    /// tight loses the rollout silently; being too loose costs at most a handful of extra candidates, and the
    /// newest-match rule still prefers this session's rollout over any predecessor in the same directory.
    /// </summary>
    private static readonly TimeSpan StartTolerance = TimeSpan.FromMinutes(5);

    private readonly string _sessionsRoot;

    public FileSystemCodexRolloutLocator(string sessionsRoot) => _sessionsRoot = sessionsRoot;

    public static string ResolveDefaultSessionsRoot(ProviderEnvironmentConfiguration configuration) =>
        Path.Combine(configuration.CodexHome, "sessions");

    public string? Resolve(string workingDirectory, DateTimeOffset openedAtUtc)
    {
        try
        {
            if (!Directory.Exists(_sessionsRoot))
            {
                return null;
            }

            string target = Normalize(workingDirectory);
            DateTimeOffset earliest = SubtractSaturating(openedAtUtc, StartTolerance);
            DateTime earliestFileTime = earliest.UtcDateTime;
            string? best = null;
            DateTimeOffset bestStart = DateTimeOffset.MinValue;

            foreach (FileInfo file in EnumerateCandidates(_sessionsRoot, DayFloor(earliest)))
            {
                // The cheap bound, and the reason this method no longer opens the whole store. Creation time
                // alone is not trusted: filesystems that don't record a birth time can report one that is
                // older than the file really is, and skipping a live rollout would fail silently. Requiring
                // the last write to be just as old too means only a file that is old by every available
                // measure is skipped unopened; anything else still gets its first line read below.
                if (file.CreationTimeUtc < earliestFileTime && file.LastWriteTimeUtc < earliestFileTime)
                {
                    continue;
                }

                if (!TryReadMeta(file.FullName, out string cwd, out DateTimeOffset started))
                {
                    continue;
                }

                if (!string.Equals(Normalize(cwd), target, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (started < earliest)
                {
                    continue;
                }

                if (best is null || started > bestStart)
                {
                    best = file.FullName;
                    bestStart = started;
                }
            }

            return best;
        }
        catch
        {
            return null; // never break a turn over a telemetry lookup
        }
    }

    /// <summary>
    /// The oldest day directory worth descending into. Codex names day directories from <b>local</b> time
    /// while the session's open timestamp is UTC, so around midnight the two disagree by a calendar day in
    /// either direction; the floor is dropped a full day to cover any offset either way. Directories dated
    /// after the session are always kept — a rollout may start at any point during it.
    /// </summary>
    private static DateOnly DayFloor(DateTimeOffset earliest)
    {
        DateTime day = earliest.UtcDateTime.Date;
        return day <= DateTime.MinValue.AddDays(1)
            ? DateOnly.MinValue
            : DateOnly.FromDateTime(day.AddDays(-1));
    }

    private static DateTimeOffset SubtractSaturating(DateTimeOffset value, TimeSpan amount) =>
        value - DateTimeOffset.MinValue < amount ? DateTimeOffset.MinValue : value - amount;

    /// <summary>
    /// Walks <c>&lt;root&gt;/YYYY/MM/DD</c>, pruning whole years, months and days that end before
    /// <paramref name="floor"/>. A path segment that is not a date is not something the floor can reason
    /// about, so its subtree is scanned in full rather than dropped — a store laid out some other way must
    /// never silently lose a rollout, and the per-file time bound keeps those files cheap anyway.
    /// </summary>
    private static IEnumerable<FileInfo> EnumerateCandidates(string root, DateOnly floor)
    {
        var rootDirectory = new DirectoryInfo(root);
        foreach (FileInfo file in Rollouts(rootDirectory, SearchOption.TopDirectoryOnly))
        {
            yield return file;
        }

        foreach (DirectoryInfo year in rootDirectory.EnumerateDirectories())
        {
            if (!TryReadDatePart(year.Name, out int y) || y > floor.Year)
            {
                foreach (FileInfo file in Rollouts(year, SearchOption.AllDirectories))
                {
                    yield return file;
                }

                continue;
            }

            if (y < floor.Year)
            {
                continue;
            }

            foreach (DirectoryInfo month in year.EnumerateDirectories())
            {
                if (!TryReadDatePart(month.Name, out int m) || m > floor.Month)
                {
                    foreach (FileInfo file in Rollouts(month, SearchOption.AllDirectories))
                    {
                        yield return file;
                    }

                    continue;
                }

                if (m < floor.Month)
                {
                    continue;
                }

                foreach (DirectoryInfo day in month.EnumerateDirectories())
                {
                    if (TryReadDatePart(day.Name, out int d) && d < floor.Day)
                    {
                        continue;
                    }

                    foreach (FileInfo file in Rollouts(day, SearchOption.AllDirectories))
                    {
                        yield return file;
                    }
                }
            }
        }
    }

    private static IEnumerable<FileInfo> Rollouts(DirectoryInfo directory, SearchOption option) =>
        directory.EnumerateFiles("rollout-*.jsonl", option);

    private static bool TryReadDatePart(string name, out int value) =>
        int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    private static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path;
        }
    }

    private static bool TryReadMeta(string file, out string cwd, out DateTimeOffset started)
    {
        cwd = string.Empty;
        started = default;
        try
        {
            using var reader = new StreamReader(file);
            string? first = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(first))
            {
                return false;
            }

            using JsonDocument doc = JsonDocument.Parse(first);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("payload", out JsonElement payload)
                || payload.ValueKind != JsonValueKind.Object
                || !payload.TryGetProperty("cwd", out JsonElement cwdEl)
                || cwdEl.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            cwd = cwdEl.GetString() ?? string.Empty;
            if (cwd.Length == 0)
            {
                return false;
            }

            started = ReadTimestamp(root, payload) ?? File.GetCreationTimeUtc(file);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static DateTimeOffset? ReadTimestamp(JsonElement root, JsonElement payload)
    {
        foreach (JsonElement holder in new[] { root, payload })
        {
            if (holder.TryGetProperty("timestamp", out JsonElement ts)
                && ts.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(ts.GetString(), out DateTimeOffset parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        return null;
    }
}
