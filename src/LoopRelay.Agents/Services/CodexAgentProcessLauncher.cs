using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;

namespace LoopRelay.Agents.Services;

public sealed class CodexAgentProcessLauncher(
    IProcessRunner processRunner,
    IAgentExecutableResolver executableResolver) : IAgentProcessLauncher
{
    public Task<IAgentProcess> LaunchAsync(
        AgentSessionSpec spec,
        AgentSessionMode mode,
        CancellationToken cancellationToken = default)
    {
        string executable = executableResolver.Resolve();
        IReadOnlyList<string> arguments = CodexAgentArgumentBuilder.Build(spec, mode);
        string workingDirectory = spec.WorkingDirectory ?? Directory.GetCurrentDirectory();

        return processRunner.StartInteractiveAsync(executable, arguments, workingDirectory, cancellationToken);
    }
}
