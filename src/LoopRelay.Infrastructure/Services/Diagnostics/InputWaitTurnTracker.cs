using System.Diagnostics;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Infrastructure.Abstractions.Diagnostics;
using LoopRelay.Infrastructure.Models.Diagnostics;

namespace LoopRelay.Infrastructure.Services.Diagnostics;

internal sealed class InputWaitTurnTracker : IAgentTurnProgressObserver, IAsyncDisposable
{
    private readonly IInputWaitProgressRenderer _renderer;
    private readonly string _repositoryId;
    private readonly SessionIdentity _sessionId;
    private readonly SessionRole _sessionRole;
    private readonly string _transport;
    private readonly string? _model;
    private readonly int _promptChars;
    private readonly int _promptBytes;
    private readonly int _promptTokensEstimated;
    private readonly string _tokenEstimateSource;
    private readonly string _estimatorVersion;
    private readonly object gate = new();
    private readonly CancellationTokenSource renderCts = new();
    private readonly long startedTimestamp = Stopwatch.GetTimestamp();
    private readonly Func<DateTimeOffset> _utcNow;
    private Task? renderTask;
    private bool firstOutputRendered;
    private bool completed;

    private DateTimeOffset? promptPreparedAt;
    private DateTimeOffset? requestWriteStartedAt;
    private DateTimeOffset? requestSubmittedAt;
    private DateTimeOffset? requestAcceptedAt;
    private DateTimeOffset? firstProtocolEventAt;
    private DateTimeOffset? firstOutputAt;
    private DateTimeOffset? completedAt;

    public InputWaitTurnTracker(
        IInputWaitProgressRenderer renderer,
        string repositoryId,
        SessionIdentity sessionId,
        SessionRole sessionRole,
        string transport,
        string? model,
        int promptChars,
        int promptBytes,
        int promptTokensEstimated,
        string tokenEstimateSource,
        string estimatorVersion,
        Func<DateTimeOffset>? utcNow = null)
    {
        _renderer = renderer;
        _repositoryId = repositoryId;
        _sessionId = sessionId;
        _sessionRole = sessionRole;
        _transport = transport;
        _model = model;
        _promptChars = promptChars;
        _promptBytes = promptBytes;
        _promptTokensEstimated = promptTokensEstimated;
        _tokenEstimateSource = tokenEstimateSource;
        _estimatorVersion = estimatorVersion;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public void Start()
    {
        lock (gate)
        {
            promptPreparedAt ??= _utcNow();
        }

        SafeRender(() => _renderer.Started(Snapshot()));
        renderTask = Task.Run(RenderLoopAsync);
    }

    public void RequestWriteStarted() => SetOnce(ref requestWriteStartedAt);

    public void RequestSubmitted() => SetOnce(ref requestSubmittedAt);

    public void RequestAccepted() => SetOnce(ref requestAcceptedAt);

    public void FirstProtocolEvent() => SetOnce(ref firstProtocolEventAt);

    public void ObserveChunk(AgentStreamChunk chunk)
    {
        if (chunk.Stream != AgentProcessOutputStream.StandardOutput || string.IsNullOrEmpty(chunk.Content))
        {
            return;
        }

        FirstProtocolEvent();
        FirstOutput();
    }

    public void FirstOutput()
    {
        bool shouldRender;
        lock (gate)
        {
            firstOutputAt ??= _utcNow();
            shouldRender = !firstOutputRendered;
            firstOutputRendered = true;
        }

        if (!shouldRender)
        {
            return;
        }

        SafeRender(() => _renderer.FirstOutput(Snapshot()));
        renderCts.Cancel();
    }

    public async ValueTask<InputWaitObservation?> CompleteAsync(
        AgentTurnResult? result,
        string status,
        CancellationToken cancellationToken)
    {
        bool renderCompletedWithoutOutput;
        lock (gate)
        {
            completedAt ??= _utcNow();
            completed = true;
            renderCompletedWithoutOutput = !firstOutputRendered;
        }

        renderCts.Cancel();
        if (renderTask is not null)
        {
            try
            {
                await renderTask.WaitAsync(CancellationToken.None);
            }
            catch
            {
                // Rendering is informational.
            }
        }

        if (renderCompletedWithoutOutput)
        {
            SafeRender(() => _renderer.CompletedWithoutOutput(Snapshot()));
        }

        cancellationToken.ThrowIfCancellationRequested();

        return result is null
            ? null
            : BuildObservation(result.TurnIndex, status);
    }

    public async ValueTask DisposeAsync()
    {
        renderCts.Cancel();
        if (renderTask is not null)
        {
            try
            {
                await renderTask.WaitAsync(CancellationToken.None);
            }
            catch
            {
                // Rendering is informational.
            }
        }

        renderCts.Dispose();
    }

    private async Task RenderLoopAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(_renderer.RefreshInterval);
            while (await timer.WaitForNextTickAsync(renderCts.Token))
            {
                if (!RenderWaitingIfStillWaiting())
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when first output or completion stops the wait display.
        }
        catch
        {
            // Rendering is informational.
        }
    }

    private bool RenderWaitingIfStillWaiting()
    {
        lock (gate)
        {
            if (firstOutputRendered || completed)
            {
                return false;
            }

            // Keep timer renders ordered before first-output renders; otherwise a tick can repaint
            // "processing input" after visible output has already started.
            SafeRender(() => _renderer.Waiting(SnapshotWithoutLock()));
            return true;
        }
    }

    private InputWaitObservation BuildObservation(int turnIndex, string status)
    {
        lock (gate)
        {
            return new InputWaitObservation(
                _repositoryId,
                _sessionId,
                _sessionRole,
                turnIndex,
                _transport,
                _model,
                _promptChars,
                _promptBytes,
                _promptTokensEstimated,
                _tokenEstimateSource,
                promptPreparedAt,
                requestWriteStartedAt,
                requestSubmittedAt,
                requestAcceptedAt,
                firstProtocolEventAt,
                firstOutputAt,
                completedAt,
                status,
                _estimatorVersion);
        }
    }

    private InputWaitProgressSnapshot Snapshot() =>
        new(_promptTokensEstimated, Stopwatch.GetElapsedTime(startedTimestamp), HasFirstOutput());

    private InputWaitProgressSnapshot SnapshotWithoutLock() =>
        new(_promptTokensEstimated, Stopwatch.GetElapsedTime(startedTimestamp), firstOutputAt is not null);

    private bool HasFirstOutput()
    {
        lock (gate)
        {
            return firstOutputAt is not null;
        }
    }

    private void SetOnce(ref DateTimeOffset? field)
    {
        lock (gate)
        {
            field ??= _utcNow();
        }
    }

    private static void SafeRender(Action render)
    {
        try
        {
            render();
        }
        catch
        {
            // Progress rendering must never break the turn.
        }
    }
}
