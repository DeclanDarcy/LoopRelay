using System.Text;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;
using LoopRelay.Agents.Services;
using LoopRelay.Infrastructure.Abstractions.Diagnostics;
using LoopRelay.Infrastructure.Models.Diagnostics;

namespace LoopRelay.Infrastructure.Services.Diagnostics;

public sealed class InputWaitProgressAgentRuntime(
    IAgentRuntime inner,
    IAgentTokenEstimator tokenEstimator,
    IInputWaitProgressRenderer renderer,
    IInputWaitObservationSink? observationSink = null) : IAgentRuntime
{
    private readonly IInputWaitObservationSink observationSinkField =
        observationSink ?? NullInputWaitObservationSink.Instance;

    public async Task<IAgentSession> OpenSessionAsync(
        AgentSessionSpec spec,
        CancellationToken cancellationToken = default)
    {
        IAgentSession session = await inner.OpenSessionAsync(spec, cancellationToken);
        return new InputWaitProgressAgentSession(this, session);
    }

    public Task<AgentTurnResult> RunOneShotAsync(
        AgentSessionSpec spec,
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default) =>
        RunWithProgressAsync(
            spec.RepositoryId,
            spec.SessionId,
            spec.Role,
            "one-shot",
            ModelFrom(spec),
            prompt,
            onChunk,
            (wrapped, token) => inner.RunOneShotAsync(spec, prompt, wrapped, token),
            cancellationToken);

    public ValueTask CloseSessionAsync(IAgentSession session) =>
        inner.CloseSessionAsync(session is InputWaitProgressAgentSession progress ? progress.Inner : session);

    private async Task<AgentTurnResult> RunWithProgressAsync(
        string repositoryId,
        SessionIdentity sessionId,
        SessionRole role,
        string transport,
        string? model,
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk,
        Func<Func<AgentStreamChunk, Task>, CancellationToken, Task<AgentTurnResult>> run,
        CancellationToken cancellationToken)
    {
        Estimate estimate = EstimatePrompt(prompt, tokenEstimator);
        await using var tracker = new InputWaitTurnTracker(
            renderer,
            repositoryId,
            sessionId,
            role,
            transport,
            model,
            prompt.Length,
            Encoding.UTF8.GetByteCount(prompt),
            estimate.Tokens,
            estimate.Source,
            estimate.Version);

        tracker.Start();
        Func<AgentStreamChunk, Task> wrappedOnChunk = async chunk =>
        {
            tracker.ObserveChunk(chunk);
            if (onChunk is not null)
            {
                await onChunk(chunk);
            }
        };

        using IDisposable scope = AgentTurnProgress.Use(tracker);
        try
        {
            AgentTurnResult result = await run(wrappedOnChunk, cancellationToken);
            InputWaitObservation? observation =
                await tracker.CompleteAsync(result, result.State.ToString(), cancellationToken);
            if (observation is not null)
            {
                await RecordObservationAsync(observation, cancellationToken);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await tracker.CompleteAsync(null, "Canceled", CancellationToken.None);
            throw;
        }
        catch
        {
            await tracker.CompleteAsync(null, "Failed", CancellationToken.None);
            throw;
        }
    }

    private async ValueTask RecordObservationAsync(
        InputWaitObservation observation,
        CancellationToken cancellationToken)
    {
        try
        {
            await observationSinkField.RecordAsync(observation, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Observation persistence is best-effort. The turn result has already completed.
        }
    }

    private static Estimate EstimatePrompt(string prompt, IAgentTokenEstimator estimator)
    {
        string source = estimator.GetType().Name;
        try
        {
            return new Estimate(Math.Max(0, estimator.Estimate(prompt)), source, $"{source}:v1");
        }
        catch
        {
            return new Estimate(0, $"{source}:failed", $"{source}:v1");
        }
    }

    private static string? ModelFrom(AgentSessionSpec spec) =>
        spec.StartupOptions.TryGetValue("model", out string? model) && !string.IsNullOrWhiteSpace(model)
            ? model
            : null;

    private readonly record struct Estimate(int Tokens, string Source, string Version);

    private sealed class InputWaitProgressAgentSession(
        InputWaitProgressAgentRuntime owner,
        IAgentSession inner) : IAgentSession
    {
        public IAgentSession Inner => inner;

        public SessionIdentity SessionId => inner.SessionId;
        public string RepositoryId => inner.RepositoryId;
        public SessionRole Role => inner.Role;
        public AgentSessionMode Mode => inner.Mode;
        public AgentProcessState State => inner.State;
        public int CompletedTurns => inner.CompletedTurns;
        public AgentTokenUsage TotalUsage => inner.TotalUsage;
        public string? ThreadId => inner.ThreadId;

        public Task<AgentTurnResult> RunTurnAsync(
            string prompt,
            Func<AgentStreamChunk, Task>? onChunk = null,
            CancellationToken cancellationToken = default) =>
            owner.RunWithProgressAsync(
                inner.RepositoryId,
                inner.SessionId,
                inner.Role,
                TransportName(inner.Mode),
                model: null,
                prompt,
                onChunk,
                (wrapped, token) => inner.RunTurnAsync(prompt, wrapped, token),
                cancellationToken);

        public Task CancelAsync(CancellationToken cancellationToken = default) => inner.CancelAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();

        private static string TransportName(AgentSessionMode mode) =>
            mode == AgentSessionMode.Persistent ? "app-server" : "one-shot";
    }
}
