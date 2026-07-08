using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Codex;

namespace LoopRelay.Agents.Services.Process;

public sealed class CodexAgentProcessLauncher(
    IProcessRunner processRunner,
    IAgentExecutableResolver executableResolver) : IAgentProcessLauncher
{
    private readonly IProcessRunner _processRunner = processRunner;
    private readonly IAgentExecutableResolver _executableResolver = executableResolver;
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
