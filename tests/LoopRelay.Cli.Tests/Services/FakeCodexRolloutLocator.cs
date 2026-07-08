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

internal sealed class FakeCodexRolloutLocator : Cli.ICodexRolloutLocator
{
    public string? Path { get; set; }
    public int Calls { get; private set; }

    public string? Resolve(string workingDirectory, DateTimeOffset openedAtUtc)
    {
        Calls++;
        return Path;
    }
}
