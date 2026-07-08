using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;

namespace LoopRelay.Cli.Services;

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
