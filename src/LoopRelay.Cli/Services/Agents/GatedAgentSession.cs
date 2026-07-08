using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Telemetry;

namespace LoopRelay.Cli.Services.Agents;

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
    private readonly IAgentSession _inner = inner;
    private readonly IUsageLimitDetector _usageLimit = usageLimit;
    private readonly ISessionTelemetryRecorder _recorder = recorder;
    private readonly DateTimeOffset _openedAtUtc = openedAtUtc;
    private readonly InputWaitObservationStore? _inputWaitObservations = inputWaitObservations;
    /// <summary>Wait-and-retry attempts per logical turn before the failure propagates. Covers cascading
    /// windows (a 5h reset followed by a weekly limit) without masking a persistently broken codex. Lives
    /// here — not on the detector — because the seam that runs the retry loop owns the retry policy.</summary>
    internal const int MaxUsageLimitRetriesPerTurn = 3;

    private string? cachedLogPath;

    internal IAgentSession Inner => _inner;

    public SessionIdentity SessionId => _inner.SessionId;
    public string RepositoryId => _inner.RepositoryId;
    public SessionRole Role => _inner.Role;
    public AgentSessionMode Mode => _inner.Mode;
    public AgentProcessState State => _inner.State;
    public int CompletedTurns => _inner.CompletedTurns;
    public AgentTokenUsage TotalUsage => _inner.TotalUsage;
    public string? ThreadId => _inner.ThreadId;

    public async Task<AgentTurnResult> RunTurnAsync(
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        AgentTurnResult result;
        for (int attempt = 0; ; attempt++)
        {
            result = await _inner.RunTurnAsync(prompt, onChunk, cancellationToken);
            cachedLogPath = await _recorder.RecordTurnAsync(
                repoName, workingDirectory, _inner.SessionId, _inner.Role, _openedAtUtc,
                cachedLogPath, result,
                _inputWaitObservations?.Take(_inner.SessionId, result.TurnIndex),
                cancellationToken);

            UsageLimitHit? hit = _usageLimit.Detect(result);
            if (hit is null)
            {
                break;
            }

            if (attempt >= MaxUsageLimitRetriesPerTurn)
            {
                _usageLimit.WarnRetriesExhausted(MaxUsageLimitRetriesPerTurn);
                break;
            }

            // A usage-limit failure normally leaves the app-server alive, but when the codex process
            // died the session is unrecoverable: a retry would throw OperationCanceledException off the
            // dead session's linked token, which LoopRunner misreads as user cancellation. Retry only a
            // demonstrably live session, re-checking after the potentially multi-day wait.
            if (_inner.State != AgentProcessState.Running)
            {
                break;
            }

            await _usageLimit.WaitOutAsync(hit, cancellationToken);

            if (_inner.State != AgentProcessState.Running)
            {
                break;
            }
        }

        return result;
    }

    public Task CancelAsync(CancellationToken cancellationToken = default) => _inner.CancelAsync(cancellationToken);

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
