using System.Text.Json;

namespace LoopRelay.Cli;

/// <summary>
/// Finds codex's own rollout log under <c>~/.codex/sessions/YYYY/MM/DD/rollout-*.jsonl</c>. Codex never tells
/// LoopRelay its session id, so we match on the rollout's first-line <c>session_meta</c> (cwd == the session's
/// working directory) and pick the newest whose start timestamp is at/after the session opened. Fails to null —
/// the log row is still valuable without the path.
/// </summary>
internal sealed class FileSystemCodexRolloutLocator : ICodexRolloutLocator
{
    private static readonly TimeSpan StartTolerance = TimeSpan.FromSeconds(1);

    private readonly string sessionsRoot;

    public FileSystemCodexRolloutLocator(string sessionsRoot) => this.sessionsRoot = sessionsRoot;

    /// <summary><c>%CODEX_HOME%/sessions</c>, else <c>%USERPROFILE%/.codex/sessions</c>.</summary>
    public static string ResolveDefaultSessionsRoot()
    {
        string? home = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        }

        return Path.Combine(home, "sessions");
    }

    public string? Resolve(string workingDirectory, DateTimeOffset openedAtUtc)
    {
        try
        {
            if (!Directory.Exists(sessionsRoot))
            {
                return null;
            }

            string target = Normalize(workingDirectory);
            string? best = null;
            DateTimeOffset bestStart = DateTimeOffset.MinValue;

            foreach (string file in Directory.EnumerateFiles(sessionsRoot, "rollout-*.jsonl", SearchOption.AllDirectories))
            {
                if (!TryReadMeta(file, out string cwd, out DateTimeOffset started))
                {
                    continue;
                }

                if (!string.Equals(Normalize(cwd), target, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (started < openedAtUtc - StartTolerance)
                {
                    continue;
                }

                if (best is null || started > bestStart)
                {
                    best = file;
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
