using System.Diagnostics;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;

namespace LoopRelay.Infrastructure.Diagnostics;

internal sealed class InputWaitTurnTracker : IAgentTurnProgressObserver, IAsyncDisposable
{
    private readonly IInputWaitProgressRenderer renderer;
    private readonly string repositoryId;
    private readonly SessionIdentity sessionId;
    private readonly SessionRole sessionRole;
    private readonly string transport;
    private readonly string? model;
    private readonly int promptChars;
    private readonly int promptBytes;
    private readonly int promptTokensEstimated;
    private readonly string tokenEstimateSource;
    private readonly string estimatorVersion;
    private readonly object gate = new();
    private readonly CancellationTokenSource renderCts = new();
    private readonly long startedTimestamp = Stopwatch.GetTimestamp();
    private readonly Func<DateTimeOffset> utcNow;
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
        this.renderer = renderer;
        this.repositoryId = repositoryId;
        this.sessionId = sessionId;
        this.sessionRole = sessionRole;
        this.transport = transport;
        this.model = model;
        this.promptChars = promptChars;
        this.promptBytes = promptBytes;
        this.promptTokensEstimated = promptTokensEstimated;
        this.tokenEstimateSource = tokenEstimateSource;
        this.estimatorVersion = estimatorVersion;
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public void Start()
    {
        lock (gate)
        {
            promptPreparedAt ??= utcNow();
        }

        SafeRender(() => renderer.Started(Snapshot()));
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
            firstOutputAt ??= utcNow();
            shouldRender = !firstOutputRendered;
            firstOutputRendered = true;
        }

        if (!shouldRender)
        {
            return;
        }

        SafeRender(() => renderer.FirstOutput(Snapshot()));
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
            completedAt ??= utcNow();
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
            SafeRender(() => renderer.CompletedWithoutOutput(Snapshot()));
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
            using var timer = new PeriodicTimer(renderer.RefreshInterval);
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
            SafeRender(() => renderer.Waiting(SnapshotWithoutLock()));
            return true;
        }
    }

    private InputWaitObservation BuildObservation(int turnIndex, string status)
    {
        lock (gate)
        {
            return new InputWaitObservation(
                repositoryId,
                sessionId,
                sessionRole,
                turnIndex,
                transport,
                model,
                promptChars,
                promptBytes,
                promptTokensEstimated,
                tokenEstimateSource,
                promptPreparedAt,
                requestWriteStartedAt,
                requestSubmittedAt,
                requestAcceptedAt,
                firstProtocolEventAt,
                firstOutputAt,
                completedAt,
                status,
                estimatorVersion);
        }
    }

    private InputWaitProgressSnapshot Snapshot() =>
        new(promptTokensEstimated, Stopwatch.GetElapsedTime(startedTimestamp), HasFirstOutput());

    private InputWaitProgressSnapshot SnapshotWithoutLock() =>
        new(promptTokensEstimated, Stopwatch.GetElapsedTime(startedTimestamp), firstOutputAt is not null);

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
            field ??= utcNow();
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
