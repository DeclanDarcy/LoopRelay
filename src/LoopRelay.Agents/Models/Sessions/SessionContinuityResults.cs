using System.Text.Json;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Agents.Models.Sessions;

public sealed record ProviderSessionReference(string Provider, string ThreadId);

public sealed record SessionTransportProgress(
    bool RequestWriteStarted,
    bool RequestSubmitted,
    bool ResponseAcknowledged,
    bool TurnSubmitted,
    int? ProcessExitCode,
    string? BoundedStderr);

public sealed record SessionOperationFailure(
    string Classification,
    string ProviderMethod,
    long? RequestId,
    int? JsonRpcCode,
    JsonElement ErrorData,
    string? RedactedDiagnostic,
    bool TimedOut,
    bool Cancelled);

public sealed record SessionResumeRequest(
    AgentSessionSpec SessionSpec,
    ProviderSessionReference Original,
    SessionContinuityProfile Profile,
    int Attempt = 1,
    TimeSpan? Timeout = null);

public sealed record SessionResumeResult(
    SessionResumeOutcome Outcome,
    IAgentSession? Session,
    ProviderSessionReference Original,
    ProviderSessionReference? Resolved,
    SessionOperationStage Stage,
    SessionOperationFailure? Failure,
    SessionTransportProgress Transport,
    string ContinuityProfileDigest,
    int Attempt)
{
    public bool IsReplacementEligible => Outcome is SessionResumeOutcome.UnavailableSession or SessionResumeOutcome.CorruptedState
        && !Transport.TurnSubmitted;
}

public sealed record SessionCreateRequest(
    AgentSessionSpec SessionSpec,
    SessionContinuityProfile Profile,
    string IdempotencyKey,
    TimeSpan? Timeout = null);

public sealed record SessionCreateResult(
    bool Succeeded,
    IAgentSession? Session,
    ProviderSessionReference? Created,
    SessionOperationStage Stage,
    SessionOperationFailure? Failure,
    SessionTransportProgress Transport,
    string ContinuityProfileDigest);

public sealed record SessionContinuityNegotiationRequest(
    string Provider,
    string ClientVersion,
    string? ServerVersion,
    string? ExecutableIdentity,
    string? ProtocolIdentity,
    string? SchemaDigest,
    JsonElement InitializeResult,
    bool OfferExperimentalApi);

public sealed record SessionContinuityNegotiationResult(
    SessionContinuityProfile Profile,
    bool FromCertifiedManifest,
    string Evidence);

public sealed record SessionContentRequest(
    AgentSessionSpec SessionSpec,
    ProviderSessionReference Session,
    SessionContinuityProfile Profile,
    TimeSpan? Timeout = null);
public sealed record SessionContentRecord(
    int Order,
    string Kind,
    string Role,
    string Text,
    string? ProviderItemId,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record SessionContentResult(
    bool Succeeded,
    string? ContentDigest,
    SessionOperationFailure? Failure,
    IReadOnlyList<SessionContentRecord>? Records = null,
    string? VerifiedBoundary = null,
    bool Partial = false);
public sealed record SessionSeedRequest(
    IAgentSession Target,
    ProviderSessionReference Session,
    string Content,
    string Marker,
    SessionContinuityProfile Profile,
    TimeSpan? Timeout = null);
public sealed record SessionSeedResult(
    bool Succeeded,
    SessionOperationFailure? Failure,
    string? ProviderTurnId = null,
    string? Output = null,
    SessionTransportProgress? Transport = null);
public sealed record SessionForkRequest(
    AgentSessionSpec SessionSpec,
    ProviderSessionReference Parent,
    SessionContinuityProfile Profile,
    string IdempotencyKey,
    TimeSpan? Timeout = null);
public sealed record SessionForkResult(
    bool Succeeded,
    IAgentSession? Session,
    ProviderSessionReference Parent,
    ProviderSessionReference? Child,
    SessionOperationFailure? Failure,
    SessionTransportProgress? Transport = null,
    string? HistoryDigest = null);
public sealed record SessionReconcileRequest(
    SessionContinuityOperation Operation,
    string CorrelationId,
    SessionContinuityProfile Profile,
    AgentSessionSpec? SessionSpec = null,
    ProviderSessionReference? Parent = null);
public sealed record SessionReconcileResult(
    bool Resolved,
    ProviderSessionReference? Session,
    SessionOperationFailure? Failure,
    IReadOnlyList<ProviderSessionReference>? Candidates = null);
