using System.Text.Json;
using LoopRelay.Cli.Models;

namespace LoopRelay.Cli.Services.Telemetry;

/// <summary>
/// Parses the codex app-server <c>account/rateLimits/read</c> response (JSON) into a
/// <see cref="CodexUsageStatus"/>. The snapshot reports capacity USED (<c>usedPercent</c>); the telemetry
/// post-turn probe (the sole remaining consumer since the watermark gate was replaced by
/// <see cref="UsageLimitDetector"/>) tracks capacity REMAINING, so remaining = 100 - used. <c>primary</c> is the 5h window and
/// <c>secondary</c> is the weekly window; <c>resetsAt</c> is a unix-seconds instant.
/// <para>
/// Returns null only when no usable rate-limit snapshot is present, so callers can fail open. A snapshot
/// with a single window is honoured: the ABSENT window defaults to FULL capacity, never to exhausted, so
/// missing data can never wedge the loop on a phantom reset.
/// </para>
/// </summary>
internal static class CodexRateLimitsParser
{
    /// <summary>Parses against the current wall clock. See <see cref="Parse(string, DateTimeOffset)"/>.</summary>
    public static CodexUsageStatus? Parse(string json) => Parse(json, DateTimeOffset.UtcNow);

    /// <summary>
    /// Parses <paramref name="json"/>, computing reset durations relative to <paramref name="now"/>.
    /// The explicit clock keeps the reset arithmetic deterministic under test.
    /// </summary>
    public static CodexUsageStatus? Parse(string json, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!TryLocateSnapshot(document.RootElement, out JsonElement snapshot))
            {
                return null;
            }

            bool hasPrimary = TryWindow(snapshot, "primary", out JsonElement primary);
            bool hasSecondary = TryWindow(snapshot, "secondary", out JsonElement secondary);
            if (!hasPrimary && !hasSecondary)
            {
                return null;
            }

            (int fiveRemaining, TimeSpan fiveReset) = Window(primary, hasPrimary, now);
            (int weeklyRemaining, TimeSpan weeklyReset) = Window(secondary, hasSecondary, now);

            return new CodexUsageStatus(fiveRemaining, fiveReset, weeklyRemaining, weeklyReset);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // The snapshot object appears as result.rateLimits (JSON-RPC envelope), a top-level rateLimits, or bare.
    private static bool TryLocateSnapshot(JsonElement root, out JsonElement snapshot)
    {
        snapshot = default;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (root.TryGetProperty("result", out JsonElement result)
            && result.ValueKind == JsonValueKind.Object
            && result.TryGetProperty("rateLimits", out JsonElement nested)
            && nested.ValueKind == JsonValueKind.Object)
        {
            snapshot = nested;
            return true;
        }

        if (root.TryGetProperty("rateLimits", out JsonElement top) && top.ValueKind == JsonValueKind.Object)
        {
            snapshot = top;
            return true;
        }

        if (root.TryGetProperty("primary", out _) || root.TryGetProperty("secondary", out _))
        {
            snapshot = root;
            return true;
        }

        return false;
    }

    private static bool TryWindow(JsonElement snapshot, string name, out JsonElement window)
    {
        window = default;
        if (snapshot.TryGetProperty(name, out JsonElement candidate) && candidate.ValueKind == JsonValueKind.Object)
        {
            window = candidate;
            return true;
        }

        return false;
    }

    // A present window -> (remaining%, timeUntilReset). An absent window -> full capacity, no wait.
    private static (int Remaining, TimeSpan Reset) Window(JsonElement window, bool present, DateTimeOffset now)
    {
        if (!present)
        {
            return (100, TimeSpan.Zero);
        }

        int used = IntProperty(window, "usedPercent") ?? 0;
        int remaining = Math.Clamp(100 - used, 0, 100);

        TimeSpan reset = TimeSpan.Zero;
        if (window.TryGetProperty("resetsAt", out JsonElement resetsAt)
            && resetsAt.ValueKind == JsonValueKind.Number
            && resetsAt.TryGetInt64(out long unixSeconds))
        {
            reset = DateTimeOffset.FromUnixTimeSeconds(unixSeconds) - now;
        }

        return (remaining, reset);
    }

    private static int? IntProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out int parsed)
            ? parsed
            : null;
}
