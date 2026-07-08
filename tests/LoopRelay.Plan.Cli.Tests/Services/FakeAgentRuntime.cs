using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Plan.Cli.Tests.Models;

namespace LoopRelay.Plan.Cli.Tests.Services;

internal sealed class FakeAgentRuntime(IArtifactStore store) : IAgentRuntime
{
    public Queue<ScriptedTurn> OneShotTurns { get; } = new();
    public Queue<ScriptedTurn> SessionTurns { get; } = new();
    public int OpenSessions { get; private set; }
    public int ClosedSessions { get; private set; }
    public List<(AgentSessionSpec Spec, string Prompt)> OneShotCalls { get; } = new();
    public List<AgentSessionSpec> OpenedSpecs { get; } = new();

    public Task<AgentTurnResult> RunOneShotAsync(
        AgentSessionSpec spec, string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken ct = default)
    {
        OneShotCalls.Add((spec, prompt));
        ScriptedTurn turn = OneShotTurns.Dequeue();
        return Task.FromResult(turn.Handler(spec, prompt, store));
    }

    public Task<IAgentSession> OpenSessionAsync(AgentSessionSpec spec, CancellationToken ct = default)
    {
        OpenedSpecs.Add(spec);
        OpenSessions++;
        return Task.FromResult<IAgentSession>(new FakeAgentSession(this, spec));
    }

    public ValueTask CloseSessionAsync(IAgentSession session)
    {
        ClosedSessions++;
        return ValueTask.CompletedTask;
    }

    internal AgentTurnResult RunSessionTurn(AgentSessionSpec spec, string prompt) =>
        SessionTurns.Dequeue().Handler(spec, prompt, store);
}
