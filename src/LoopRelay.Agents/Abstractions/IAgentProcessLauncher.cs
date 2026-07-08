using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;

namespace LoopRelay.Agents.Abstractions;

public interface IAgentProcessLauncher
{
    Task<IAgentProcess> LaunchAsync(
        AgentSessionSpec spec,
        AgentSessionMode mode,
        CancellationToken cancellationToken = default);
}
