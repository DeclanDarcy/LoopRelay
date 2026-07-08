using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;

namespace LoopRelay.Cli.Tests.Services.Agents;


/// <summary>Serves a single scripted interactive process (the codex /usage session) via StartInteractiveAsync.</summary>
internal sealed class FakeInteractiveProcessRunner(FakeAgentProcess process) : IProcessRunner
{
    public List<(string FileName, IReadOnlyList<string> Args, string WorkingDirectory)> InteractiveCalls { get; } = new();

    public Task<IAgentProcess> StartInteractiveAsync(
        string fileName, IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken = default)
    {
        InteractiveCalls.Add((fileName, arguments, workingDirectory));
        return Task.FromResult<IAgentProcess>(process);
    }

    public Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory) =>
        throw new NotSupportedException();
}
