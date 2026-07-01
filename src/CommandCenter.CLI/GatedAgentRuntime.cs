using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;

namespace CommandCenter.Cli;

/// <summary>
/// Wraps an <see cref="IAgentRuntime"/> so the Codex usage gate runs before EVERY codex turn/one-shot,
/// not just once per loop iteration. A single iteration invokes codex many times (execution work + handoff
/// turns, decision seed + proposal, transfer delta/evolve/reseed) and the warm decision session is reused
/// across iterations, so quota can drain to zero mid-iteration; gating at each turn is the finest boundary
/// we control (one of OUR turns is a whole codex run of many internal model calls). Opening a session spawns
/// the app-server but spends no quota, so it is NOT gated — only turns and one-shots are.
/// </summary>
internal sealed class GatedAgentRuntime(IAgentRuntime inner, IUsageGate usageGate) : IAgentRuntime
{
    public async Task<IAgentSession> OpenSessionAsync(
        AgentSessionSpec spec, CancellationToken cancellationToken = default)
    {
        IAgentSession session = await inner.OpenSessionAsync(spec, cancellationToken);
        return new GatedAgentSession(session, usageGate);
    }

    public async Task<AgentTurnResult> RunOneShotAsync(
        AgentSessionSpec spec,
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        await usageGate.WaitForCapacityAsync(cancellationToken);
        return await inner.RunOneShotAsync(spec, prompt, onChunk, cancellationToken);
    }

    public ValueTask CloseSessionAsync(IAgentSession session) =>
        // The inner runtime keys teardown off the session it created, so hand it the raw inner session.
        inner.CloseSessionAsync(session is GatedAgentSession gated ? gated.Inner : session);
}

/// <summary>
/// A session wrapper that runs the usage gate before each turn, then delegates to the wrapped session.
/// Everything else passes straight through to <see cref="Inner"/>.
/// </summary>
internal sealed class GatedAgentSession(IAgentSession inner, IUsageGate usageGate) : IAgentSession
{
    internal IAgentSession Inner => inner;

    public SessionIdentity SessionId => inner.SessionId;
    public string RepositoryId => inner.RepositoryId;
    public SessionRole Role => inner.Role;
    public AgentSessionMode Mode => inner.Mode;
    public AgentProcessState State => inner.State;
    public int CompletedTurns => inner.CompletedTurns;
    public AgentTokenUsage TotalUsage => inner.TotalUsage;

    public async Task<AgentTurnResult> RunTurnAsync(
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        await usageGate.WaitForCapacityAsync(cancellationToken);
        return await inner.RunTurnAsync(prompt, onChunk, cancellationToken);
    }

    public Task CancelAsync(CancellationToken cancellationToken = default) => inner.CancelAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
