using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Repositories;
using LoopRelay.DecisionSessions.Abstractions;
using LoopRelay.DecisionSessions.Models;
using LoopRelay.DecisionSessions.Persistence;
using LoopRelay.DecisionSessions.Primitives;

namespace LoopRelay.DecisionSessions.Services;

public sealed class DecisionSessionContinuityArtifactService(
    IRepositoryService repositoryService,
    IDecisionSessionRepository sessionRepository,
    IDecisionSessionLifecyclePolicy lifecyclePolicy,
    IDecisionSessionMetricsService metricsService,
    IDecisionSessionEconomicsService economicsService,
    IDecisionSessionCoherenceService coherenceService,
    IDecisionSessionEvidenceReader evidenceReader,
    TimeProvider timeProvider) : IDecisionSessionContinuityArtifactService
{
    public async Task<DecisionSessionContinuityArtifact> CreateAsync(
        Guid repositoryId,
        DecisionSessionId sourceSessionId,
        DecisionSessionId? targetSessionId = null)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        IReadOnlyList<DecisionSession> sessions = await sessionRepository.ListAsync(repository);
        DecisionSession? activeSession = sessions.SingleOrDefault(session => session.State == DecisionSessionState.Active);
        if (activeSession is not null && activeSession.Id != sourceSessionId)
        {
            throw new DecisionSessionValidationException("Continuity artifact source session must match the active decision session when one exists.");
        }

        DecisionSession? sourceSession = sessions.FirstOrDefault(session => session.Id == sourceSessionId);
        if (sourceSession is null)
        {
            throw new KeyNotFoundException($"Decision session was not found: {sourceSessionId}");
        }

        if (sourceSession.State is not (DecisionSessionState.Active or DecisionSessionState.TransferPending))
        {
            throw new DecisionSessionValidationException("Continuity artifact source session must be active or transfer-pending.");
        }

        DateTimeOffset createdAt = timeProvider.GetUtcNow();
        DecisionSessionLifecycleSnapshot policySnapshot = sourceSession.State == DecisionSessionState.TransferPending
            ? await ReadRequiredPolicySnapshotAsync(repository, sourceSessionId)
            : await lifecyclePolicy.EvaluateAsync(repositoryId);
        DecisionSessionMetricsSnapshot metricsSnapshot = await metricsService.GetMetricsAsync(repositoryId);
        DecisionSessionEconomicsSnapshot economicsSnapshot = await economicsService.GetEconomicsAsync(repositoryId);
        DecisionSessionCoherenceSnapshot coherenceSnapshot = await coherenceService.GetCoherenceAsync(repositoryId);
        DecisionSessionEvidence evidence = await evidenceReader.ReadAsync(repository, sourceSession, createdAt);

        DecisionSessionContinuityReference[] decisionReferences = CreateReferences(
            evidence,
            "Decision",
            ["decisions", "decision-candidates", "decision-proposals"]);
        DecisionSessionContinuityReference[] reasoningReferences = CreateReferences(
            evidence,
            "Reasoning",
            ["reasoning-events", "reasoning-threads", "reasoning-relationships"]);
        DecisionSessionContinuityReference[] operationalContextReferences = CreateReferences(
            evidence,
            "OperationalContext",
            ["operational-context-proposals", "operational-context-artifacts"]);

        string artifactId = CreateArtifactId(createdAt, sourceSessionId);
        string fingerprint = CreateContinuityFingerprint(
            repository.Id,
            sourceSessionId,
            targetSessionId,
            policySnapshot.Evaluation,
            metricsSnapshot.Metrics,
            economicsSnapshot.Economics,
            coherenceSnapshot.Coherence,
            metricsSnapshot.Cache,
            decisionReferences,
            reasoningReferences,
            operationalContextReferences);
        var artifact = new DecisionSessionContinuityArtifact(
            artifactId,
            repository.Id,
            sourceSessionId,
            targetSessionId,
            createdAt,
            policySnapshot.Evaluation,
            metricsSnapshot.Metrics,
            economicsSnapshot.Economics,
            coherenceSnapshot.Coherence,
            metricsSnapshot.Cache,
            decisionReferences,
            reasoningReferences,
            operationalContextReferences,
            fingerprint,
            CreateDiagnostics(evidence));

        DecisionSessionContinuityArtifactValidation validation = Validate(artifact);
        if (!validation.IsValid)
        {
            throw new DecisionSessionValidationException(string.Join("; ", validation.Errors));
        }

        await sessionRepository.WriteContinuityArtifactAsync(repository, artifact);
        return artifact;
    }

    public async Task<IReadOnlyList<DecisionSessionContinuityArtifact>> ListAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await sessionRepository.ListContinuityArtifactsAsync(repository);
    }

    public async Task<DecisionSessionContinuityArtifact?> GetAsync(Guid repositoryId, string artifactId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await sessionRepository.ReadContinuityArtifactAsync(repository, artifactId);
    }

    public async Task<DecisionSessionContinuityArtifact> AttachTargetSessionAsync(
        Guid repositoryId,
        string artifactId,
        DecisionSessionId targetSessionId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionSessionContinuityArtifact artifact = await sessionRepository.ReadContinuityArtifactAsync(repository, artifactId)
            ?? throw new KeyNotFoundException($"Decision session continuity artifact was not found: {artifactId}");
        DecisionSessionContinuityArtifact updated = artifact with
        {
            TargetSessionId = targetSessionId,
            ContinuityFingerprint = CreateContinuityFingerprint(
                artifact.RepositoryId,
                artifact.SourceSessionId,
                targetSessionId,
                artifact.PolicyEvaluation,
                artifact.Metrics,
                artifact.Economics,
                artifact.Coherence,
                artifact.Cache,
                artifact.DecisionReferences,
                artifact.ReasoningReferences,
                artifact.OperationalContextReferences)
        };
        DecisionSessionContinuityArtifactValidation validation = Validate(updated);
        if (!validation.IsValid)
        {
            throw new DecisionSessionValidationException(string.Join("; ", validation.Errors));
        }

        await sessionRepository.WriteContinuityArtifactAsync(repository, updated);
        return updated;
    }

    public DecisionSessionContinuityArtifactValidation Validate(DecisionSessionContinuityArtifact artifact)
    {
        var errors = new List<string>();
        if (artifact.RepositoryId == Guid.Empty)
        {
            errors.Add("Repository id is required.");
        }

        if (artifact.SourceSessionId.Value == Guid.Empty)
        {
            errors.Add("Source session id is required.");
        }

        if (!string.Equals(artifact.ArtifactId, CreateArtifactId(artifact.CreatedAt, artifact.SourceSessionId), StringComparison.Ordinal))
        {
            errors.Add("Artifact id is not deterministic for the created timestamp and source session id.");
        }

        if (artifact.DecisionReferences.Count == 0 || artifact.DecisionReferences.Sum(reference => reference.ItemCount) <= 0)
        {
            errors.Add("At least one decision reference is required.");
        }

        if (artifact.ReasoningReferences.Count == 0 || artifact.ReasoningReferences.Sum(reference => reference.ItemCount) <= 0)
        {
            errors.Add("At least one reasoning reference is required.");
        }

        if (artifact.OperationalContextReferences.Count == 0 ||
            artifact.OperationalContextReferences.Sum(reference => reference.ItemCount) <= 0)
        {
            errors.Add("At least one operational context reference is required.");
        }

        string expectedFingerprint = CreateContinuityFingerprint(
            artifact.RepositoryId,
            artifact.SourceSessionId,
            artifact.TargetSessionId,
            artifact.PolicyEvaluation,
            artifact.Metrics,
            artifact.Economics,
            artifact.Coherence,
            artifact.Cache,
            artifact.DecisionReferences,
            artifact.ReasoningReferences,
            artifact.OperationalContextReferences);
        if (!string.Equals(artifact.ContinuityFingerprint, expectedFingerprint, StringComparison.Ordinal))
        {
            errors.Add("Continuity fingerprint does not match artifact content.");
        }

        return errors.Count == 0
            ? DecisionSessionContinuityArtifactValidation.Valid
            : new DecisionSessionContinuityArtifactValidation(false, errors, []);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<DecisionSessionLifecycleSnapshot> ReadRequiredPolicySnapshotAsync(
        Repository repository,
        DecisionSessionId sourceSessionId)
    {
        DecisionSessionLifecycleSnapshot? snapshot = await sessionRepository.ReadLifecyclePolicySnapshotAsync(repository);
        if (snapshot is null)
        {
            throw new DecisionSessionValidationException("A persisted transfer policy snapshot is required after source session is transfer-pending.");
        }

        if (snapshot.Diagnostics.Inputs.Session.Id != sourceSessionId)
        {
            throw new DecisionSessionValidationException("Persisted transfer policy snapshot source does not match the continuity artifact source.");
        }

        return snapshot;
    }

    private static DecisionSessionContinuityReference[] CreateReferences(
        DecisionSessionEvidence evidence,
        string referenceType,
        IReadOnlyList<string> sources)
    {
        return evidence.Sources
            .Where(source => sources.Contains(source.Source, StringComparer.Ordinal))
            .Select(source => new DecisionSessionContinuityReference(
                source.Source,
                referenceType,
                source.ItemCount,
                source.ByteCount,
                source.LastActivityAt,
                Sha256(source.SerializedContent)))
            .OrderBy(reference => reference.Source, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> CreateDiagnostics(DecisionSessionEvidence evidence)
    {
        return [
            "Continuity artifact records governance-continuity evidence for transfer.",
            "Decision Sessions produce and validate this payload; they do not own operational context.",
            $"Evidence items captured: {evidence.EvidenceItemCount}.",
            $"Operational context references captured: {evidence.OperationalContextRevisionCount}."
        ];
    }

    private static string CreateArtifactId(DateTimeOffset createdAt, DecisionSessionId sourceSessionId)
    {
        return $"continuity.{createdAt.UtcDateTime:yyyyMMddTHHmmss.fffffffZ}.{sourceSessionId}.json";
    }

    private static string CreateContinuityFingerprint(
        Guid repositoryId,
        DecisionSessionId sourceSessionId,
        DecisionSessionId? targetSessionId,
        DecisionSessionLifecycleEvaluation policyEvaluation,
        DecisionSessionMetrics metrics,
        DecisionSessionEconomics economics,
        DecisionSessionCoherence coherence,
        DecisionSessionCacheMetrics cache,
        IReadOnlyList<DecisionSessionContinuityReference> decisionReferences,
        IReadOnlyList<DecisionSessionContinuityReference> reasoningReferences,
        IReadOnlyList<DecisionSessionContinuityReference> operationalContextReferences)
    {
        var payload = new ContinuityFingerprintPayload(
            repositoryId,
            sourceSessionId,
            targetSessionId,
            policyEvaluation,
            metrics,
            economics,
            coherence,
            cache,
            decisionReferences.OrderBy(reference => reference.Source, StringComparer.Ordinal).ToArray(),
            reasoningReferences.OrderBy(reference => reference.Source, StringComparer.Ordinal).ToArray(),
            operationalContextReferences.OrderBy(reference => reference.Source, StringComparer.Ordinal).ToArray());
        return Sha256(JsonSerializer.Serialize(payload, DecisionSessionJson.Options));
    }

    private static string Sha256(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record ContinuityFingerprintPayload(
        Guid RepositoryId,
        DecisionSessionId SourceSessionId,
        DecisionSessionId? TargetSessionId,
        DecisionSessionLifecycleEvaluation PolicyEvaluation,
        DecisionSessionMetrics Metrics,
        DecisionSessionEconomics Economics,
        DecisionSessionCoherence Coherence,
        DecisionSessionCacheMetrics Cache,
        IReadOnlyList<DecisionSessionContinuityReference> DecisionReferences,
        IReadOnlyList<DecisionSessionContinuityReference> ReasoningReferences,
        IReadOnlyList<DecisionSessionContinuityReference> OperationalContextReferences);
}
