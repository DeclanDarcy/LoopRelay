using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Cli.Tests.Models;
using LoopRelay.Core.Abstractions.Artifacts;

namespace LoopRelay.Cli.Tests.Services.Agents;

internal sealed class FakeAgentRuntime(IArtifactStore store) : IAgentRuntime, IAgentSessionContinuityRuntime
{
    public Queue<ScriptedTurn> OneShotTurns { get; } = new();
    public Queue<ScriptedTurn> SessionTurns { get; } = new();
    public int OpenSessions { get; private set; }
    public int ClosedSessions { get; private set; }
    public List<(AgentSessionSpec Spec, string Prompt)> OneShotCalls { get; } = new();
    public List<(AgentSessionSpec Spec, string Prompt)> SessionCalls { get; } = new();
    public List<AgentSessionSpec> OpenedSpecs { get; } = new();

    /// <summary>When true, the continuity operation returns a structured deterministic protocol failure.</summary>
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
        OpenSessions++;
        return Task.FromResult<IAgentSession>(new FakeAgentSession(this, spec));
    }

    public Task<SessionContinuityNegotiationResult> NegotiateAsync(
        SessionContinuityNegotiationRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SessionContinuityNegotiationResult(Profile(), true, "test"));

    public Task<SessionResumeResult> ResumeSessionAsync(
        SessionResumeRequest request, CancellationToken cancellationToken = default)
    {
        OpenedSpecs.Add(request.SessionSpec);
        if (FailResume)
        {
            return Task.FromResult(new SessionResumeResult(
                SessionResumeOutcome.DeterministicProtocolFailure, null, request.Original, null,
                SessionOperationStage.OperationResponse,
                new SessionOperationFailure(
                    "DeterministicProtocolFailure", "thread/resume", 2, -32602, default,
                    "scripted structured protocol failure", false, false),
                new SessionTransportProgress(true, true, true, false, null, null),
                request.Profile.Digest, request.Attempt));
        }

        OpenSessions++;
        var session = new FakeAgentSession(this, request.SessionSpec);
        return Task.FromResult(new SessionResumeResult(
            SessionResumeOutcome.SuccessfulResume, session, request.Original, request.Original,
            SessionOperationStage.Completed, null,
            new SessionTransportProgress(true, true, true, false, null, null),
            request.Profile.Digest, request.Attempt));
    }

    public async Task<SessionCreateResult> CreateSessionAsync(
        SessionCreateRequest request, CancellationToken cancellationToken = default)
    {
        IAgentSession session = await OpenSessionAsync(request.SessionSpec, cancellationToken);
        return new SessionCreateResult(true, session, new ProviderSessionReference("codex", session.ThreadId!),
            SessionOperationStage.Completed, null,
            new SessionTransportProgress(true, true, true, false, null, null), request.Profile.Digest);
    }

    public Task<SessionContentResult> ReadSessionAsync(SessionContentRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SessionContentResult(false, null, null));

    public Task<SessionSeedResult> SeedSessionAsync(SessionSeedRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SessionSeedResult(false, null));

    public Task<SessionForkResult> ForkSessionAsync(SessionForkRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SessionForkResult(false, null, request.Parent, null, null));

    public Task<SessionReconcileResult> ReconcileAsync(SessionReconcileRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SessionReconcileResult(false, null, null));

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

    private static SessionContinuityProfile Profile() => new(
        "codex", "test", "test", "test", "v2", "test",
        new Dictionary<string, bool> { ["experimentalApi"] = true },
        new Dictionary<string, string>(),
        new Dictionary<SessionContinuityOperation, SessionOperationSupportDescriptor>
        {
            [SessionContinuityOperation.Resume] = new SessionOperationSupportDescriptor(
                SessionOperationSupport.Supported,
                "v2",
                new Dictionary<string, SessionParameterSupport>
                {
                    [SessionContinuityProfile.ExcludeTurnsParameter] = new(SessionOperationSupport.Supported, "test"),
                },
                "load", "same-id", "none", "read", "test"),
        },
        256_000, "test", "test", negotiatedAt: DateTimeOffset.UnixEpoch);
}
