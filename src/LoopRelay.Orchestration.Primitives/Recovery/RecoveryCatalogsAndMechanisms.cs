using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Orchestration.Recovery;

public sealed class RecoveryMechanismCatalog(IEnumerable<IRecoveryMechanism> mechanisms) : IRecoveryMechanismCatalog
{
    public IReadOnlyList<IRecoveryMechanism> All { get; } = mechanisms
        .OrderBy(mechanism => mechanism.Key.Identity, StringComparer.Ordinal)
        .ThenBy(mechanism => mechanism.Key.Version, StringComparer.Ordinal)
        .ToArray();

    public IRecoveryMechanism Resolve(RecoveryMechanismKey key) =>
        All.SingleOrDefault(mechanism => mechanism.Key == key)
        ?? throw new InvalidOperationException($"Recovery mechanism {key.Identity}@{key.Version} is not registered.");
}

public sealed class RecoverySourceCatalog(IEnumerable<IRecoverySource> sources) : IRecoverySourceCatalog
{
    public IReadOnlyList<IRecoverySource> All { get; } = sources
        .OrderBy(source => source.Kind, StringComparer.Ordinal)
        .ToArray();
}

public sealed class ThreadReadReconstructionMechanism : ReconstructionMechanism
{
    public override RecoveryMechanismKey Key { get; } = new("ThreadReadReconstruction", "1");
    public override string LineageMechanism => "PublicProjection";
    protected override string RequiredSourceKind => "ThreadRead";

    public override RecoveryMechanismEligibility EvaluateEligibility(RecoveryPlanningInput input)
    {
        if (input.Profile.Operation(SessionContinuityOperation.ConversationRead).Status != SessionOperationSupport.Supported)
        {
            return Ineligible("ConversationRead is not Supported.");
        }

        return base.EvaluateEligibility(input);
    }
}

public sealed class RolloutReconstructionMechanism : ReconstructionMechanism
{
    public override RecoveryMechanismKey Key { get; } = new("RolloutReconstruction", "1");
    public override string LineageMechanism => "RolloutSalvage";
    protected override string RequiredSourceKind => "RolloutSalvage";
}

public sealed class RepositoryReconstructionMechanism : ReconstructionMechanism
{
    public override RecoveryMechanismKey Key { get; } = new("RepositoryReconstruction", "1");
    public override string LineageMechanism => "RepositoryOnly";
    protected override string RequiredSourceKind => "Repository";
}

public abstract class ReconstructionMechanism : IRecoveryMechanism
{
    public abstract RecoveryMechanismKey Key { get; }
    public abstract string LineageMechanism { get; }
    protected abstract string RequiredSourceKind { get; }
    public RecoveryActivationStrategy ActivationStrategy => RecoveryActivationStrategy.EagerCreateAndInject;
    public bool RequiresContextInjection => true;
    public string ValidationStrategy => "exact-marker-and-envelope-digest.v1";
    public string ReconciliationStrategy => "thread-read-marker-absence-before-retry.v1";

    public RecoveryRuntimeOutcome SuccessOutcome(RecoveryCompleteness completeness) => completeness switch
    {
        RecoveryCompleteness.Full => RecoveryRuntimeOutcome.ReplacementRecoveredFull,
        RecoveryCompleteness.RepositoryOnly => RecoveryRuntimeOutcome.ReplacementRepositoryOnly,
        _ => RecoveryRuntimeOutcome.ReplacementRecoveredPartial,
    };

    public virtual RecoveryMechanismEligibility EvaluateEligibility(RecoveryPlanningInput input)
    {
        if (input.Profile.Operation(SessionContinuityOperation.ConversationWrite).Status != SessionOperationSupport.Supported)
        {
            return Ineligible("ConversationWrite is not Supported.");
        }

        if (input.Profile.MaximumRecoverableContext is null || input.ContextBudget <= 0)
        {
            return Ineligible("MaximumRecoverableContext or effective context budget is unknown.");
        }

        if (string.IsNullOrWhiteSpace(input.EnvelopeDigest))
        {
            return Ineligible("A verified canonical recovery envelope is unavailable.");
        }

        RecoverySourceDescriptor? source = input.Sources
            .Where(candidate => candidate.Kind == RequiredSourceKind)
            .OrderBy(candidate => candidate.Order)
            .FirstOrDefault();
        if (source is null)
        {
            return Ineligible($"Required source {RequiredSourceKind} is unavailable.");
        }
        RecoverySourceDescriptor? repository = input.Sources
            .Where(candidate => candidate.Kind == "Repository")
            .OrderBy(candidate => candidate.Order)
            .FirstOrDefault();
        if (repository is null)
        {
            return Ineligible("Repository-authoritative base context is unavailable.");
        }

        return new RecoveryMechanismEligibility(
            true,
            source.Completeness,
            [
                $"source={source.Kind}:{source.Digest}",
                $"repository={repository.Digest}",
                $"profile={input.Profile.Digest}",
                $"budget={input.ContextBudget}",
            ]);
    }

    protected static RecoveryMechanismEligibility Ineligible(string reason) =>
        new(false, RecoveryCompleteness.Unknown, [reason]);

    public async Task<RecoveryMechanismExecutionResult> ExecuteAsync(
        RecoveryMechanismExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Plan.Mechanism != Key)
        {
            return KnownFailure("The persisted plan does not select this mechanism.");
        }

        RecoverySourceObservation? source = request.Sources.SingleOrDefault(candidate =>
            candidate.Descriptor.Kind == RequiredSourceKind
            && request.Plan.Sources.Any(planned =>
                planned.Kind == candidate.Descriptor.Kind
                && planned.Digest == candidate.Descriptor.Digest));
        if (source is null || string.IsNullOrWhiteSpace(request.CanonicalEnvelope))
        {
            return KnownFailure("The required planned source or canonical envelope is unavailable.");
        }
        if (!request.Sources.Any(candidate =>
                candidate.Descriptor.Kind == "Repository"
                && request.Plan.Sources.Any(planned =>
                    planned.Kind == "Repository" && planned.Digest == candidate.Descriptor.Digest)))
        {
            return KnownFailure("The planned repository-authoritative base source is unavailable.");
        }

        if (request.Phase == RecoveryMechanismExecutionPhase.CreateReplacement)
        {
            SessionCreateResult created = await request.ContinuityRuntime.CreateSessionAsync(
                request.CreateRequest, cancellationToken);
            if (!created.Succeeded || created.Session is null || created.Created is null)
            {
                return new RecoveryMechanismExecutionResult(
                    created.Transport.RequestSubmitted
                        ? RecoveryMechanismExecutionStatus.UnknownOutcome
                        : RecoveryMechanismExecutionStatus.KnownFailure,
                    null, null, created, null, created.Failure,
                    "Replacement creation did not produce a verified provider session.");
            }

            return new RecoveryMechanismExecutionResult(
                RecoveryMechanismExecutionStatus.Succeeded,
                created.Session,
                created.Created,
                created,
                null,
                null,
                null);
        }

        if (request.ExistingSession is null || request.ExistingReplacement is null)
        {
            return KnownFailure("Context injection requires the durably recorded replacement identity and live session.");
        }

        SessionSeedResult seed = await request.ContinuityRuntime.SeedSessionAsync(
            new SessionSeedRequest(
                request.ExistingSession,
                request.ExistingReplacement,
                request.CanonicalEnvelope,
                request.Marker,
                request.CreateRequest.Profile,
                request.CreateRequest.Timeout),
            cancellationToken);
        if (!seed.Succeeded)
        {
            return new RecoveryMechanismExecutionResult(
                seed.Transport?.TurnSubmitted == true
                    ? RecoveryMechanismExecutionStatus.UnknownOutcome
                    : RecoveryMechanismExecutionStatus.KnownFailure,
                request.ExistingSession, request.ExistingReplacement, null, seed, seed.Failure,
                "Context injection did not reach a verified marker acknowledgement.");
        }

        return new RecoveryMechanismExecutionResult(
            RecoveryMechanismExecutionStatus.Succeeded,
            request.ExistingSession,
            request.ExistingReplacement,
            null,
            seed,
            null,
            null);
    }

    public async Task<RecoveryMechanismExecutionResult> ReconcileAsync(
        RecoveryMechanismExecutionRequest request,
        RecoveryMechanismExecutionResult previous,
        CancellationToken cancellationToken = default)
    {
        if (previous.Replacement is null)
        {
            SessionReconcileResult reconciliation = await request.ContinuityRuntime.ReconcileAsync(
                new SessionReconcileRequest(
                    SessionContinuityOperation.Create,
                    request.Plan.IdempotencyIdentity,
                    request.CreateRequest.Profile),
                cancellationToken);
            return reconciliation.Resolved && reconciliation.Session is not null
                ? previous with { Replacement = reconciliation.Session, Diagnostic = "Replacement identity reconciled; a live handle is still required." }
                : previous;
        }

        SessionContentResult read = await request.ContinuityRuntime.ReadSessionAsync(
            new SessionContentRequest(
                request.CreateRequest.SessionSpec,
                previous.Replacement,
                request.CreateRequest.Profile,
                request.CreateRequest.Timeout),
            cancellationToken);
        bool markerPresent = read.Succeeded
            && read.Records?.Any(record => record.Text.Contains(request.Marker, StringComparison.Ordinal)) == true;
        return markerPresent
            ? previous with { Status = RecoveryMechanismExecutionStatus.Succeeded, Diagnostic = null }
            : previous;
    }

    public Task<RecoveryMechanismValidationResult> ValidateAsync(
        RecoveryMechanismExecutionRequest request,
        RecoveryMechanismExecutionResult result,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool identity = result.Session is not null
            && result.Replacement is not null
            && string.Equals(result.Session.ThreadId, result.Replacement.ThreadId, StringComparison.Ordinal);
        bool marker = result.Seed?.Succeeded == true
            && result.Seed.Output?.Contains(request.Marker, StringComparison.Ordinal) == true;
        return Task.FromResult(new RecoveryMechanismValidationResult(
            identity && marker,
            result.Status == RecoveryMechanismExecutionStatus.UnknownOutcome,
            identity && marker ? null : "Replacement identity or exact recovery marker was not verified."));
    }

    private static RecoveryMechanismExecutionResult KnownFailure(string diagnostic) =>
        new(RecoveryMechanismExecutionStatus.KnownFailure, null, null, null, null, null, diagnostic);
}
