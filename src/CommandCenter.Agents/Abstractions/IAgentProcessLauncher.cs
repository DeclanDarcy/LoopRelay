using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Abstractions;

public interface IAgentProcessLauncher
{
    Task<IAgentProcess> LaunchAsync(
        AgentSessionSpec spec,
        AgentSessionMode mode,
        CancellationToken cancellationToken = default);
}
