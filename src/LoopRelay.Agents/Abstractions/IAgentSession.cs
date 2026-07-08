using LoopRelay.Agents.Models;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Agents.Abstractions;

public interface IAgentSession : IAsyncDisposable
{
    SessionIdentity SessionId { get; }

    string RepositoryId { get; }

    SessionRole Role { get; }

    AgentSessionMode Mode { get; }

    AgentProcessState State { get; }

    int CompletedTurns { get; }

    AgentTokenUsage TotalUsage { get; }

    /// <summary>The codex app-server thread id — null until the handshake completes, and always null for
    /// one-shot/legacy sessions. The durable key a later process can resume the conversation with.</summary>
    string? ThreadId { get; }

    Task<AgentTurnResult> RunTurnAsync(
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default);

    Task CancelAsync(CancellationToken cancellationToken = default);
}
