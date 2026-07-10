using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Services.Telemetry;

namespace LoopRelay.Cli.Services.Agents;

/// <summary>
/// Wraps an <see cref="IAgentRuntime"/> so every codex turn/one-shot emits one telemetry row per attempt,
/// and persistent-session turns that failed on the codex usage limit are waited out and retried in place.
/// A single iteration invokes codex many times and the warm decision session is reused across iterations,
/// so the limit can be hit by any turn mid-iteration; this per-turn seam is the finest boundary we
/// control, and retrying here lets the loop survive quota exhaustion without the steps above ever
/// noticing. One-shots are recorded but never retried (see <see cref="RunOneShotAsync"/>). Opening a
/// session spawns the app-server but spends no quota and cannot hit the limit, so it is neither watched
/// nor recorded.
/// </summary>
internal sealed class GatedAgentRuntime(
    IAgentRuntime _inner,
    IUsageLimitDetector _usageLimit,
    ISessionTelemetryRecorder _recorder,
    IClock _clock,
    string repoName,
    InputWaitObservationStore? _inputWaitObservations = null) : IAgentRuntime, IAgentSessionContinuityRuntime
{
    public async Task<IAgentSession> OpenSessionAsync(
        AgentSessionSpec spec, CancellationToken cancellationToken = default)
    {
        IAgentSession session = await _inner.OpenSessionAsync(spec, cancellationToken);
        return new GatedAgentSession(
            session, _usageLimit, _recorder, repoName, spec.WorkingDirectory ?? string.Empty, _clock.UtcNow,
            _inputWaitObservations);
    }

    public async Task<AgentTurnResult> RunOneShotAsync(
        AgentSessionSpec spec,
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        // One-shots are deliberately NOT retried on a usage-limit failure: legacy/projection-adjacent callers may
        // still mutate their only candidate output, so only the caller can decide whether a rerun is safe.
        DateTimeOffset openedAt = _clock.UtcNow;
        AgentTurnResult result = await _inner.RunOneShotAsync(spec, prompt, onChunk, cancellationToken);
        await _recorder.RecordTurnAsync(
            repoName, spec.WorkingDirectory ?? string.Empty, spec.SessionId, spec.Role, openedAt,
            cachedLogPath: null, result,
            _inputWaitObservations?.Take(spec.SessionId, result.TurnIndex),
            cancellationToken);
        return result;
    }

    public ValueTask CloseSessionAsync(IAgentSession session) =>
        _inner.CloseSessionAsync(session is GatedAgentSession gated ? gated.Inner : session);

    public Task<SessionContinuityNegotiationResult> NegotiateAsync(
        SessionContinuityNegotiationRequest request,
        CancellationToken cancellationToken = default) =>
        Continuity.NegotiateAsync(request, cancellationToken);

    public async Task<SessionCreateResult> CreateSessionAsync(
        SessionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        SessionCreateResult result = await Continuity.CreateSessionAsync(request, cancellationToken);
        return result.Session is null
            ? result
            : result with { Session = Wrap(result.Session, request.SessionSpec) };
    }

    public async Task<SessionResumeResult> ResumeSessionAsync(
        SessionResumeRequest request,
        CancellationToken cancellationToken = default)
    {
        SessionResumeResult result = await Continuity.ResumeSessionAsync(request, cancellationToken);
        return result.Session is null
            ? result
            : result with { Session = Wrap(result.Session, request.SessionSpec) };
    }

    public Task<SessionContentResult> ReadSessionAsync(
        SessionContentRequest request,
        CancellationToken cancellationToken = default) =>
        Continuity.ReadSessionAsync(request, cancellationToken);

    public Task<SessionSeedResult> SeedSessionAsync(
        SessionSeedRequest request,
        CancellationToken cancellationToken = default) =>
        Continuity.SeedSessionAsync(request, cancellationToken);

    public async Task<SessionForkResult> ForkSessionAsync(
        SessionForkRequest request,
        CancellationToken cancellationToken = default)
    {
        SessionForkResult result = await Continuity.ForkSessionAsync(request, cancellationToken);
        return result.Session is null
            ? result
            : result with { Session = Wrap(result.Session, request.SessionSpec) };
    }

    public Task<SessionReconcileResult> ReconcileAsync(
        SessionReconcileRequest request,
        CancellationToken cancellationToken = default) =>
        Continuity.ReconcileAsync(request, cancellationToken);

    private IAgentSessionContinuityRuntime Continuity => _inner as IAgentSessionContinuityRuntime
        ?? throw new InvalidOperationException("The inner agent runtime does not support continuity operations.");

    private IAgentSession Wrap(IAgentSession session, AgentSessionSpec spec) =>
        new GatedAgentSession(
            session, _usageLimit, _recorder, repoName, spec.WorkingDirectory ?? string.Empty,
            _clock.UtcNow, _inputWaitObservations);
}
