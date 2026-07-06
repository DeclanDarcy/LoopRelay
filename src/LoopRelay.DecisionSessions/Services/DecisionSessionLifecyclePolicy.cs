using LoopRelay.Core.Repositories;
using LoopRelay.DecisionSessions.Abstractions;
using LoopRelay.DecisionSessions.Models;

namespace LoopRelay.DecisionSessions.Services;

public sealed class DecisionSessionLifecyclePolicy(
    IRepositoryService repositoryService,
    IDecisionSessionRepository sessionRepository,
    IDecisionSessionMetricsService metricsService,
    IDecisionSessionEconomicsService economicsService,
    IDecisionSessionCoherenceService coherenceService,
    DecisionSessionLifecyclePolicyOptions options,
    TimeProvider timeProvider) : IDecisionSessionLifecyclePolicy
{
    public async Task<DecisionSessionLifecycleSnapshot> EvaluateAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DateTimeOffset evaluatedAt = timeProvider.GetUtcNow();

        // The lifecycle Continue-vs-Transfer decision is entirely time-dependent (transferScore(now) vs
        // reuseScore), so it is NEVER cached — it is computed fresh on every read. No policy snapshot is
        // persisted to .agents/decision-sessions/lifecycle anymore (refactor-lazy-sqlite.md, Phase 3).
        DecisionSession? activeSession = await sessionRepository.GetActiveAsync(repository);
        if (activeSession is null)
        {
            throw new KeyNotFoundException($"No active decision session exists for repository: {repositoryId}");
        }

        DecisionSessionMetricsSnapshot metricsSnapshot = await metricsService.GetMetricsAsync(repositoryId);
        DecisionSessionEconomicsSnapshot economicsSnapshot = await economicsService.GetEconomicsAsync(repositoryId);
        DecisionSessionCoherenceSnapshot coherenceSnapshot = await coherenceService.GetCoherenceAsync(repositoryId);
        return BuildSnapshot(
            repository.Id,
            activeSession,
            evaluatedAt,
            metricsSnapshot,
            economicsSnapshot,
            coherenceSnapshot,
            []);
    }

    private DecisionSessionLifecycleSnapshot BuildSnapshot(
        Guid repositoryId,
        DecisionSession activeSession,
        DateTimeOffset evaluatedAt,
        DecisionSessionMetricsSnapshot metricsSnapshot,
        DecisionSessionEconomicsSnapshot economicsSnapshot,
        DecisionSessionCoherenceSnapshot coherenceSnapshot,
        IReadOnlyList<string> rebuildWarnings)
    {
        ReuseScoreAssessment reuseScore = CalculateReuseScore(economicsSnapshot, coherenceSnapshot);
        TransferScoreAssessment transferScore = CalculateTransferScore(metricsSnapshot, economicsSnapshot, coherenceSnapshot);
        DecisionSessionLifecycleDecision decision = transferScore.Score > reuseScore.Score
            ? DecisionSessionLifecycleDecision.Transfer
            : DecisionSessionLifecycleDecision.Continue;
        string reason = decision == DecisionSessionLifecycleDecision.Transfer
            ? $"Transfer score {transferScore.Score:0.0000} exceeds reuse score {reuseScore.Score:0.0000}."
            : transferScore.Score == reuseScore.Score
                ? $"Reuse score {reuseScore.Score:0.0000} equals transfer score {transferScore.Score:0.0000}; continuing avoids churn."
                : $"Reuse score {reuseScore.Score:0.0000} exceeds transfer score {transferScore.Score:0.0000}.";

        var evaluation = new DecisionSessionLifecycleEvaluation(
            decision,
            reuseScore.Score,
            transferScore.Score,
            reason,
            CreateContributingFactors(reuseScore, transferScore),
            evaluatedAt);
        var inputs = new DecisionSessionLifecycleInputs(
            activeSession,
            metricsSnapshot.Metrics,
            metricsSnapshot.Statistics,
            metricsSnapshot.Cache,
            economicsSnapshot.Economics,
            coherenceSnapshot.Coherence);
        var diagnostics = new DecisionSessionLifecycleDiagnostics(
            repositoryId,
            evaluatedAt,
            inputs,
            reuseScore,
            transferScore,
            [
                "Lifecycle policy is deterministic and may only decide Continue or Transfer.",
                "Lifecycle policy does not evaluate transfer eligibility, execute transfer, or mutate registry state.",
                "Equal scores decide Continue to avoid lifecycle churn.",
                $"Growth contribution normalizes growth rate against {options.HighGrowthRateThreshold}."
            ],
            metricsSnapshot.Diagnostics.Warnings
                .Concat(economicsSnapshot.Diagnostics.Warnings)
                .Concat(coherenceSnapshot.Diagnostics.Warnings)
                .Concat(rebuildWarnings)
                .Distinct(StringComparer.Ordinal)
                .ToArray());

        return new DecisionSessionLifecycleSnapshot(repositoryId, evaluation, diagnostics, evaluatedAt);
    }

    private ReuseScoreAssessment CalculateReuseScore(
        DecisionSessionEconomicsSnapshot economicsSnapshot,
        DecisionSessionCoherenceSnapshot coherenceSnapshot)
    {
        decimal reuseContribution = Clamp(economicsSnapshot.Economics.EstimatedReuseValue);
        decimal cacheContribution = Clamp(economicsSnapshot.Economics.EstimatedCacheBenefit);
        decimal continuityContribution = Clamp(economicsSnapshot.Economics.EstimatedContinuityBenefit);
        decimal coherenceContribution = Clamp(coherenceSnapshot.Coherence.CoherenceScore);
        decimal score = Clamp(Round(
            (reuseContribution * options.ReuseEconomicsWeight) +
            (cacheContribution * options.CacheBenefitWeight) +
            (continuityContribution * options.ContinuityBenefitWeight) +
            (coherenceContribution * options.CoherenceWeight)));
        return new ReuseScoreAssessment(
            score,
            Round(reuseContribution),
            Round(cacheContribution),
            Round(continuityContribution),
            Round(coherenceContribution));
    }

    private TransferScoreAssessment CalculateTransferScore(
        DecisionSessionMetricsSnapshot metricsSnapshot,
        DecisionSessionEconomicsSnapshot economicsSnapshot,
        DecisionSessionCoherenceSnapshot coherenceSnapshot)
    {
        decimal transferContribution = Clamp(economicsSnapshot.Economics.EstimatedTransferValue);
        decimal pressureContribution = Clamp(coherenceSnapshot.Coherence.TransferPressure);
        decimal fragmentationContribution = Clamp(coherenceSnapshot.Coherence.FragmentationScore);
        decimal growthContribution = Normalize(metricsSnapshot.Statistics.GrowthRate, options.HighGrowthRateThreshold);
        decimal cacheRiskContribution = Clamp(metricsSnapshot.Cache.EstimatedCacheMissRisk);
        decimal score = Clamp(Round(
            (transferContribution * options.TransferEconomicsWeight) +
            (pressureContribution * options.TransferPressureWeight) +
            (fragmentationContribution * options.FragmentationWeight) +
            (growthContribution * options.GrowthWeight) +
            (cacheRiskContribution * options.CacheMissRiskWeight)));
        return new TransferScoreAssessment(
            score,
            Round(transferContribution),
            Round(pressureContribution),
            Round(fragmentationContribution),
            Round(growthContribution),
            Round(cacheRiskContribution));
    }

    private static IReadOnlyList<string> CreateContributingFactors(
        ReuseScoreAssessment reuseScore,
        TransferScoreAssessment transferScore)
    {
        return
        [
            $"Reuse value contribution: {reuseScore.EstimatedReuseValueContribution:0.0000}",
            $"Cache benefit contribution: {reuseScore.CacheBenefitContribution:0.0000}",
            $"Continuity benefit contribution: {reuseScore.ContinuityBenefitContribution:0.0000}",
            $"Coherence contribution: {reuseScore.CoherenceContribution:0.0000}",
            $"Transfer value contribution: {transferScore.EstimatedTransferValueContribution:0.0000}",
            $"Transfer pressure contribution: {transferScore.TransferPressureContribution:0.0000}",
            $"Fragmentation contribution: {transferScore.FragmentationContribution:0.0000}",
            $"Growth contribution: {transferScore.GrowthContribution:0.0000}",
            $"Cache miss risk contribution: {transferScore.CacheMissRiskContribution:0.0000}"
        ];
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
