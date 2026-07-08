using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;

namespace LoopRelay.Cli.Tests.Services;

internal sealed class FakeSessionTelemetrySink : ISessionTelemetrySink
{
    public List<SessionTelemetryRecord> Records { get; } = new();
    public bool Throw { get; set; }

    public void Append(SessionTelemetryRecord record)
    {
        if (Throw) throw new IOException("disk full");
        Records.Add(record);
    }
}
