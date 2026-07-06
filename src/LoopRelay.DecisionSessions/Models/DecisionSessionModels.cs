using LoopRelay.DecisionSessions.Primitives;

namespace LoopRelay.DecisionSessions.Models;

public enum DecisionSessionState
{
    Created,
    Active,
    TransferPending,
    Transferred,
    Retired
}

public sealed record DecisionSessionOwnership(
    Guid RepositoryId,
    string CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record DecisionSessionMetadata(
    string? TransferReason = null,
    DecisionSessionId? TransferredToSessionId = null,
    DateTimeOffset? UpdatedAt = null);

public sealed record DecisionSession(
    DecisionSessionId Id,
    Guid RepositoryId,
    DecisionSessionState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? RetiredAt,
    DecisionSessionOwnership Ownership,
    DecisionSessionMetadata Metadata)
{
    public static DecisionSession Create(Guid repositoryId, string createdBy, DateTimeOffset createdAt)
    {
        return new DecisionSession(
            DecisionSessionId.New(),
            repositoryId,
            DecisionSessionState.Created,
            createdAt,
            null,
            null,
            new DecisionSessionOwnership(repositoryId, createdBy, createdAt),
            new DecisionSessionMetadata(UpdatedAt: createdAt));
    }
}

public sealed record DecisionSessionRecord(DecisionSession Session);

public sealed record DecisionSessionProjection(
    DecisionSessionId Id,
    Guid RepositoryId,
    DecisionSessionState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? RetiredAt,
    string CreatedBy)
{
    public static DecisionSessionProjection FromSession(DecisionSession session)
    {
        return new DecisionSessionProjection(
            session.Id,
            session.RepositoryId,
            session.State,
            session.CreatedAt,
            session.ActivatedAt,
            session.RetiredAt,
            session.Ownership.CreatedBy);
    }
}

public sealed record DecisionSessionDiagnostics(
    Guid RepositoryId,
    bool IsValid,
    int SessionCount,
    int ActiveSessionCount,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    DateTimeOffset GeneratedAt);

public sealed record DecisionSessionValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public static DecisionSessionValidationResult Valid { get; } = new(true, [], []);
}

public sealed record DecisionSessionRegistryDiagnostics(
    Guid RepositoryId,
    DecisionSessionValidationResult Validation,
    DateTimeOffset GeneratedAt);

public sealed class DecisionSessionConflictException(string message) : InvalidOperationException(message);

public sealed class DecisionSessionValidationException(string message) : InvalidOperationException(message);
