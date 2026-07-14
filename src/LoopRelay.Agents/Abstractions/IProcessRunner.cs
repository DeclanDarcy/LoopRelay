using LoopRelay.Agents.Models;
using LoopRelay.Agents.Models.Process;

namespace LoopRelay.Agents.Abstractions;

public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory);

    Task<IAgentProcess> StartInteractiveAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default);
}
