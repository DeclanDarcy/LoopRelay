using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Services;

public sealed class DecisionSessionObservabilityService(
    IRepositoryService repositoryService,
    IDecisionSessionRepository sessionRepository,
    TimeProvider timeProvider) : IDecisionSessionObservabilityService
{
    public async Task<DecisionSessionLifecycleProjection> GetProjectionAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DateTimeOffset generatedAt = timeProvider.GetUtcNow();
        List<string> errors = [];
        List<string> warnings = [];
        IReadOnlyList<DecisionSession> sessions = await ReadSessionsAsync(repository, errors);
        DecisionSessionProjection[] sessionProjections = sessions
            .Select(DecisionSessionProjection.FromSession)
            .ToArray();
        DecisionSessionProjection? activeSession = sessionProjections.SingleOrDefault(session => session.State == DecisionSessionState.Active);
        IReadOnlyList<DecisionSessionTransfer> transfers = await ReadAsync(
            () => sessionRepository.ListTransfersAsync(repository),
            "Decision session transfers",
            warnings);
        IReadOnlyList<DecisionSessionContinuityArtifact> artifacts = await ReadAsync(
            () => sessionRepository.ListContinuityArtifactsAsync(repository),
            "Decision session continuity artifacts",
            warnings);
        IReadOnlyList<DecisionSessionRecoveryResult> recoveryResults = await ReadAsync(
            () => sessionRepository.ListRecoveryResultsAsync(repository),
            "Decision session recovery results",
            warnings);
        var diagnostics = new DecisionSessionDiagnostics(
            repositoryId,
            errors.Count == 0,
            sessions.Count,
            sessions.Count(session => session.State == DecisionSessionState.Active),
            errors,
            warnings,
            generatedAt);

        return new DecisionSessionLifecycleProjection(
            repositoryId,
            activeSession,
            sessionProjections,
            await ReadNullableAsync(() => sessionRepository.ReadMetricsSnapshotAsync(repository), "Decision session metrics snapshot", warnings),
            await ReadNullableAsync(() => sessionRepository.ReadEconomicsSnapshotAsync(repository), "Decision session economics snapshot", warnings),
            await ReadNullableAsync(() => sessionRepository.ReadCoherenceSnapshotAsync(repository), "Decision session coherence snapshot", warnings),
            await ReadNullableAsync(() => sessionRepository.ReadLifecyclePolicySnapshotAsync(repository), "Decision session lifecycle policy snapshot", warnings),
            await ReadNullableAsync(() => sessionRepository.ReadTransferEligibilitySnapshotAsync(repository), "Decision session transfer eligibility snapshot", warnings),
            ResolveCurrentArtifact(sessions, transfers, artifacts),
            transfers.OrderByDescending(transfer => transfer.StartedAt).Take(10).ToArray(),
            transfers
                .SelectMany(transfer => transfer.Events)
                .OrderByDescending(transferEvent => transferEvent.OccurredAt)
                .ThenBy(transferEvent => transferEvent.EventId, StringComparer.Ordinal)
                .Take(25)
                .ToArray(),
            recoveryResults.OrderByDescending(result => result.RecoveredAt).Take(10).ToArray(),
            diagnostics,
            generatedAt);
    }

    public async Task<DecisionSessionLifecycleHistory> GetHistoryAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        List<string> warnings = [];
        IReadOnlyList<DecisionSession> sessions = await ReadSessionsAsync(repository, warnings);
        DecisionSessionMetricsSnapshot? metrics = await ReadNullableAsync(() => sessionRepository.ReadMetricsSnapshotAsync(repository), "Decision session metrics snapshot", warnings);
        DecisionSessionEconomicsSnapshot? economics = await ReadNullableAsync(() => sessionRepository.ReadEconomicsSnapshotAsync(repository), "Decision session economics snapshot", warnings);
        DecisionSessionCoherenceSnapshot? coherence = await ReadNullableAsync(() => sessionRepository.ReadCoherenceSnapshotAsync(repository), "Decision session coherence snapshot", warnings);
        DecisionSessionLifecycleSnapshot? policy = await ReadNullableAsync(() => sessionRepository.ReadLifecyclePolicySnapshotAsync(repository), "Decision session lifecycle policy snapshot", warnings);
        DecisionSessionTransferEligibilitySnapshot? eligibility = await ReadNullableAsync(() => sessionRepository.ReadTransferEligibilitySnapshotAsync(repository), "Decision session transfer eligibility snapshot", warnings);
        IReadOnlyList<DecisionSessionContinuityArtifact> artifacts = await ReadAsync(
            () => sessionRepository.ListContinuityArtifactsAsync(repository),
            "Decision session continuity artifacts",
            warnings);
        IReadOnlyList<DecisionSessionTransfer> transfers = await ReadAsync(
            () => sessionRepository.ListTransfersAsync(repository),
            "Decision session transfers",
            warnings);
        IReadOnlyList<DecisionSessionRecoveryResult> recoveryResults = await ReadAsync(
            () => sessionRepository.ListRecoveryResultsAsync(repository),
            "Decision session recovery results",
            warnings);

        var events = new List<DecisionSessionLifecycleHistoryEvent>();
        foreach (DecisionSession session in sessions)
        {
            events.Add(new DecisionSessionLifecycleHistoryEvent(
                DecisionSessionLifecycleHistoryEventType.Created,
                session.CreatedAt,
                session.Id,
                null,
                null,
                null,
                null,
                "Decision session was created.",
                []));
            if (session.ActivatedAt is not null)
            {
                events.Add(new DecisionSessionLifecycleHistoryEvent(
                    DecisionSessionLifecycleHistoryEventType.Activated,
                    session.ActivatedAt.Value,
                    session.Id,
                    null,
                    null,
                    null,
                    null,
                    "Decision session was activated.",
                    []));
            }

            if (session.RetiredAt is not null)
            {
                events.Add(new DecisionSessionLifecycleHistoryEvent(
                    DecisionSessionLifecycleHistoryEventType.Retired,
                    session.RetiredAt.Value,
                    session.Id,
                    session.Metadata.TransferredToSessionId,
                    null,
                    null,
                    null,
                    "Decision session was retired.",
                    FilterNull([session.Metadata.TransferReason])));
            }
        }

        AddAnalysisEvents(events, metrics, economics, coherence);
        if (policy is not null)
        {
            events.Add(new DecisionSessionLifecycleHistoryEvent(
                DecisionSessionLifecycleHistoryEventType.PolicyEvaluated,
                policy.Evaluation.EvaluatedAt,
                policy.Diagnostics.Inputs.Session.Id,
                null,
                null,
                null,
                null,
                policy.Evaluation.Reason,
                policy.Evaluation.ContributingFactors));
        }

        if (eligibility is not null)
        {
            events.Add(new DecisionSessionLifecycleHistoryEvent(
                DecisionSessionLifecycleHistoryEventType.TransferEligibilityEvaluated,
                eligibility.Eligibility.CheckedAt,
                eligibility.Eligibility.SourceSessionId,
                null,
                null,
                null,
                null,
                $"Transfer eligibility evaluated as {eligibility.Eligibility.Status}.",
                eligibility.Eligibility.Findings.Select(finding => $"{finding.Severity}: {finding.Message}").ToArray()));
        }

        foreach (DecisionSessionContinuityArtifact artifact in artifacts)
        {
            events.Add(new DecisionSessionLifecycleHistoryEvent(
                DecisionSessionLifecycleHistoryEventType.ContinuityArtifactCreated,
                artifact.CreatedAt,
                artifact.SourceSessionId,
                artifact.TargetSessionId,
                artifact.ArtifactId,
                null,
                null,
                "Decision session continuity artifact was created.",
                artifact.Diagnostics));
        }

        foreach (DecisionSessionTransfer transfer in transfers)
        {
            AddTransferEvents(events, transfer);
            if (transfer.Succeeded && transfer.TargetSessionId is not null)
            {
                events.Add(new DecisionSessionLifecycleHistoryEvent(
                    DecisionSessionLifecycleHistoryEventType.ReplacementCreated,
                    transfer.CompletedAt ?? transfer.StartedAt,
                    transfer.TargetSessionId,
                    transfer.SourceSessionId,
                    transfer.ContinuityArtifactId,
                    transfer.TransferId,
                    null,
                    "Decision session transfer replacement was created and activated.",
                    transfer.Diagnostics));
            }
        }

        foreach (DecisionSessionRecoveryResult recovery in recoveryResults)
        {
            events.Add(new DecisionSessionLifecycleHistoryEvent(
                DecisionSessionLifecycleHistoryEventType.Recovered,
                recovery.RecoveredAt,
                recovery.ActiveSessionId,
                null,
                null,
                null,
                recovery.RecoveryId,
                recovery.Succeeded
                    ? "Decision session recovery completed."
                    : "Decision session recovery found errors.",
                recovery.Findings.Select(finding => $"{finding.Severity}: {finding.Message}").ToArray()));
        }

        if (warnings.Count > 0)
        {
            events.Add(new DecisionSessionLifecycleHistoryEvent(
                DecisionSessionLifecycleHistoryEventType.Recovered,
                timeProvider.GetUtcNow(),
                null,
                null,
                null,
                null,
                null,
                "Decision session lifecycle history has incomplete evidence.",
                warnings));
        }

        return new DecisionSessionLifecycleHistory(
            repositoryId,
            events
                .OrderBy(lifecycleEvent => lifecycleEvent.OccurredAt)
                .ThenBy(lifecycleEvent => lifecycleEvent.EventType)
                .ThenBy(lifecycleEvent => lifecycleEvent.SessionId?.Value)
                .ThenBy(lifecycleEvent => lifecycleEvent.TransferId, StringComparer.Ordinal)
                .ThenBy(lifecycleEvent => lifecycleEvent.RecoveryId, StringComparer.Ordinal)
                .ToArray(),
            timeProvider.GetUtcNow());
    }

    public async Task<DecisionSessionInfluenceTrace> GetInfluenceTraceAsync(Guid repositoryId)
    {
        DecisionSessionLifecycleProjection projection = await GetProjectionAsync(repositoryId);
        var signals = new List<DecisionSessionInfluenceSignal>();

        if (projection.Metrics is not null)
        {
            AddSignal(
                signals,
                "Metrics",
                "Evidence size",
                null,
                $"{projection.Metrics.Metrics.EstimatedTokenCount} tokens, {projection.Metrics.Metrics.ContextByteSize} bytes",
                "Current decision-session evidence size used by lifecycle analysis.",
                projection.Metrics.Diagnostics.Warnings);
            AddSignal(
                signals,
                "Cache TTL",
                "Estimated cache TTL",
                null,
                projection.Metrics.Cache.EstimatedCacheTtl.ToString(),
                "Estimated remaining cache lifetime for reusable session context.",
                projection.Metrics.Diagnostics.Assumptions);
            AddSignal(
                signals,
                "Cache miss risk",
                "Estimated cache miss risk",
                projection.Metrics.Cache.EstimatedCacheMissRisk,
                projection.Metrics.Cache.EstimatedCacheExpiresAt is null
                    ? "No cache expiry estimate"
                    : $"Expires at {projection.Metrics.Cache.EstimatedCacheExpiresAt.Value:O}",
                "Estimated risk that cached decision-session context will no longer be reusable.",
                projection.Metrics.Diagnostics.Warnings);
        }

        if (projection.Economics is not null)
        {
            AddSignal(
                signals,
                "Economics",
                "Reuse value",
                projection.Economics.Economics.EstimatedReuseValue,
                $"cache benefit {projection.Economics.Economics.EstimatedCacheBenefit}, continuity benefit {projection.Economics.Economics.EstimatedContinuityBenefit}",
                "Estimated value of continuing the active decision session.",
                projection.Economics.Diagnostics.Assumptions);
            AddSignal(
                signals,
                "Economics",
                "Transfer value",
                projection.Economics.Economics.EstimatedTransferValue,
                $"context cost {projection.Economics.Economics.EstimatedContextCost}, reasoning cost {projection.Economics.Economics.EstimatedReasoningCost}",
                "Estimated value of transferring continuity to a replacement decision session.",
                projection.Economics.Diagnostics.Warnings);
        }

        if (projection.Coherence is not null)
        {
            AddSignal(
                signals,
                "Coherence",
                "Coherence score",
                projection.Coherence.Coherence.CoherenceScore,
                $"fragmentation {projection.Coherence.Coherence.FragmentationScore}, density {projection.Coherence.Coherence.DensityScore}",
                "Current reasoning and governance coherence used by lifecycle policy.",
                projection.Coherence.Diagnostics.Assumptions);
            AddSignal(
                signals,
                "Coherence",
                "Transfer pressure",
                projection.Coherence.Coherence.TransferPressure,
                $"continuity {projection.Coherence.Coherence.ContinuityScore}",
                "Coherence-derived pressure toward continuity transfer.",
                projection.Coherence.Diagnostics.Warnings);
        }

        if (projection.Policy is not null)
        {
            AddSignal(
                signals,
                "Policy",
                "Lifecycle decision",
                projection.Policy.Evaluation.TransferScore,
                $"{projection.Policy.Evaluation.Decision}: reuse {projection.Policy.Evaluation.ReuseScore}, transfer {projection.Policy.Evaluation.TransferScore}",
                projection.Policy.Evaluation.Reason,
                projection.Policy.Evaluation.ContributingFactors);
        }

        if (projection.TransferEligibility is not null)
        {
            AddSignal(
                signals,
                "Eligibility",
                "Transfer eligibility",
                null,
                projection.TransferEligibility.Eligibility.Status.ToString(),
                "Operational readiness for executing a policy-directed transfer.",
                projection.TransferEligibility.Eligibility.Findings.Select(finding => $"{finding.Severity}: {finding.Message}"));
        }

        if (projection.CurrentContinuityArtifact is not null)
        {
            AddSignal(
                signals,
                "Continuity artifact",
                "Current artifact",
                null,
                projection.CurrentContinuityArtifact.ArtifactId,
                "Continuity artifact currently associated with pending or recent transfer evidence.",
                projection.CurrentContinuityArtifact.Diagnostics);
        }

        foreach (DecisionSessionTransfer transfer in projection.RecentTransfers.Take(3))
        {
            AddSignal(
                signals,
                "Transfer",
                transfer.TransferId,
                null,
                transfer.Succeeded ? "Succeeded" : "Incomplete",
                $"Transfer from {transfer.SourceSessionId} to {transfer.TargetSessionId?.ToString() ?? "none"}.",
                transfer.Diagnostics);
        }

        foreach (DecisionSessionRecoveryResult recovery in projection.RecentRecoveryResults.Take(3))
        {
            AddSignal(
                signals,
                "Recovery",
                recovery.RecoveryId,
                null,
                recovery.Succeeded ? "Succeeded" : "Failed",
                "Recent lifecycle recovery evidence.",
                recovery.Findings.Select(finding => $"{finding.Severity}: {finding.Message}"));
        }

        return new DecisionSessionInfluenceTrace(
            repositoryId,
            projection.ActiveSession?.Id,
            projection.Policy?.Evaluation.Decision,
            projection.TransferEligibility?.Eligibility.Status,
            signals,
            projection.Diagnostics.Errors.Concat(projection.Diagnostics.Warnings).ToArray(),
            timeProvider.GetUtcNow());
    }

    private static void AddAnalysisEvents(
        List<DecisionSessionLifecycleHistoryEvent> events,
        DecisionSessionMetricsSnapshot? metrics,
        DecisionSessionEconomicsSnapshot? economics,
        DecisionSessionCoherenceSnapshot? coherence)
    {
        if (metrics is not null)
        {
            events.Add(new DecisionSessionLifecycleHistoryEvent(
                DecisionSessionLifecycleHistoryEventType.AnalysisCaptured,
                metrics.GeneratedAt,
                null,
                null,
                null,
                null,
                null,
                "Decision session metrics analysis was captured.",
                metrics.Diagnostics.Warnings));
        }

        if (economics is not null)
        {
            events.Add(new DecisionSessionLifecycleHistoryEvent(
                DecisionSessionLifecycleHistoryEventType.AnalysisCaptured,
                economics.GeneratedAt,
                null,
                null,
                null,
                null,
                null,
                "Decision session economics analysis was captured.",
                economics.Diagnostics.Warnings));
        }

        if (coherence is not null)
        {
            events.Add(new DecisionSessionLifecycleHistoryEvent(
                DecisionSessionLifecycleHistoryEventType.AnalysisCaptured,
                coherence.GeneratedAt,
                null,
                null,
                null,
                null,
                null,
                "Decision session coherence analysis was captured.",
                coherence.Diagnostics.Warnings));
        }
    }

    private static void AddTransferEvents(List<DecisionSessionLifecycleHistoryEvent> events, DecisionSessionTransfer transfer)
    {
        foreach (DecisionSessionTransferEvent transferEvent in transfer.Events)
        {
            DecisionSessionLifecycleHistoryEventType eventType = transferEvent.EventType switch
            {
                DecisionSessionTransferEventType.Started => DecisionSessionLifecycleHistoryEventType.TransferStarted,
                DecisionSessionTransferEventType.Completed => DecisionSessionLifecycleHistoryEventType.TransferCompleted,
                DecisionSessionTransferEventType.Failed => DecisionSessionLifecycleHistoryEventType.TransferStarted,
                _ => DecisionSessionLifecycleHistoryEventType.TransferStarted
            };
            events.Add(new DecisionSessionLifecycleHistoryEvent(
                eventType,
                transferEvent.OccurredAt,
                transferEvent.SourceSessionId,
                transferEvent.TargetSessionId,
                transferEvent.ContinuityArtifactId,
                transfer.TransferId,
                null,
                transferEvent.Message,
                transferEvent.Diagnostics));
        }
    }

    private static void AddSignal(
        List<DecisionSessionInfluenceSignal> signals,
        string category,
        string name,
        decimal? score,
        string value,
        string description,
        IEnumerable<string> contributingFactors)
    {
        signals.Add(new DecisionSessionInfluenceSignal(
            category,
            name,
            score,
            value,
            description,
            contributingFactors
                .Where(factor => !string.IsNullOrWhiteSpace(factor))
                .Distinct(StringComparer.Ordinal)
                .ToArray()));
    }

    private static DecisionSessionContinuityArtifact? ResolveCurrentArtifact(
        IReadOnlyList<DecisionSession> sessions,
        IReadOnlyList<DecisionSessionTransfer> transfers,
        IReadOnlyList<DecisionSessionContinuityArtifact> artifacts)
    {
        DecisionSessionTransfer? latestTransfer = transfers
            .OrderByDescending(transfer => transfer.StartedAt)
            .FirstOrDefault();
        if (latestTransfer?.ContinuityArtifactId is not null)
        {
            return artifacts.FirstOrDefault(artifact =>
                string.Equals(artifact.ArtifactId, latestTransfer.ContinuityArtifactId, StringComparison.Ordinal));
        }

        DecisionSession? transferSession = sessions
            .Where(session => session.State is DecisionSessionState.TransferPending or DecisionSessionState.Transferred)
            .OrderByDescending(session => session.Metadata.UpdatedAt ?? session.RetiredAt ?? session.ActivatedAt ?? session.CreatedAt)
            .FirstOrDefault();
        return transferSession is null
            ? null
            : artifacts
                .Where(artifact => artifact.SourceSessionId == transferSession.Id)
                .OrderByDescending(artifact => artifact.CreatedAt)
                .FirstOrDefault();
    }

    private async Task<IReadOnlyList<DecisionSession>> ReadSessionsAsync(Repository repository, List<string> errors)
    {
        try
        {
            return await sessionRepository.ListAsync(repository);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or JsonException)
        {
            errors.Add($"Decision session registry could not be read: {exception.Message}");
            return [];
        }
    }

    private static async Task<T?> ReadNullableAsync<T>(Func<Task<T?>> read, string evidenceName, List<string> warnings)
    {
        try
        {
            return await read();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or JsonException)
        {
            warnings.Add($"{evidenceName} could not be read: {exception.Message}");
            return default;
        }
    }

    private static async Task<IReadOnlyList<T>> ReadAsync<T>(Func<Task<IReadOnlyList<T>>> read, string evidenceName, List<string> warnings)
    {
        try
        {
            return await read();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or JsonException)
        {
            warnings.Add($"{evidenceName} could not be read: {exception.Message}");
            return [];
        }
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static IReadOnlyList<string> FilterNull(IEnumerable<string?> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }
}
