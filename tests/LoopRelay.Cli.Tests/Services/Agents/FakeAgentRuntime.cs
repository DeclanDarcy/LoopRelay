using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Cli.Tests.Models;
using LoopRelay.Core.Abstractions.Artifacts;

namespace LoopRelay.Cli.Tests.Services.Agents;

internal sealed class FakeAgentRuntime(IArtifactStore store) : IAgentRuntime
{
    public AgentRuntimeCapabilities Capabilities { get; } = new("test", true, true, true);
    public Queue<ScriptedTurn> OneShotTurns { get; } = new();
    public Queue<ScriptedTurn> SessionTurns { get; } = new();
    public int OpenSessions { get; private set; }
    public int ClosedSessions { get; private set; }
    public List<(AgentSessionSpec Spec, string Prompt)> OneShotCalls { get; } = new();
    public List<(AgentSessionSpec Spec, string Prompt)> SessionCalls { get; } = new();
    public List<AgentSessionSpec> OpenedSpecs { get; } = new();

    /// <summary>When true, any open that ASKS to resume throws the typed resume failure (scripting the
    /// rollout-gone / unknown-thread case); non-resume opens still succeed.</summary>
    public bool FailResume { get; set; }

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
        if (spec.ResumeThreadId is not null && FailResume)
        {
            throw new AgentSessionResumeException("scripted resume failure");
        }

        OpenSessions++;
        return Task.FromResult<IAgentSession>(new FakeAgentSession(this, spec));
    }

    public ValueTask CloseSessionAsync(IAgentSession session)
    {
        ClosedSessions++;
        return ValueTask.CompletedTask;
    }

    internal AgentTurnResult RunSessionTurn(AgentSessionSpec spec, string prompt)
    {
        SessionCalls.Add((spec, prompt));
        return SessionTurns.Dequeue().Handler(spec, prompt, store);
    }
}
