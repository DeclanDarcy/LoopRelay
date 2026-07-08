using System.Text.Json;

namespace LoopRelay.Cli;

/// <summary>Canonical serialization for the telemetry log: compact, camelCase, one object per line.</summary>
internal static class SessionTelemetryJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };
}
