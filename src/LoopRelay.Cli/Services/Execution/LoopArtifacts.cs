using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Services.Artifacts;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Cli.Services.Execution;

/// <summary>
/// Coordinates canonical artifact/history persistence. It never evaluates recommendations or
/// converts their model/effort fields into effective runtime configuration.
/// </summary>
internal sealed class LoopArtifacts(
    IArtifactStore _store,
    Repository _repository,
    ILoopHistoryStore _historyStore,
    IExecutionRecommendationEvidenceStore _recommendations)
{
    private readonly IArtifactStore artifacts = _store is RepositoryArtifactStore
        ? _store
        : new RepositoryArtifactStore(_store, _repository);

    public Repository Repository => _repository;
    public IArtifactStore Store => artifacts;

    public Task<bool> ExistsAsync(string relativePath) => artifacts.ExistsAsync(relativePath);
    public Task<string?> ReadAsync(string relativePath) => artifacts.ReadAsync(relativePath);
    public Task WriteAsync(string relativePath, string content) => artifacts.WriteAsync(relativePath, content);
    public Task<string?> ReadPlanAsync() => ReadAsync(OrchestrationArtifactPaths.Plan);
    public Task<string?> ReadDetailsAsync() => ReadAsync(OrchestrationArtifactPaths.Details);

    public Task<string?> RotateLiveHandoffAsync(
        CanonicalCausalContext causality,
        HistoryEvidenceAttachments? evidence = null) =>
        RotateAsync(OrchestrationArtifactPaths.LiveHandoff, LoopHistoryKind.Handoff, causality, evidence);

    public async Task<string?> RotateLiveDecisionsAsync(
        CanonicalCausalContext causality,
        HistoryEvidenceAttachments? evidence = null)
    {
        string? content = await RotateAsync(
            OrchestrationArtifactPaths.Decisions, LoopHistoryKind.Decisions, causality, evidence);
        await InvalidateExecutionRecommendationProjectionAsync();
        return content;
    }

    public Task<string?> RotateOperationalDeltaAsync(
        CanonicalCausalContext causality,
        HistoryEvidenceAttachments? evidence = null) =>
        RotateAsync(OrchestrationArtifactPaths.OperationalDelta, LoopHistoryKind.OperationalDelta, causality, evidence);

    public Task<(string? Content, string? RelativePath)> ReadLatestHandoffAsync() =>
        ReadLatestAsync(OrchestrationArtifactPaths.LiveHandoff, LoopHistoryKind.Handoff);

    public Task<(string? Content, string? RelativePath)> ReadLatestDecisionsAsync() =>
        ReadLatestAsync(OrchestrationArtifactPaths.Decisions, LoopHistoryKind.Decisions);

    public async Task<bool> RetireLiveDecisionsAsync()
    {
        string live = OrchestrationArtifactPaths.Decisions;
        bool existed = await artifacts.ExistsAsync(live);
        if (existed) await artifacts.DeleteAsync(live);
        await InvalidateExecutionRecommendationProjectionAsync();
        return existed;
    }

    /// <summary>
    /// Persists two distinct canonical facts. The decision history identity becomes the exact
    /// decision-product version to which the immutable advisory recommendation is bound.
    /// </summary>
    public async Task<(LoopHistoryRecord Decision, ExecutionRecommendationEvidence Recommendation)>
        PersistDecisionsAsync(
            string decisions,
            ExecutionRecommendation recommendation,
            CanonicalCausalContext causality,
            AgentSessionIdentity sourceSession,
            TurnIdentity sourceTurn,
            string rationale,
            HistoryEvidenceAttachments? evidence = null,
            CancellationToken cancellationToken = default)
    {
        HistoryEvidenceAttachments attached = AttachRecommendationTurn(
            evidence, sourceSession, sourceTurn);
        LoopHistoryRecord decision = await _historyStore.AppendAsync(
            new LoopHistoryAppendRequest(
                LoopHistoryKind.Decisions,
                decisions,
                causality,
                attached),
            cancellationToken);
        var recommendationEvidence = new ExecutionRecommendationEvidence(
            ExecutionRecommendationIdentity.New(),
            new DecisionProductVersionIdentity(decision.Identity.Value),
            causality,
            sourceSession,
            sourceTurn,
            recommendation.Model,
            recommendation.Effort,
            rationale,
            DateTimeOffset.UtcNow);
        await _recommendations.AppendAsync(recommendationEvidence, cancellationToken);

        // The live files are compatibility projections only. Runtime and Policy Authority read
        // canonical history/evidence stores, never these files.
        await artifacts.WriteAsync(OrchestrationArtifactPaths.Decisions, decisions);
        PersistedExecutionRecommendation projection =
            ExecutionRecommendationContract.Bind(decisions, recommendation);
        await artifacts.WriteAsync(
            OrchestrationArtifactPaths.ExecutionRecommendation,
            ExecutionRecommendationContract.SerializePersisted(projection));
        return (decision, recommendationEvidence);
    }

    public async Task<ExecutionRecommendationEvidence> PersistRecommendationEvidenceAsync(
        DecisionProductVersionIdentity decisionProduct,
        string decisions,
        ExecutionRecommendation recommendation,
        CanonicalCausalContext causality,
        AgentSessionIdentity sourceSession,
        TurnIdentity sourceTurn,
        string rationale,
        CancellationToken cancellationToken = default)
    {
        var evidence = new ExecutionRecommendationEvidence(
            ExecutionRecommendationIdentity.New(), decisionProduct, causality, sourceSession, sourceTurn,
            recommendation.Model, recommendation.Effort, rationale, DateTimeOffset.UtcNow);
        await _recommendations.AppendAsync(evidence, cancellationToken);
        PersistedExecutionRecommendation projection =
            ExecutionRecommendationContract.Bind(decisions, recommendation);
        await artifacts.WriteAsync(
            OrchestrationArtifactPaths.ExecutionRecommendation,
            ExecutionRecommendationContract.SerializePersisted(projection));
        return evidence;
    }

    public async Task InvalidateExecutionRecommendationProjectionAsync()
    {
        string path = OrchestrationArtifactPaths.ExecutionRecommendation;
        if (await artifacts.ExistsAsync(path)) await artifacts.DeleteAsync(path);
    }

    public async Task EnsureOperationalContextAsync()
    {
        if (await ExistsAsync(OrchestrationArtifactPaths.OperationalContext)) return;
        string? plan = await ReadPlanAsync();
        if (plan is not null) await WriteAsync(OrchestrationArtifactPaths.OperationalContext, plan);
    }

    private async Task<string?> RotateAsync(
        string liveRelative,
        LoopHistoryKind kind,
        CanonicalCausalContext causality,
        HistoryEvidenceAttachments? evidence)
    {
        string? content = await artifacts.ReadAsync(liveRelative);
        if (content is null) return null;
        await _historyStore.AppendAsync(new LoopHistoryAppendRequest(kind, content, causality, evidence));
        await artifacts.DeleteAsync(liveRelative);
        return content;
    }

    private async Task<(string? Content, string? RelativePath)> ReadLatestAsync(
        string liveRelative,
        LoopHistoryKind kind)
    {
        string? live = await artifacts.ReadAsync(liveRelative);
        if (live is not null) return (live, liveRelative);
        LoopHistoryRecord? latest = await _historyStore.ReadLatestAsync(kind);
        return latest is null ? (null, null) : (latest.Content, latest.MaterializedRelativePath);
    }

    private static HistoryEvidenceAttachments AttachRecommendationTurn(
        HistoryEvidenceAttachments? evidence,
        AgentSessionIdentity session,
        TurnIdentity turn)
    {
        HistoryEvidenceAttachments current = evidence ?? HistoryEvidenceAttachments.Empty;
        return new HistoryEvidenceAttachments(
            current.Identity,
            current.Provider ?? new HistoryProviderEvidence("canonical-session", session.Value, turn.Value),
            current.Continuity,
            current.Recovery,
            current.Repository,
            current.Effects);
    }
}
