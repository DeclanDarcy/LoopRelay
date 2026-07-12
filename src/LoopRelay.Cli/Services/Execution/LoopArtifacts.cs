using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
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
    public Repository Repository => _repository;
    public IArtifactStore Store => _store;

    public Task<bool> ExistsAsync(string relativePath) => _store.ExistsAsync(Resolve(relativePath));
    public Task<string?> ReadAsync(string relativePath) => _store.ReadAsync(Resolve(relativePath));
    public Task WriteAsync(string relativePath, string content) => _store.WriteAsync(Resolve(relativePath), content);
    public Task<bool> ExistsAbsoluteAsync(string absolutePath) => _store.ExistsAsync(absolutePath);
    public Task<string?> ReadAbsoluteAsync(string absolutePath) => _store.ReadAsync(absolutePath);
    public Task WriteAbsoluteAsync(string absolutePath, string content) => _store.WriteAsync(absolutePath, content);
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
        string live = Resolve(OrchestrationArtifactPaths.Decisions);
        bool existed = await _store.ExistsAsync(live);
        if (existed) await _store.DeleteAsync(live);
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
        await _store.WriteAsync(Resolve(OrchestrationArtifactPaths.Decisions), decisions);
        PersistedExecutionRecommendation projection =
            ExecutionRecommendationContract.Bind(decisions, recommendation);
        await _store.WriteAsync(
            Resolve(OrchestrationArtifactPaths.ExecutionRecommendation),
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
        await _store.WriteAsync(
            Resolve(OrchestrationArtifactPaths.ExecutionRecommendation),
            ExecutionRecommendationContract.SerializePersisted(projection));
        return evidence;
    }

    public async Task InvalidateExecutionRecommendationProjectionAsync()
    {
        string path = Resolve(OrchestrationArtifactPaths.ExecutionRecommendation);
        if (await _store.ExistsAsync(path)) await _store.DeleteAsync(path);
    }

    public async Task EnsureOperationalContextAsync()
    {
        if (await ExistsAsync(OrchestrationArtifactPaths.OperationalContext)) return;
        string? plan = await ReadPlanAsync();
        if (plan is not null) await WriteAsync(OrchestrationArtifactPaths.OperationalContext, plan);
    }

    private string Resolve(string relativePath) => ArtifactPath.ResolveRepositoryPath(_repository, relativePath);

    private async Task<string?> RotateAsync(
        string liveRelative,
        LoopHistoryKind kind,
        CanonicalCausalContext causality,
        HistoryEvidenceAttachments? evidence)
    {
        string? content = await _store.ReadAsync(Resolve(liveRelative));
        if (content is null) return null;
        await _historyStore.AppendAsync(new LoopHistoryAppendRequest(kind, content, causality, evidence));
        await _store.DeleteAsync(Resolve(liveRelative));
        return content;
    }

    private async Task<(string? Content, string? RelativePath)> ReadLatestAsync(
        string liveRelative,
        LoopHistoryKind kind)
    {
        string? live = await _store.ReadAsync(Resolve(liveRelative));
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
