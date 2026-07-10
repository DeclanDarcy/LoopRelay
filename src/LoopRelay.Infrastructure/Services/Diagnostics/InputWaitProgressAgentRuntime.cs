using System.Text;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services;
using LoopRelay.Agents.Services.Sessions;
using LoopRelay.Infrastructure.Abstractions.Diagnostics;
using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Infrastructure.Services.Diagnostics;

public sealed class InputWaitProgressAgentRuntime(
    IAgentRuntime _inner,
    IAgentTokenEstimator _tokenEstimator,
    IInputWaitProgressRenderer _renderer,
    IInputWaitObservationSink? _observationSink = null) : IAgentRuntime
{
    private readonly IInputWaitObservationSink _observationSinkField =
        _observationSink ?? NullInputWaitObservationSink.Instance;

    public async Task<IAgentSession> OpenSessionAsync(
        AgentSessionSpec spec,
        CancellationToken cancellationToken = default)
    {
        IAgentSession session = await _inner.OpenSessionAsync(spec, cancellationToken);
        return new InputWaitProgressAgentSession(this, session, AgentConfigurationCatalog.Format(spec.Model));
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
            (wrapped, token) => _inner.RunOneShotAsync(spec, prompt, wrapped, token),
            cancellationToken);

    public ValueTask CloseSessionAsync(IAgentSession session) =>
        _inner.CloseSessionAsync(session is InputWaitProgressAgentSession progress ? progress.Inner : session);

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
        Estimate estimate = EstimatePrompt(prompt, _tokenEstimator);
        await using var tracker = new InputWaitTurnTracker(
            _renderer,
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
            await _observationSinkField.RecordAsync(observation, cancellationToken);
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

    private static string ModelFrom(AgentSessionSpec spec) =>
        AgentConfigurationCatalog.Format(spec.Model);

    private readonly record struct Estimate(int Tokens, string Source, string Version);

    private sealed class InputWaitProgressAgentSession(
        InputWaitProgressAgentRuntime _owner,
        IAgentSession _inner,
        string _model) : IAgentSession
    {
        public IAgentSession Inner => _inner;

        public SessionIdentity SessionId => _inner.SessionId;
        public string RepositoryId => _inner.RepositoryId;
        public SessionRole Role => _inner.Role;
        public AgentSessionMode Mode => _inner.Mode;
        public AgentProcessState State => _inner.State;
        public int CompletedTurns => _inner.CompletedTurns;
        public AgentTokenUsage TotalUsage => _inner.TotalUsage;
        public string? ThreadId => _inner.ThreadId;

        public Task<AgentTurnResult> RunTurnAsync(
            string prompt,
            Func<AgentStreamChunk, Task>? onChunk = null,
            CancellationToken cancellationToken = default) =>
            _owner.RunWithProgressAsync(
                _inner.RepositoryId,
                _inner.SessionId,
                _inner.Role,
                TransportName(_inner.Mode),
                _model,
                prompt,
                onChunk,
                (wrapped, token) => _inner.RunTurnAsync(prompt, wrapped, token),
                cancellationToken);

        public Task CancelAsync(CancellationToken cancellationToken = default) => _inner.CancelAsync(cancellationToken);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();

        private static string TransportName(AgentSessionMode mode) =>
            mode == AgentSessionMode.Persistent ? "app-server" : "one-shot";
    }
}
