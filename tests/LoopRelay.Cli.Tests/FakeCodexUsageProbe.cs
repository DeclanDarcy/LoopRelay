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


/// <summary>Returns scripted <see cref="CodexUsageStatus"/> snapshots for the telemetry post-probe (no real codex).</summary>
internal sealed class FakeCodexUsageProbe : Cli.ICodexUsageProbe
{
    public Queue<Cli.CodexUsageStatus?> Results { get; } = new();

    /// <summary>Returned when <see cref="Results"/> is empty (the steady-state answer).</summary>
    public Cli.CodexUsageStatus? Default { get; set; }

    public int Calls { get; private set; }

    public Task<Cli.CodexUsageStatus?> QueryAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : Default);
    }
}
