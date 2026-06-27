using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Abstractions;

public interface IAgentSession : IAsyncDisposable
{
    SessionIdentity SessionId { get; }

    string RepositoryId { get; }

    SessionRole Role { get; }

    AgentSessionMode Mode { get; }

    AgentProcessState State { get; }

    int CompletedTurns { get; }

    AgentTokenUsage TotalUsage { get; }

    Task<AgentTurnResult> RunTurnAsync(
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default);

    Task CancelAsync(CancellationToken cancellationToken = default);
}
