using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;

namespace LoopRelay.Cli;

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
    IAgentRuntime inner,
    IUsageLimitDetector usageLimit,
    ISessionTelemetryRecorder recorder,
    IClock clock,
    string repoName,
    InputWaitObservationStore? inputWaitObservations = null) : IAgentRuntime
{
    public async Task<IAgentSession> OpenSessionAsync(
        AgentSessionSpec spec, CancellationToken cancellationToken = default)
    {
        IAgentSession session = await inner.OpenSessionAsync(spec, cancellationToken);
        return new GatedAgentSession(
            session, usageLimit, recorder, repoName, spec.WorkingDirectory ?? string.Empty, clock.UtcNow,
            inputWaitObservations);
    }

    public async Task<AgentTurnResult> RunOneShotAsync(
        AgentSessionSpec spec,
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        // One-shots are deliberately NOT retried on a usage-limit failure: legacy/projection-adjacent callers may
        // still mutate their only candidate output, so only the caller can decide whether a rerun is safe.
        DateTimeOffset openedAt = clock.UtcNow;
        AgentTurnResult result = await inner.RunOneShotAsync(spec, prompt, onChunk, cancellationToken);
        await recorder.RecordTurnAsync(
            repoName, spec.WorkingDirectory ?? string.Empty, spec.SessionId, spec.Role, openedAt,
            cachedLogPath: null, result,
            inputWaitObservations?.Take(spec.SessionId, result.TurnIndex),
            cancellationToken);
        return result;
    }

    public ValueTask CloseSessionAsync(IAgentSession session) =>
        inner.CloseSessionAsync(session is GatedAgentSession gated ? gated.Inner : session);
}

/// <summary>
/// A session wrapper that delegates each turn to <see cref="Inner"/>, records one telemetry row per
/// attempt, and — when the turn failed because codex hit its usage limit — waits out the advertised reset
/// and reruns the same prompt on the same warm session. The codex rollout log path is resolved once
/// (lazily, after the first turn) and cached for the session's remaining turns.
/// </summary>
internal sealed class GatedAgentSession(
    IAgentSession inner,
    IUsageLimitDetector usageLimit,
    ISessionTelemetryRecorder recorder,
    string repoName,
    string workingDirectory,
    DateTimeOffset openedAtUtc,
    InputWaitObservationStore? inputWaitObservations = null) : IAgentSession
{
    /// <summary>Wait-and-retry attempts per logical turn before the failure propagates. Covers cascading
    /// windows (a 5h reset followed by a weekly limit) without masking a persistently broken codex. Lives
    /// here — not on the detector — because the seam that runs the retry loop owns the retry policy.</summary>
    internal const int MaxUsageLimitRetriesPerTurn = 3;

    private string? cachedLogPath;

    internal IAgentSession Inner => inner;

    public SessionIdentity SessionId => inner.SessionId;
    public string RepositoryId => inner.RepositoryId;
    public SessionRole Role => inner.Role;
    public AgentSessionMode Mode => inner.Mode;
    public AgentProcessState State => inner.State;
    public int CompletedTurns => inner.CompletedTurns;
    public AgentTokenUsage TotalUsage => inner.TotalUsage;
    public string? ThreadId => inner.ThreadId;

    public async Task<AgentTurnResult> RunTurnAsync(
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        AgentTurnResult result;
        for (int attempt = 0; ; attempt++)
        {
            result = await inner.RunTurnAsync(prompt, onChunk, cancellationToken);
            cachedLogPath = await recorder.RecordTurnAsync(
                repoName, workingDirectory, inner.SessionId, inner.Role, openedAtUtc,
                cachedLogPath, result,
                inputWaitObservations?.Take(inner.SessionId, result.TurnIndex),
                cancellationToken);

            UsageLimitHit? hit = usageLimit.Detect(result);
            if (hit is null)
            {
                break;
            }

            if (attempt >= MaxUsageLimitRetriesPerTurn)
            {
                usageLimit.WarnRetriesExhausted(MaxUsageLimitRetriesPerTurn);
                break;
            }

            // A usage-limit failure normally leaves the app-server alive, but when the codex process
            // died the session is unrecoverable: a retry would throw OperationCanceledException off the
            // dead session's linked token, which LoopRunner misreads as user cancellation. Retry only a
            // demonstrably live session, re-checking after the potentially multi-day wait.
            if (inner.State != AgentProcessState.Running)
            {
                break;
            }

            await usageLimit.WaitOutAsync(hit, cancellationToken);

            if (inner.State != AgentProcessState.Running)
            {
                break;
            }
        }

        return result;
    }

    public Task CancelAsync(CancellationToken cancellationToken = default) => inner.CancelAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
