using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Codex;

namespace LoopRelay.Agents.Services.Process;

public sealed class CodexAgentProcessLauncher(
    IProcessRunner _processRunner,
    IAgentExecutableResolver _executableResolver) : IAgentProcessLauncher
{
    public Task<IAgentProcess> LaunchAsync(
        AgentSessionSpec spec,
        AgentSessionMode mode,
        CancellationToken cancellationToken = default)
    {
        string executable = _executableResolver.Resolve();
        IReadOnlyList<string> arguments = CodexAgentArgumentBuilder.Build(spec, mode);
        string workingDirectory = spec.WorkingDirectory ?? Directory.GetCurrentDirectory();

        return _processRunner.StartInteractiveAsync(executable, arguments, workingDirectory, cancellationToken);
    }
}
