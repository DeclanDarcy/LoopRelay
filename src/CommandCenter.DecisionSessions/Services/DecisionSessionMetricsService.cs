using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Services;

public sealed class DecisionSessionMetricsService(
    IRepositoryService repositoryService,
    IDecisionSessionRegistry sessionRegistry,
    IDecisionSessionRepository sessionRepository,
    IDecisionSessionEvidenceReader evidenceReader,
    ITokenEstimator tokenEstimator,
    TimeProvider timeProvider) : IDecisionSessionMetricsService
{
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(1);

    public async Task<DecisionSessionMetricsSnapshot> GetMetricsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionSession? activeSession = await sessionRegistry.GetActiveSessionAsync(repositoryId);
        DateTimeOffset measuredAt = timeProvider.GetUtcNow();
        var rebuildWarnings = new List<string>();

        try
        {
            _ = await sessionRepository.ReadMetricsSnapshotAsync(repository);
        }
        catch (DecisionSessionValidationException exception)
        {
            rebuildWarnings.Add($"Existing metrics snapshot is invalid and was rebuilt: {exception.Message}");
        }
        catch (JsonException exception)
        {
            rebuildWarnings.Add($"Existing metrics snapshot JSON is invalid and was rebuilt: {exception.Message}");
        }

        DecisionSessionEvidence evidence = await evidenceReader.ReadAsync(repository, activeSession, measuredAt);
        DecisionSessionMetricsSnapshot snapshot = BuildSnapshot(repository.Id, activeSession, measuredAt, evidence, rebuildWarnings);
        await sessionRepository.WriteMetricsSnapshotAsync(repository, snapshot);
        return snapshot;
    }

    private DecisionSessionMetricsSnapshot BuildSnapshot(
        Guid repositoryId,
        DecisionSession? activeSession,
        DateTimeOffset measuredAt,
        DecisionSessionEvidence evidence,
        IReadOnlyList<string> rebuildWarnings)
    {
        long totalBytes = evidence.Sources.Sum(size => size.ByteCount);
        long totalCharacters = evidence.Sources.Sum(size => size.CharacterCount);
        long estimatedTokens = tokenEstimator.EstimateTokenCount(string.Concat(evidence.Sources.Select(size => size.SerializedContent)));
        TimeSpan sessionAge = activeSession is null ? TimeSpan.Zero : NonNegative(measuredAt - activeSession.CreatedAt);
        TimeSpan elapsed = NonNegative(measuredAt - evidence.SessionStartedAt);
        TimeSpan idle = NonNegative(measuredAt - evidence.LastActivityAt);
        decimal elapsedHours = Math.Max(1m, (decimal)elapsed.TotalHours);
        decimal activityRate = evidence.EvidenceItemCount / elapsedHours;
        decimal growthRate = totalBytes / elapsedHours;
        CacheRiskBreakdown cacheRisk = CalculateCacheMissRisk(elapsed, idle);
        DateTimeOffset? cacheExpiresAt = evidence.LastActivityAt + DefaultCacheTtl;

        var metrics = new DecisionSessionMetrics(
            estimatedTokens,
            totalBytes,
            evidence.ReasoningEventCount,
            evidence.ReasoningThreadCount,
            evidence.ReasoningRelationshipCount,
            evidence.DecisionCount,
            evidence.DecisionCandidateCount,
            evidence.DecisionProposalCount,
            evidence.OperationalContextRevisionCount,
            evidence.LastActivityAt,
            measuredAt);
        var statistics = new DecisionSessionStatistics(sessionAge, elapsed, idle, growthRate, activityRate);
        var activity = new DecisionSessionActivity(evidence.EvidenceItemCount, evidence.LastActivityAt, idle, activityRate);
        var growth = new DecisionSessionGrowth(totalBytes, estimatedTokens, elapsed, growthRate);
        var cache = new DecisionSessionCacheMetrics(DefaultCacheTtl, cacheRisk.Risk, cacheExpiresAt);
        var diagnostics = new DecisionSessionMetricsDiagnostics(
            repositoryId,
            measuredAt,
            evidence.Sources.Select(size => new DecisionSessionMetricsSourceDiagnostic(
                size.Source,
                size.ItemCount,
                size.ByteCount,
                size.CharacterCount,
                size.Notes)).ToArray(),
            [
                $"Token count is estimated deterministically as (characterCount + 3) / 4 over {totalCharacters} characters.",
                $"Cache TTL assumption is fixed at {DefaultCacheTtl.TotalMinutes:0} minutes for Stage 2A.",
                $"Cache risk uses elapsed contribution {cacheRisk.ElapsedContribution:0.0000} and idle contribution {cacheRisk.IdleContribution:0.0000}.",
                "Confidence is high for repository-backed evidence counts and medium for provider-independent token and cache estimates."
            ],
            evidence.Warnings.Concat(rebuildWarnings).ToArray());

        return new DecisionSessionMetricsSnapshot(repositoryId, metrics, statistics, activity, growth, cache, diagnostics, measuredAt);
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
