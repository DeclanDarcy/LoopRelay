using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Cli.Tests.Models;
using LoopRelay.Core.Abstractions.Artifacts;

namespace LoopRelay.Cli.Tests.Services.Agents;

internal sealed class FakeAgentRuntime(IArtifactStore store) : IAgentRuntime
{
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
    public string RecommendationOutput { get; set; } =
        """{"Model":"gpt-5.6-terra","Effort":"high"}""";

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
        if (prompt.StartsWith("Select the model and reasoning effort", StringComparison.Ordinal))
        {
            return new AgentTurnResult(
                0,
                AgentTurnState.Completed,
                RecommendationOutput,
                new AgentTokenUsage(1, 1));
        }

        return SessionTurns.Dequeue().Handler(spec, prompt, store);
    }
}
