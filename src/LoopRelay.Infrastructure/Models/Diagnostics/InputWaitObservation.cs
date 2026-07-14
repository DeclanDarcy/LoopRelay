using LoopRelay.Agents.Primitives;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Infrastructure.Models.Diagnostics;

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
