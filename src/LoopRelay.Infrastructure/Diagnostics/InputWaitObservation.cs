using LoopRelay.Agents.Models;

namespace LoopRelay.Infrastructure.Diagnostics;

public sealed record InputWaitObservation(
    string RepositoryId,
    SessionIdentity SessionId,
    SessionRole SessionRole,
    int TurnIndex,
    string Transport,
    string? Model,
    int PromptChars,
    int PromptBytes,
    int PromptTokensEstimated,
    string TokenEstimateSource,
    DateTimeOffset? PromptPreparedAt,
    DateTimeOffset? RequestWriteStartedAt,
    DateTimeOffset? RequestSubmittedAt,
    DateTimeOffset? RequestAcceptedAt,
    DateTimeOffset? FirstProtocolEventAt,
    DateTimeOffset? FirstOutputAt,
    DateTimeOffset? CompletedAt,
    string Status,
    string EstimatorVersion);

public interface IInputWaitObservationSink
{
    ValueTask RecordAsync(InputWaitObservation observation, CancellationToken cancellationToken);
}

public sealed class NullInputWaitObservationSink : IInputWaitObservationSink
{
    public static NullInputWaitObservationSink Instance { get; } = new();

    private NullInputWaitObservationSink() { }

    public ValueTask RecordAsync(InputWaitObservation observation, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}
