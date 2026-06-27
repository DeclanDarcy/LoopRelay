using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Abstractions;

public interface IAgentRuntime
{
    Task<IAgentSession> OpenSessionAsync(
        AgentSessionSpec spec,
        CancellationToken cancellationToken = default);

    Task<AgentTurnResult> RunOneShotAsync(
        AgentSessionSpec spec,
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default);
}
