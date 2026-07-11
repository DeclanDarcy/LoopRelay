using LoopRelay.Agents.Models;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;

namespace LoopRelay.Agents.Abstractions;

public interface IAgentRuntime
{
    /// <summary>
    /// The runtime's provider identity and declared capabilities (M7). Decorators forward the
    /// inner runtime's declaration; the gateway negotiates session specs against it before
    /// launch and records the provider as session evidence.
    /// </summary>
    AgentRuntimeCapabilities Capabilities { get; }

    Task<IAgentSession> OpenSessionAsync(
        AgentSessionSpec spec,
        CancellationToken cancellationToken = default);

    Task<AgentTurnResult> RunOneShotAsync(
        AgentSessionSpec spec,
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Single-sited teardown for a held-open session opened by <see cref="OpenSessionAsync"/> (m10): BOTH
    /// deregisters it from the owning <c>AgentSessionRegistry</c> AND disposes it. Callers that hold a session
    /// must close it through here rather than calling <c>DisposeAsync</c> directly, otherwise the registry
    /// accumulates dead entries and re-disposes them on shutdown. Idempotent/best-effort: a session that is not
    /// (or is no longer) registered is still disposed exactly once.
    /// </summary>
    ValueTask CloseSessionAsync(IAgentSession session);
}
