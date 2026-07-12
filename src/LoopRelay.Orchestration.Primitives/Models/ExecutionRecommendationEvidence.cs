using LoopRelay.Core.Models.Identity;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Orchestration.Models;

public static class ExecutionRecommendationEvidenceSchemas
{
    public const string Version1 = "execution-recommendation-evidence.v1";
}

/// <summary>Immutable advisory fact. It can inform Policy Authority but cannot configure Runtime.</summary>
public sealed record ExecutionRecommendationEvidence
{
    public ExecutionRecommendationEvidence(
        ExecutionRecommendationIdentity identity,
        DecisionProductVersionIdentity decisionProduct,
        CanonicalCausalContext sourceCausality,
        AgentSessionIdentity sourceSession,
        TurnIdentity sourceTurn,
        AgentModel recommendedModel,
        AgentEffort recommendedEffort,
        string rationale,
        DateTimeOffset createdAt,
        string schemaVersion = ExecutionRecommendationEvidenceSchemas.Version1)
    {
        ArgumentNullException.ThrowIfNull(sourceCausality);
        if (identity.IsEmpty || decisionProduct.IsEmpty || sourceSession.IsEmpty || sourceTurn.IsEmpty ||
            string.IsNullOrWhiteSpace(rationale) || string.IsNullOrWhiteSpace(schemaVersion) ||
            !Enum.IsDefined(recommendedModel) || !Enum.IsDefined(recommendedEffort))
        {
            throw new ArgumentException("Execution recommendation evidence must be causally and structurally complete.");
        }

        Identity = identity;
        DecisionProduct = decisionProduct;
        SourceCausality = sourceCausality;
        SourceSession = sourceSession;
        SourceTurn = sourceTurn;
        RecommendedModel = recommendedModel;
        RecommendedEffort = recommendedEffort;
        Rationale = rationale.Trim();
        CreatedAt = createdAt;
        SchemaVersion = schemaVersion;
    }

    public ExecutionRecommendationIdentity Identity { get; }
    public DecisionProductVersionIdentity DecisionProduct { get; }
    public CanonicalCausalContext SourceCausality { get; }
    public AgentSessionIdentity SourceSession { get; }
    public TurnIdentity SourceTurn { get; }
    public AgentModel RecommendedModel { get; }
    public AgentEffort RecommendedEffort { get; }
    public string Rationale { get; }
    public DateTimeOffset CreatedAt { get; }
    public string SchemaVersion { get; }
}

public sealed record ProviderCapabilityEvidence(
    ProviderCapabilityEvidenceIdentity Identity,
    string Provider,
    IReadOnlyList<AgentModel> AvailableModels,
    AgentEffort MaximumEffort,
    DateTimeOffset ObservedAt);

public sealed record ResolvedRuntimeProfile(
    RuntimeProfileIdentity Identity,
    string Provider,
    AgentModel Model,
    AgentEffort Effort,
    string ExecutionMode,
    string SandboxProfile,
    string PermissionProfile,
    string ApprovalPolicy,
    string ContinuityPolicy,
    TimeSpan Timeout,
    string UsagePolicy,
    string RecoveryPolicy);

public enum RuntimeProfileEvaluationOutcome
{
    Accepted,
    Constrained,
    Rejected,
    IgnoredByPolicy,
    Stale,
    Invalid,
    Unsupported,
}

public sealed record RuntimeProfileEvaluation(
    RuntimeProfileEvaluationIdentity Identity,
    ExecutionRecommendationIdentity? Recommendation,
    DecisionProductVersionIdentity DecisionProduct,
    PolicyIdentity Policy,
    ProviderCapabilityEvidence ProviderCapabilities,
    RuntimeProfileEvaluationOutcome Outcome,
    ResolvedRuntimeProfile EffectiveProfile,
    IReadOnlyList<string> Reasons,
    DateTimeOffset EvaluatedAt);

/// <summary>Only this governed input crosses from Policy Authority into execution.</summary>
public sealed record ExecutionAuthorization
{
    public ExecutionAuthorization(
        ExecutionAuthorizationIdentity identity,
        DecisionProductVersionIdentity decisionProduct,
        RuntimeProfileIdentity runtimeProfile,
        RuntimeProfileEvaluationIdentity policyEvaluation,
        RenderedPromptFactIdentity executionPrompt,
        ConsumedInputManifestIdentity causalInputManifest,
        CanonicalCausalContext causality)
    {
        ArgumentNullException.ThrowIfNull(causality);
        if (identity.IsEmpty || decisionProduct.IsEmpty || runtimeProfile.IsEmpty ||
            policyEvaluation.IsEmpty || executionPrompt.IsEmpty || causalInputManifest.IsEmpty)
        {
            throw new ArgumentException("Execution authorization must be causally complete.");
        }

        Identity = identity;
        DecisionProduct = decisionProduct;
        RuntimeProfile = runtimeProfile;
        PolicyEvaluation = policyEvaluation;
        ExecutionPrompt = executionPrompt;
        CausalInputManifest = causalInputManifest;
        Causality = causality;
    }

    public ExecutionAuthorizationIdentity Identity { get; }
    public DecisionProductVersionIdentity DecisionProduct { get; }
    public RuntimeProfileIdentity RuntimeProfile { get; }
    public RuntimeProfileEvaluationIdentity PolicyEvaluation { get; }
    public RenderedPromptFactIdentity ExecutionPrompt { get; }
    public ConsumedInputManifestIdentity CausalInputManifest { get; }
    public CanonicalCausalContext Causality { get; }
}

public interface IExecutionRecommendationEvidenceStore
{
    Task AppendAsync(ExecutionRecommendationEvidence evidence, CancellationToken cancellationToken = default);
    Task<ExecutionRecommendationEvidence?> ReadAsync(
        ExecutionRecommendationIdentity identity,
        CancellationToken cancellationToken = default);
    Task<ExecutionRecommendationEvidence?> ReadForDecisionAsync(
        DecisionProductVersionIdentity decisionProduct,
        CancellationToken cancellationToken = default);
}

public interface IRuntimeProfileEvaluationStore
{
    Task AppendAsync(RuntimeProfileEvaluation evaluation, CancellationToken cancellationToken = default);
    Task<RuntimeProfileEvaluation?> ReadAsync(
        RuntimeProfileEvaluationIdentity identity,
        CancellationToken cancellationToken = default);
}

public interface IResolvedRuntimeProfileStore
{
    Task AppendAsync(ResolvedRuntimeProfile profile, CancellationToken cancellationToken = default);
    Task<ResolvedRuntimeProfile?> ReadAsync(
        RuntimeProfileIdentity identity,
        CancellationToken cancellationToken = default);
}
