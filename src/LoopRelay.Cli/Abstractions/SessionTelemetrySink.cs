using LoopRelay.Cli.Models;

namespace LoopRelay.Cli.Abstractions;

/// <summary>Appends telemetry rows. Implementations must never throw out of <see cref="Append"/> in a way
/// that could reach a turn — the recorder wraps calls, but keep sink work simple and self-contained.</summary>
internal interface ISessionTelemetrySink
{
    void Append(SessionTelemetryRecord record);
}
