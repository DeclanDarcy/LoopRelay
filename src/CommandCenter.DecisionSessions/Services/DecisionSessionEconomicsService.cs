using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;

namespace CommandCenter.DecisionSessions.Services;

public sealed class DecisionSessionEconomicsService(
    IRepositoryService repositoryService,
    IDecisionSessionRepository sessionRepository,
    IDecisionSessionMetricsService metricsService,
    DecisionSessionEconomicsOptions options,
    TimeProvider timeProvider) : IDecisionSessionEconomicsService
{
    public async Task<DecisionSessionEconomicsSnapshot> GetEconomicsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DateTimeOffset generatedAt = timeProvider.GetUtcNow();
        var rebuildWarnings = new List<string>();

        try
        {
            _ = await sessionRepository.ReadEconomicsSnapshotAsync(repository);
        }
        catch (DecisionSessionValidationException exception)
        {
            rebuildWarnings.Add($"Existing economics snapshot is invalid and was rebuilt: {exception.Message}");
        }
        catch (JsonException exception)
        {
            rebuildWarnings.Add($"Existing economics snapshot JSON is invalid and was rebuilt: {exception.Message}");
        }

        DecisionSessionMetricsSnapshot metricsSnapshot = await metricsService.GetMetricsAsync(repositoryId);
        DecisionSessionEconomicsSnapshot economicsSnapshot = BuildSnapshot(repository.Id, generatedAt, metricsSnapshot, rebuildWarnings);
        await sessionRepository.WriteEconomicsSnapshotAsync(repository, economicsSnapshot);
        return economicsSnapshot;
    }

    private DecisionSessionEconomicsSnapshot BuildSnapshot(
        Guid repositoryId,
        DateTimeOffset generatedAt,
        DecisionSessionMetricsSnapshot metricsSnapshot,
        IReadOnlyList<string> rebuildWarnings)
    {
        DecisionSessionEconomicsInputs inputs = new(
            metricsSnapshot.Metrics,
            metricsSnapshot.Statistics,
            metricsSnapshot.Activity,
            metricsSnapshot.Growth,
            metricsSnapshot.Cache);

        decimal contextCost = Average(
            Normalize(metricsSnapshot.Metrics.EstimatedTokenCount, options.LargeContextTokenThreshold),
            Normalize(metricsSnapshot.Metrics.ContextByteSize, options.LargeContextByteThreshold));
        decimal reasoningCost = Average(
            Normalize(metricsSnapshot.Metrics.ReasoningEventCount, options.ReasoningEventThreshold),
            Normalize(metricsSnapshot.Metrics.ReasoningThreadCount, options.ReasoningThreadThreshold),
            Normalize(metricsSnapshot.Metrics.ReasoningRelationshipCount, options.ReasoningRelationshipThreshold));
        ContinuityBenefitAssessment continuityBenefit = CalculateContinuityBenefit(metricsSnapshot);
        CacheRiskAssessment cacheRisk = new(
            metricsSnapshot.Cache.EstimatedCacheMissRisk,
            metricsSnapshot.Cache.EstimatedCacheTtl,
            metricsSnapshot.Cache.EstimatedCacheExpiresAt);
        CacheBenefitAssessment cacheBenefit = CalculateCacheBenefit(metricsSnapshot, cacheRisk);
        TransferValueAssessment transferValue = CalculateTransferValue(metricsSnapshot, contextCost, cacheRisk.Value);
        ReuseValueAssessment reuseValue = CalculateReuseValue(metricsSnapshot, continuityBenefit.Value, cacheBenefit.Value);

        var economics = new DecisionSessionEconomics(
            reuseValue.Value,
            transferValue.Value,
            contextCost,
            reasoningCost,
            continuityBenefit.Value,
            cacheBenefit.Value,
            cacheRisk.Value);
        var diagnostics = new DecisionSessionEconomicsDiagnostics(
            repositoryId,
            generatedAt,
            inputs,
            reuseValue,
            transferValue,
            cacheBenefit,
            cacheRisk,
            continuityBenefit,
            [
                "Economics is deterministic analysis and does not make lifecycle decisions.",
                $"Context cost normalizes tokens against {options.LargeContextTokenThreshold} and bytes against {options.LargeContextByteThreshold}.",
                $"Reasoning cost normalizes events, threads, and relationships against {options.ReasoningEventThreshold}, {options.ReasoningThreadThreshold}, and {options.ReasoningRelationshipThreshold}.",
                $"Cache benefit assumes cached-token cost factor {options.CachedTokenCostFactor:0.00}.",
                $"Reuse value uses assumed coherence score {options.AssumedCoherenceScore:0.00} until Stage 2C coherence exists."
            ],
            metricsSnapshot.Diagnostics.Warnings.Concat(rebuildWarnings).ToArray());

        return new DecisionSessionEconomicsSnapshot(repositoryId, economics, diagnostics, generatedAt);
    }

    private ContinuityBenefitAssessment CalculateContinuityBenefit(DecisionSessionMetricsSnapshot snapshot)
    {
        decimal decisionContribution = Normalize(
            snapshot.Metrics.DecisionCount + snapshot.Metrics.DecisionCandidateCount + snapshot.Metrics.DecisionProposalCount,
            options.DecisionThreshold);
        decimal reasoningNodes = Math.Max(1m, snapshot.Metrics.ReasoningEventCount + snapshot.Metrics.ReasoningThreadCount);
        decimal reasoningDensityContribution = Clamp(snapshot.Metrics.ReasoningRelationshipCount / reasoningNodes);
        decimal operationalContextContribution = Normalize(
            snapshot.Metrics.OperationalContextRevisionCount,
            options.OperationalContextRevisionThreshold);
        decimal value = Clamp(Round(
            (decisionContribution * 0.45m) +
            (reasoningDensityContribution * 0.30m) +
            (operationalContextContribution * 0.25m)));
        return new ContinuityBenefitAssessment(
            value,
            Round(decisionContribution),
            Round(reasoningDensityContribution),
            Round(operationalContextContribution));
    }

    private CacheBenefitAssessment CalculateCacheBenefit(DecisionSessionMetricsSnapshot snapshot, CacheRiskAssessment cacheRisk)
    {
        decimal reusableCorpusScore = Average(
            Normalize(snapshot.Metrics.EstimatedTokenCount, options.LargeContextTokenThreshold),
            Normalize(snapshot.Metrics.ContextByteSize, options.LargeContextByteThreshold));
        decimal cacheRiskPenalty = 1m - cacheRisk.Value;
        decimal value = Clamp(Round(reusableCorpusScore * (1m - options.CachedTokenCostFactor) * cacheRiskPenalty));
        return new CacheBenefitAssessment(value, Round(reusableCorpusScore), options.CachedTokenCostFactor, Round(cacheRiskPenalty));
    }

    private TransferValueAssessment CalculateTransferValue(DecisionSessionMetricsSnapshot snapshot, decimal contextCost, decimal cacheRisk)
    {
        decimal growthContribution = Normalize(snapshot.Statistics.GrowthRate, options.LargeContextByteThreshold / 24m);
        decimal idleContribution = Clamp((decimal)(snapshot.Statistics.IdleDuration.TotalMinutes / snapshot.Cache.EstimatedCacheTtl.TotalMinutes));
        decimal value = Clamp(Round(
            (growthContribution * 0.25m) +
            (idleContribution * 0.25m) +
            (cacheRisk * 0.30m) +
            (contextCost * 0.20m)));
        return new TransferValueAssessment(value, Round(growthContribution), Round(idleContribution), Round(cacheRisk), Round(contextCost));
    }

    private ReuseValueAssessment CalculateReuseValue(
        DecisionSessionMetricsSnapshot snapshot,
        decimal continuityBenefit,
        decimal cacheBenefit)
    {
        decimal activityContribution = Clamp(1m - snapshot.Cache.EstimatedCacheMissRisk);
        decimal coherenceContribution = Clamp(options.AssumedCoherenceScore);
        decimal value = Clamp(Round(
            (continuityBenefit * 0.35m) +
            (cacheBenefit * 0.30m) +
            (coherenceContribution * 0.20m) +
            (activityContribution * 0.15m)));
        return new ReuseValueAssessment(
            value,
            Round(continuityBenefit),
            Round(cacheBenefit),
            Round(coherenceContribution),
            Round(activityContribution));
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static decimal Normalize(decimal value, decimal threshold)
    {
        if (threshold <= 0m)
        {
            return 0m;
        }

        return Clamp(value / threshold);
    }

    private static decimal Average(params decimal[] values)
    {
        return values.Length == 0 ? 0m : Round(values.Average());
    }

    private static decimal Clamp(decimal value)
    {
        if (value < 0m)
        {
            return 0m;
        }

        return value > 1m ? 1m : value;
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 4, MidpointRounding.AwayFromZero);
    }
}
