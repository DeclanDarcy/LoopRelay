using LoopRelay.Agents.Abstractions;

namespace LoopRelay.Cli.Tests.Services;

internal sealed class FakeExecutableResolver : IAgentExecutableResolver
{
    public string Executable { get; init; } = "codex.exe";

    public string Resolve() => Executable;
}
