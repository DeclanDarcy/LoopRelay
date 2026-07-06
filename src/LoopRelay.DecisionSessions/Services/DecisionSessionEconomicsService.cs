using LoopRelay.Core.Repositories;
using LoopRelay.DecisionSessions.Abstractions;
using LoopRelay.DecisionSessions.Models;
using LoopRelay.Persistence.Sqlite.Abstractions;

namespace LoopRelay.DecisionSessions.Services;

public sealed class DecisionSessionEconomicsService(
    IRepositoryService repositoryService,
    IDecisionSessionRepository sessionRepository,
    IDecisionSessionMetricsService metricsService,
    DecisionSessionEconomicsOptions options,
    TimeProvider timeProvider,
    IDerivedSnapshotReader? derivedReader = null) : IDecisionSessionEconomicsService
{
    public async Task<DecisionSessionEconomicsSnapshot> GetEconomicsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);

        // The metrics snapshot is itself active compute-on-read (cached base + fresh now). Its time-dependent
        // statistics/cache drive the economics projection; the economics BASE (cost/benefit terms) depends only
        // on the pure metrics counts and is cached behind the (repo, economics-base) gate.
        DecisionSessionMetricsSnapshot metricsSnapshot = await metricsService.GetMetricsAsync(repositoryId);

        if (derivedReader is not null)
        {
            return await derivedReader.ReadDerivedAsync(
                repository,
                DecisionSessionAnalysisCache.EconomicsKind,
                DecisionSessionAnalysisCache.EconomicsFamilies,
                DecisionSessionAnalysisCache.FormulaVersion,
                _ => Task.FromResult(BuildBase(metricsSnapshot)),
                (economicsBase, now) => Project(repository.Id, now, metricsSnapshot, economicsBase),
                CancellationToken.None);
        }

        DecisionSessionEconomicsBase fallbackBase = BuildBase(metricsSnapshot);
        return Project(repository.Id, timeProvider.GetUtcNow(), metricsSnapshot, fallbackBase);
    }

    // SOURCE-PURE base: the cost/benefit terms that depend only on the pure metrics counts/bytes,
    // not on any measuredAt-relative metrics statistics. {contextCost, reasoningCost, continuityBenefit, reusableCorpusScore}.
    private DecisionSessionEconomicsBase BuildBase(DecisionSessionMetricsSnapshot metricsSnapshot)
    {
        decimal contextCost = Average(
            Normalize(metricsSnapshot.Metrics.EstimatedTokenCount, options.LargeContextTokenThreshold),
            Normalize(metricsSnapshot.Metrics.ContextByteSize, options.LargeContextByteThreshold));
        decimal reasoningCost = Average(
            Normalize(metricsSnapshot.Metrics.ReasoningEventCount, options.ReasoningEventThreshold),
            Normalize(metricsSnapshot.Metrics.ReasoningThreadCount, options.ReasoningThreadThreshold),
            Normalize(metricsSnapshot.Metrics.ReasoningRelationshipCount, options.ReasoningRelationshipThreshold));
        ContinuityBenefitAssessment continuityBenefit = CalculateContinuityBenefit(metricsSnapshot);
        decimal reusableCorpusScore = Average(
            Normalize(metricsSnapshot.Metrics.EstimatedTokenCount, options.LargeContextTokenThreshold),
            Normalize(metricsSnapshot.Metrics.ContextByteSize, options.LargeContextByteThreshold));
        return new DecisionSessionEconomicsBase(contextCost, reasoningCost, continuityBenefit, reusableCorpusScore);
    }

    // TIME-DEPENDENT projection: cacheBenefit / transferValue / reuseValue recompute from the metrics
    // snapshot's measuredAt-relative cache/statistics (idle, growthRate, cacheMissRisk).
    private DecisionSessionEconomicsSnapshot Project(
        Guid repositoryId,
        DateTimeOffset generatedAt,
        DecisionSessionMetricsSnapshot metricsSnapshot,
        DecisionSessionEconomicsBase economicsBase)
    {
        DecisionSessionEconomicsInputs inputs = new(
            metricsSnapshot.Metrics,
            metricsSnapshot.Statistics,
            metricsSnapshot.Activity,
            metricsSnapshot.Growth,
            metricsSnapshot.Cache);

        decimal contextCost = economicsBase.ContextCost;
        decimal reasoningCost = economicsBase.ReasoningCost;
        ContinuityBenefitAssessment continuityBenefit = economicsBase.ContinuityBenefit;
        CacheRiskAssessment cacheRisk = new(
            metricsSnapshot.Cache.EstimatedCacheMissRisk,
            metricsSnapshot.Cache.EstimatedCacheTtl,
            metricsSnapshot.Cache.EstimatedCacheExpiresAt);
        CacheBenefitAssessment cacheBenefit = CalculateCacheBenefit(economicsBase.ReusableCorpusScore, cacheRisk);
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
            metricsSnapshot.Diagnostics.Warnings.ToArray());

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

    private CacheBenefitAssessment CalculateCacheBenefit(decimal reusableCorpusScore, CacheRiskAssessment cacheRisk)
    {
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
