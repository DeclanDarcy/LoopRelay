using System.Collections.Concurrent;
using LoopRelay.Core.Artifacts;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Models;
using LoopRelay.Completion;
using LoopRelay.Projections;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Cli;

namespace LoopRelay.Cli.Tests;

internal sealed class FakeSessionTelemetrySink : Cli.ISessionTelemetrySink
{
    public List<Cli.SessionTelemetryRecord> Records { get; } = new();
    public bool Throw { get; set; }

    public void Append(Cli.SessionTelemetryRecord record)
    {
        if (Throw) throw new IOException("disk full");
        Records.Add(record);
    }
}
