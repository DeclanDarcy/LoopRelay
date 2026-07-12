using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Models;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Orchestration.Persistence;

public sealed class CanonicalExecutionRecommendationEvidenceStore(CanonicalWorkflowPersistenceStore _store)
    : IExecutionRecommendationEvidenceStore
{
    public Task AppendAsync(
        ExecutionRecommendationEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        CanonicalCausalContext causal = evidence.SourceCausality;
        return _store.AppendExecutionRecommendationEvidenceAsync(
            new CanonicalExecutionRecommendationEvidenceRecord(
                evidence.Identity.Value,
                evidence.DecisionProduct.Value,
                causal.Workspace.Value,
                causal.Run.Value,
                causal.WorkflowInstance.Value,
                causal.TransitionRun.Value,
                causal.Attempt.Value,
                evidence.SourceSession.Value,
                evidence.SourceTurn.Value,
                AgentConfigurationCatalog.Format(evidence.RecommendedModel),
                AgentConfigurationCatalog.Format(evidence.RecommendedEffort),
                evidence.Rationale,
                evidence.SchemaVersion,
                evidence.CreatedAt),
            cancellationToken);
    }

    public async Task<ExecutionRecommendationEvidence?> ReadAsync(
        ExecutionRecommendationIdentity identity,
        CancellationToken cancellationToken = default)
    {
        CanonicalExecutionRecommendationEvidenceRecord? record =
            await _store.ReadExecutionRecommendationEvidenceAsync(identity.Value, cancellationToken);
        return record is null ? null : ToEvidence(record);
    }

    public async Task<ExecutionRecommendationEvidence?> ReadForDecisionAsync(
        DecisionProductVersionIdentity decisionProduct,
        CancellationToken cancellationToken = default)
    {
        CanonicalExecutionRecommendationEvidenceRecord? record =
            await _store.ReadExecutionRecommendationForDecisionAsync(decisionProduct.Value, cancellationToken);
        return record is null ? null : ToEvidence(record);
    }

    public async Task<ExecutionRecommendationEvidence?> ReadLatestAsync(
        CancellationToken cancellationToken = default)
    {
        CanonicalExecutionRecommendationEvidenceRecord? record =
            await _store.ReadLatestExecutionRecommendationAsync(cancellationToken);
        return record is null ? null : ToEvidence(record);
    }

    private static ExecutionRecommendationEvidence ToEvidence(
        CanonicalExecutionRecommendationEvidenceRecord record) => new(
            new ExecutionRecommendationIdentity(record.RecommendationId),
            new DecisionProductVersionIdentity(record.DecisionProductId),
            new CanonicalCausalContext(
                new WorkspaceIdentity(record.WorkspaceId), new RunIdentity(record.RunId),
                new WorkflowInstanceIdentity(record.WorkflowInstanceId),
                new TransitionRunIdentity(record.TransitionRunId), new AttemptIdentity(record.AttemptId)),
            new AgentSessionIdentity(record.SessionId),
            new TurnIdentity(record.TurnId),
            AgentConfigurationCatalog.ParseModel(record.RecommendedModel),
            AgentConfigurationCatalog.ParseEffort(record.RecommendedEffort),
            record.Rationale,
            record.CreatedAt,
            record.SchemaVersion);
}

public sealed class CanonicalRuntimeProfileEvaluationStore(CanonicalWorkflowPersistenceStore _store)
    : IRuntimeProfileEvaluationStore, IResolvedRuntimeProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task AppendAsync(RuntimeProfileEvaluation evaluation, CancellationToken cancellationToken = default) =>
        _store.AppendRuntimeProfileEvaluationAsync(
            new CanonicalRuntimeProfileEvaluationRecord(
                evaluation.Identity.Value,
                evaluation.Recommendation?.Value,
                evaluation.DecisionProduct.Value,
                evaluation.Policy.Value,
                evaluation.ProviderCapabilities.Identity.Value,
                JsonSerializer.Serialize(evaluation.ProviderCapabilities, JsonOptions),
                evaluation.Outcome.ToString(),
                evaluation.EffectiveProfile.Identity.Value,
                JsonSerializer.Serialize(evaluation.EffectiveProfile, JsonOptions),
                evaluation.Reasons,
                evaluation.EvaluatedAt),
            cancellationToken);

    public async Task<RuntimeProfileEvaluation?> ReadAsync(
        RuntimeProfileEvaluationIdentity identity,
        CancellationToken cancellationToken = default)
    {
        CanonicalRuntimeProfileEvaluationRecord? record =
            await _store.ReadRuntimeProfileEvaluationAsync(identity.Value, cancellationToken);
        return record is null ? null : ToEvaluation(record);
    }

    Task IResolvedRuntimeProfileStore.AppendAsync(
        ResolvedRuntimeProfile profile,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "Resolved runtime profiles are persisted only as part of their policy evaluation.");

    async Task<ResolvedRuntimeProfile?> IResolvedRuntimeProfileStore.ReadAsync(
        RuntimeProfileIdentity identity,
        CancellationToken cancellationToken)
    {
        CanonicalRuntimeProfileEvaluationRecord? record =
            await _store.ReadRuntimeProfileEvaluationByProfileAsync(identity.Value, cancellationToken);
        return record is null ? null : DeserializeProfile(record.EffectiveProfileJson);
    }

    private static RuntimeProfileEvaluation ToEvaluation(CanonicalRuntimeProfileEvaluationRecord record) =>
        new(
            new RuntimeProfileEvaluationIdentity(record.EvaluationId),
            record.RecommendationId is null ? null : new ExecutionRecommendationIdentity(record.RecommendationId),
            new DecisionProductVersionIdentity(record.DecisionProductId),
            new PolicyIdentity(record.PolicyId),
            JsonSerializer.Deserialize<ProviderCapabilityEvidence>(record.ProviderCapabilityJson, JsonOptions)
                ?? throw new InvalidDataException("Persisted provider capability evidence was empty."),
            Enum.Parse<RuntimeProfileEvaluationOutcome>(record.Outcome),
            DeserializeProfile(record.EffectiveProfileJson),
            record.Reasons,
            record.EvaluatedAt);

    private static ResolvedRuntimeProfile DeserializeProfile(string json) =>
        JsonSerializer.Deserialize<ResolvedRuntimeProfile>(json, JsonOptions)
        ?? throw new InvalidDataException("Persisted runtime profile was empty.");
}
