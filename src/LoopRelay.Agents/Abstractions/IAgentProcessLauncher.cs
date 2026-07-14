using LoopRelay.Agents.Models;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Agents.Abstractions;

public interface IAgentProcessLauncher
{
    Task<IAgentProcess> LaunchAsync(
        AgentSessionSpec spec,
        AgentSessionMode mode,
        CancellationToken cancellationToken = default);
}
