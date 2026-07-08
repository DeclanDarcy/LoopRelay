using LoopRelay.Cli.Abstractions;

namespace LoopRelay.Cli.Tests.Services.Usage;

internal sealed class FakeCodexRolloutLocator : ICodexRolloutLocator
{
    public string? Path { get; set; }
    public int Calls { get; private set; }

    public string? Resolve(string workingDirectory, DateTimeOffset openedAtUtc)
    {
        Calls++;
        return Path;
    }
}
