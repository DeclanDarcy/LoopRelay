using System;
using System.Text.Json;

namespace CommandCenter.Cli;

/// <summary>
/// One row of the per-turn session telemetry log. Capacity fields are a remaining PERCENT (0–100) measured
/// by the post-turn probe, or null when the codex usage probe could not be read. Raw tokens =
/// <see cref="PromptTokens"/> + <see cref="OutputTokens"/>; <see cref="EffectiveTokens"/> is the router's
/// cache-adjusted cost. Many rows share one <see cref="CodexLogPath"/> (one rollout file per codex process
/// serves many turns).
/// </summary>
internal sealed record SessionTelemetryRecord(
    DateTimeOffset Timestamp,
    string RepoName,
    string? CodexLogPath,
    string SessionId,
    string SessionType,
    int TurnIndex,
    int PromptTokens,
    int OutputTokens,
    int CachedTokens,
    double EffectiveTokens,
    int? PostFiveHourPercent,
    int? PostWeeklyPercent);

/// <summary>Canonical serialization for the telemetry log: compact, camelCase, one object per line.</summary>
internal static class SessionTelemetryJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };
}
