using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.Persistence.Sqlite.Abstractions;

namespace CommandCenter.DecisionSessions.Services;

public sealed class DecisionSessionMetricsService(
    IRepositoryService repositoryService,
    IDecisionSessionRegistry sessionRegistry,
    IDecisionSessionRepository sessionRepository,
    IDecisionSessionEvidenceReader evidenceReader,
    ITokenEstimator tokenEstimator,
    TimeProvider timeProvider,
    IDerivedSnapshotReader? derivedReader = null) : IDecisionSessionMetricsService
{
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(1);

    public async Task<DecisionSessionMetricsSnapshot> GetMetricsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);

        // The PURE base is cached behind the (repo, metrics-base) gate keyed by the source-content
        // fingerprint; the measuredAt-relative fields are recomputed on EVERY read from base + now.
        // No derived snapshot is written to .agents/decision-sessions/analysis anymore.
        if (derivedReader is not null)
        {
            return await derivedReader.ReadDerivedAsync(
                repository,
                DecisionSessionAnalysisCache.MetricsKind,
                DecisionSessionAnalysisCache.MetricsFamilies,
                DecisionSessionAnalysisCache.FormulaVersion,
                ct => ComputeBaseAsync(repository),
                Project,
                CancellationToken.None);
        }

        // Fallback (no cache wired, e.g. pure-service tests): compute the base then project, no persistence.
        DecisionSessionMetricsBase fallbackBase = await ComputeBaseAsync(repository);
        return Project(fallbackBase, timeProvider.GetUtcNow());
    }

    // SOURCE-PURE: counts/bytes/tokens + base timestamps {sessionStartedAt, lastActivityAt, createdAt}.
    // No measuredAt-relative field is computed here; nothing in this method depends on the clock.
    private async Task<DecisionSessionMetricsBase> ComputeBaseAsync(Repository repository)
    {
        DecisionSession? activeSession = await sessionRegistry.GetActiveSessionAsync(repository.Id);
        DateTimeOffset measuredAt = timeProvider.GetUtcNow();
        DecisionSessionEvidence evidence = await evidenceReader.ReadAsync(repository, activeSession, measuredAt);

        long totalBytes = evidence.Sources.Sum(size => size.ByteCount);
        long totalCharacters = evidence.Sources.Sum(size => size.CharacterCount);
        long estimatedTokens = tokenEstimator.EstimateTokenCount(string.Concat(evidence.Sources.Select(size => size.SerializedContent)));

        var sourceDiagnostics = evidence.Sources.Select(size => new DecisionSessionMetricsSourceDiagnostic(
            size.Source,
            size.ItemCount,
            size.ByteCount,
            size.CharacterCount,
            size.Notes)).ToArray();

        return new DecisionSessionMetricsBase(
            repository.Id,
            activeSession?.CreatedAt,
            evidence.SessionStartedAt,
            evidence.LastActivityAt,
            evidence.EvidenceItemCount,
            totalBytes,
            totalCharacters,
            estimatedTokens,
            evidence.ReasoningEventCount,
            evidence.ReasoningThreadCount,
            evidence.ReasoningRelationshipCount,
            evidence.DecisionCount,
            evidence.DecisionCandidateCount,
            evidence.DecisionProposalCount,
            evidence.OperationalContextRevisionCount,
            sourceDiagnostics,
            evidence.Warnings.ToArray());
    }

    // TIME-DEPENDENT: populates ALL measuredAt-relative fields from base + now. I/O-free.
    private static DecisionSessionMetricsSnapshot Project(DecisionSessionMetricsBase metricsBase, DateTimeOffset measuredAt)
    {
        TimeSpan sessionAge = metricsBase.CreatedAt is null ? TimeSpan.Zero : NonNegative(measuredAt - metricsBase.CreatedAt.Value);
        TimeSpan elapsed = NonNegative(measuredAt - metricsBase.SessionStartedAt);
        TimeSpan idle = NonNegative(measuredAt - metricsBase.LastActivityAt);
        decimal elapsedHours = Math.Max(1m, (decimal)elapsed.TotalHours);
        decimal activityRate = metricsBase.EvidenceItemCount / elapsedHours;
        decimal growthRate = metricsBase.ContextByteSize / elapsedHours;
        CacheRiskBreakdown cacheRisk = CalculateCacheMissRisk(elapsed, idle);
        DateTimeOffset? cacheExpiresAt = metricsBase.LastActivityAt + DefaultCacheTtl;

        var metrics = new DecisionSessionMetrics(
            metricsBase.EstimatedTokenCount,
            metricsBase.ContextByteSize,
            metricsBase.ReasoningEventCount,
            metricsBase.ReasoningThreadCount,
            metricsBase.ReasoningRelationshipCount,
            metricsBase.DecisionCount,
            metricsBase.DecisionCandidateCount,
            metricsBase.DecisionProposalCount,
            metricsBase.OperationalContextRevisionCount,
            metricsBase.LastActivityAt,
            measuredAt);
        var statistics = new DecisionSessionStatistics(sessionAge, elapsed, idle, growthRate, activityRate);
        var activity = new DecisionSessionActivity(metricsBase.EvidenceItemCount, metricsBase.LastActivityAt, idle, activityRate);
        var growth = new DecisionSessionGrowth(metricsBase.ContextByteSize, metricsBase.EstimatedTokenCount, elapsed, growthRate);
        var cache = new DecisionSessionCacheMetrics(DefaultCacheTtl, cacheRisk.Risk, cacheExpiresAt);
        var diagnostics = new DecisionSessionMetricsDiagnostics(
            metricsBase.RepositoryId,
            measuredAt,
            metricsBase.Sources,
            [
                $"Token count is estimated deterministically as (characterCount + 3) / 4 over {metricsBase.TotalCharacters} characters.",
                $"Cache TTL assumption is fixed at {DefaultCacheTtl.TotalMinutes:0} minutes for Stage 2A.",
                $"Cache risk uses elapsed contribution {cacheRisk.ElapsedContribution:0.0000} and idle contribution {cacheRisk.IdleContribution:0.0000}.",
                "Confidence is high for repository-backed evidence counts and medium for provider-independent token and cache estimates."
            ],
            metricsBase.Warnings);

        return new DecisionSessionMetricsSnapshot(metricsBase.RepositoryId, metrics, statistics, activity, growth, cache, diagnostics, measuredAt);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static TimeSpan NonNegative(TimeSpan duration)
    {
        return duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
    }

    private static CacheRiskBreakdown CalculateCacheMissRisk(TimeSpan elapsed, TimeSpan idle)
    {
        decimal elapsedPressure = Clamp((decimal)(elapsed.TotalMinutes / DefaultCacheTtl.TotalMinutes));
        decimal idlePressure = Clamp((decimal)(idle.TotalMinutes / DefaultCacheTtl.TotalMinutes));
        decimal elapsedContribution = elapsedPressure * 0.4m;
        decimal idleContribution = idlePressure * 0.6m;
        decimal risk = Clamp(decimal.Round(elapsedContribution + idleContribution, 4, MidpointRounding.AwayFromZero));
        return new CacheRiskBreakdown(risk, elapsedContribution, idleContribution);
    }

    private static decimal Clamp(decimal value)
    {
        if (value < 0m)
        {
            return 0m;
        }

        return value > 1m ? 1m : value;
    }

    private sealed record CacheRiskBreakdown(decimal Risk, decimal ElapsedContribution, decimal IdleContribution);
}
