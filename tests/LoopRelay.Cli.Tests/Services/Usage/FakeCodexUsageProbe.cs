using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;

namespace LoopRelay.Cli.Tests.Services;


/// <summary>Returns scripted <see cref="CodexUsageStatus"/> snapshots for the telemetry post-probe (no real codex).</summary>
internal sealed class FakeCodexUsageProbe : ICodexUsageProbe
{
    public Queue<CodexUsageStatus?> Results { get; } = new();

    /// <summary>Returned when <see cref="Results"/> is empty (the steady-state answer).</summary>
    public CodexUsageStatus? Default { get; set; }

    public int Calls { get; private set; }

    public Task<CodexUsageStatus?> QueryAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : Default);
    }
}
