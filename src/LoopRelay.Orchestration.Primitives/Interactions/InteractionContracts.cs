using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Interactions;

public enum InteractionCategory
{
    DirtyInputCommitOffer,
    ImportConflict,
    RecoveryAmbiguity,
    CompletionAmbiguity,
}

public enum InteractionLifecycle
{
    Persisted,
    Presented,
    Responded,
    Rejected,
    Validated,
    Expired,
    Defaulted,
    Cancelled,
    Resolved,
    ResumeAuthorized,
}

public enum InteractionDeadlineBehavior
{
    None,
    ExpiresAt,
}

public enum InteractionRejectionReason
{
    NotFound,
    SchemaInvalid,
    MissingTrustEvidence,
    Late,
    Expired,
    Cancelled,
    AlreadyResolvedConflict,
    CompareAndSetConflict,
    SemanticIdempotencyConflict,
}

public sealed record InteractionCausalSubject(
    CanonicalCausalContext Causality,
    string SubjectKind,
    string SubjectIdentity);

public sealed record InteractionCategoryPolicy(
    InteractionPolicyEvaluationIdentity Identity,
    InteractionCategory Category,
    string QuestionVersion,
    string ResponseSchemaVersion,
    string ResponseJsonSchema,
    string ResponseSchemaHash,
    InteractionDeadlineBehavior DeadlineBehavior,
    DateTimeOffset? Deadline,
    string? DefaultResponseJson,
    RuntimeOutcomeKind HeadlessOutcome,
    IReadOnlyList<string> RequiredTrustEvidence,
    string ResolverOwner,
    string ResolvedPolicyIdentity,
    DateTimeOffset EvaluatedAt);

public sealed record InteractionRequest(
    InteractionRequestIdentity Identity,
    InteractionCategory Category,
    InteractionCausalSubject Subject,
    string Question,
    string PresentationJson,
    InteractionCategoryPolicy Policy,
    IReadOnlyList<string> CreationEvidence,
    string SemanticIdempotencyKey,
    DateTimeOffset CreatedAt);

public sealed record InteractionResponse(
    InteractionResponseIdentity Identity,
    InteractionRequestIdentity Request,
    string ResponseJson,
    string SemanticResponseHash,
    string SemanticIdempotencyKey,
    IReadOnlyList<string> TrustEvidence,
    string ResponderIdentity,
    DateTimeOffset RespondedAt);

public sealed record InteractionLifecycleEvent(
    InteractionEventIdentity Identity,
    InteractionRequestIdentity Request,
    InteractionLifecycle Lifecycle,
    string Explanation,
    IReadOnlyList<string> Evidence,
    DateTimeOffset RecordedAt);

public sealed record InteractionAggregate(
    InteractionRequest Request,
    InteractionLifecycle State,
    long RowVersion,
    InteractionResponse? AcceptedResponse,
    IReadOnlyList<InteractionLifecycleEvent> Events)
{
    public bool ResumeAuthorized => Events.Any(item => item.Lifecycle == InteractionLifecycle.ResumeAuthorized);
}

public sealed record InteractionResponseResult(
    bool Accepted,
    bool IdempotentDuplicate,
    InteractionResponse? Response,
    InteractionRejectionReason? Rejection,
    string Explanation,
    InteractionAggregate? Aggregate);

public sealed record CreateInteractionCommand(InteractionRequest Request);
public sealed record ListInteractionsQuery(bool OutstandingOnly = true);
public sealed record ShowInteractionQuery(InteractionRequestIdentity Request);
public sealed record RespondInteractionCommand(
    InteractionRequestIdentity Request,
    string ResponseJson,
    string SemanticIdempotencyKey,
    string ResponderIdentity,
    IReadOnlyList<string> TrustEvidence,
    long ExpectedRowVersion);
public sealed record CancelInteractionCommand(
    InteractionRequestIdentity Request,
    string Explanation,
    long ExpectedRowVersion);
public sealed record ResolveInteractionCommand(
    InteractionRequestIdentity Request,
    long ExpectedRowVersion,
    IReadOnlyList<string> Evidence);

public interface IInteractionStore
{
    Task<InteractionAggregate?> ReadAsync(InteractionRequestIdentity request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InteractionAggregate>> ListAsync(bool outstandingOnly, CancellationToken cancellationToken = default);
    Task<InteractionAggregate?> ReadBySemanticIdempotencyKeyAsync(string key, CancellationToken cancellationToken = default);
    Task PersistRequestAsync(InteractionRequest request, CancellationToken cancellationToken = default);
    Task<InteractionAggregate> AppendEventAsync(
        InteractionLifecycleEvent lifecycleEvent,
        long expectedRowVersion,
        CancellationToken cancellationToken = default);
    Task<InteractionResponseResult> TryAcceptResponseAsync(
        InteractionResponse response,
        long expectedRowVersion,
        CancellationToken cancellationToken = default);
}

public interface IInteractionBroker
{
    Task<InteractionAggregate> CreateAsync(CreateInteractionCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InteractionAggregate>> ListAsync(ListInteractionsQuery query, CancellationToken cancellationToken = default);
    Task<InteractionAggregate> ShowAsync(ShowInteractionQuery query, CancellationToken cancellationToken = default);
    Task<InteractionAggregate> PresentAsync(InteractionRequestIdentity request, long expectedRowVersion, CancellationToken cancellationToken = default);
    Task<InteractionResponseResult> RespondAsync(RespondInteractionCommand command, CancellationToken cancellationToken = default);
    Task<InteractionAggregate> CancelAsync(CancelInteractionCommand command, CancellationToken cancellationToken = default);
    Task<InteractionAggregate> ResolveAsync(ResolveInteractionCommand command, CancellationToken cancellationToken = default);
}
