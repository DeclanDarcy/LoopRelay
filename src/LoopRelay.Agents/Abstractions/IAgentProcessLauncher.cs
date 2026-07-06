using LoopRelay.Agents.Models;

namespace LoopRelay.Agents.Abstractions;

public interface IAgentProcessLauncher
{
    Task<IAgentProcess> LaunchAsync(
        AgentSessionSpec spec,
        AgentSessionMode mode,
        CancellationToken cancellationToken = default);
}
