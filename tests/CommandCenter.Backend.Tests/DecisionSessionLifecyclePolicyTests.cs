using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;
using CommandCenter.DecisionSessions.Services;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionSessionLifecyclePolicyTests
{
    [Fact]
    public async Task SameInputsProduceSamePolicyDecision()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession active = await CreateActiveSessionAsync(harness);
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            active,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt),
            CreateEconomicsSnapshot(harness.Repository.Id, generatedAt),
            CreateCoherenceSnapshot(harness.Repository.Id, generatedAt),
            generatedAt);

        DecisionSessionLifecycleSnapshot first = await service.EvaluateAsync(harness.Repository.Id);
        DecisionSessionLifecycleSnapshot second = await service.EvaluateAsync(harness.Repository.Id);

        Assert.Equal(first.Evaluation.Decision, second.Evaluation.Decision);
        Assert.Equal(first.Evaluation.ReuseScore, second.Evaluation.ReuseScore);
        Assert.Equal(first.Evaluation.TransferScore, second.Evaluation.TransferScore);
        Assert.Equal(first.Evaluation.Reason, second.Evaluation.Reason);
        Assert.Equal(first.Evaluation.ContributingFactors, second.Evaluation.ContributingFactors);
        Assert.Equal(first.Diagnostics.ReuseScore, second.Diagnostics.ReuseScore);
        Assert.Equal(first.Diagnostics.TransferScore, second.Diagnostics.TransferScore);
    }

    [Fact]
    public async Task ReuseScoreGreaterThanTransferScoreDecidesContinue()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession active = await CreateActiveSessionAsync(harness);
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            active,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, growthRate: 20_000m, cacheRisk: 0.05m),
            CreateEconomicsSnapshot(
                harness.Repository.Id,
                generatedAt,
                reuseValue: 0.90m,
                transferValue: 0.05m,
                continuityBenefit: 0.90m,
                cacheBenefit: 0.85m),
            CreateCoherenceSnapshot(harness.Repository.Id, generatedAt, coherence: 0.90m, fragmentation: 0.05m, transferPressure: 0.05m),
            generatedAt);

        DecisionSessionLifecycleSnapshot snapshot = await service.EvaluateAsync(harness.Repository.Id);

        Assert.Equal(DecisionSessionLifecycleDecision.Continue, snapshot.Evaluation.Decision);
        Assert.True(snapshot.Evaluation.ReuseScore > snapshot.Evaluation.TransferScore);
    }

    [Fact]
    public async Task TransferScoreGreaterThanReuseScoreDecidesTransfer()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession active = await CreateActiveSessionAsync(harness);
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            active,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, growthRate: 800_000m, cacheRisk: 0.90m),
            CreateEconomicsSnapshot(
                harness.Repository.Id,
                generatedAt,
                reuseValue: 0.05m,
                transferValue: 0.90m,
                continuityBenefit: 0.05m,
                cacheBenefit: 0.05m),
            CreateCoherenceSnapshot(harness.Repository.Id, generatedAt, coherence: 0.05m, fragmentation: 0.90m, transferPressure: 0.90m),
            generatedAt);

        DecisionSessionLifecycleSnapshot snapshot = await service.EvaluateAsync(harness.Repository.Id);

        Assert.Equal(DecisionSessionLifecycleDecision.Transfer, snapshot.Evaluation.Decision);
        Assert.True(snapshot.Evaluation.TransferScore > snapshot.Evaluation.ReuseScore);
    }

    [Fact]
    public async Task EqualScoresDecideContinue()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession active = await CreateActiveSessionAsync(harness);
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            active,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, growthRate: 0m, cacheRisk: 0.50m),
            CreateEconomicsSnapshot(
                harness.Repository.Id,
                generatedAt,
                reuseValue: 0.50m,
                transferValue: 0.50m,
                continuityBenefit: 0.50m,
                cacheBenefit: 0.50m),
            CreateCoherenceSnapshot(harness.Repository.Id, generatedAt, coherence: 0.50m, fragmentation: 0.50m, transferPressure: 0.50m),
            generatedAt,
            new DecisionSessionLifecyclePolicyOptions(
                ReuseEconomicsWeight: 0.25m,
                CacheBenefitWeight: 0.25m,
                ContinuityBenefitWeight: 0.25m,
                CoherenceWeight: 0.25m,
                TransferEconomicsWeight: 0.25m,
                TransferPressureWeight: 0.25m,
                FragmentationWeight: 0.25m,
                GrowthWeight: 0m,
                CacheMissRiskWeight: 0.25m));

        DecisionSessionLifecycleSnapshot snapshot = await service.EvaluateAsync(harness.Repository.Id);

        Assert.Equal(snapshot.Evaluation.ReuseScore, snapshot.Evaluation.TransferScore);
        Assert.Equal(DecisionSessionLifecycleDecision.Continue, snapshot.Evaluation.Decision);
        Assert.Contains("avoids churn", snapshot.Evaluation.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HigherCacheMissRiskRaisesTransferScore()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession active = await CreateActiveSessionAsync(harness);
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        DecisionSessionEconomicsSnapshot economics = CreateEconomicsSnapshot(harness.Repository.Id, generatedAt);
        DecisionSessionCoherenceSnapshot coherence = CreateCoherenceSnapshot(harness.Repository.Id, generatedAt);

        DecisionSessionLifecycleSnapshot low = await CreateService(
            harness,
            active,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, cacheRisk: 0.10m),
            economics,
            coherence,
            generatedAt).EvaluateAsync(harness.Repository.Id);
        DecisionSessionLifecycleSnapshot high = await CreateService(
            harness,
            active,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, cacheRisk: 0.90m),
            economics,
            coherence,
            generatedAt).EvaluateAsync(harness.Repository.Id);

        Assert.True(high.Evaluation.TransferScore > low.Evaluation.TransferScore);
        Assert.True(high.Diagnostics.TransferScore.CacheMissRiskContribution > low.Diagnostics.TransferScore.CacheMissRiskContribution);
    }

    // Phase 3 retarget (refactor-lazy-sqlite.md): the lifecycle policy is entirely time-dependent and is NEVER
    // cached or persisted as a file — it is computed fresh on every read from the analysis snapshots. So there
    // is no "invalid persisted snapshot" to validate-and-rebuild; the preserved invariant is that the policy
    // evaluates cleanly from analysis even when a stale corrupt policy FILE is present, which is now ignored.
    [Fact]
    public async Task PolicyIsAlwaysComputedFromAnalysisRegardlessOfStalePolicyFile()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession active = await CreateActiveSessionAsync(harness);
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            active,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt),
            CreateEconomicsSnapshot(harness.Repository.Id, generatedAt),
            CreateCoherenceSnapshot(harness.Repository.Id, generatedAt),
            generatedAt);
        await harness.Store.WriteAsync(
            DecisionSessionArtifactPaths.Resolve(harness.Repository, DecisionSessionArtifactPaths.LifecyclePolicySnapshotJson()),
            "{ not valid json");

        DecisionSessionLifecycleSnapshot snapshot = await service.EvaluateAsync(harness.Repository.Id);

        Assert.Equal(active.Id, snapshot.Diagnostics.Inputs.Session.Id);
        Assert.True(snapshot.Diagnostics.ReuseScore.Score >= 0m);
        Assert.True(snapshot.Diagnostics.TransferScore.Score >= 0m);
    }

    [Fact]
    public async Task PolicyDiagnosticsPreserveBothScoresAndAuthorityBoundary()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession active = await CreateActiveSessionAsync(harness);
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            active,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt),
            CreateEconomicsSnapshot(harness.Repository.Id, generatedAt),
            CreateCoherenceSnapshot(harness.Repository.Id, generatedAt),
            generatedAt);

        DecisionSessionLifecycleSnapshot snapshot = await service.EvaluateAsync(harness.Repository.Id);

        Assert.Equal(active.Id, snapshot.Diagnostics.Inputs.Session.Id);
        Assert.True(snapshot.Diagnostics.ReuseScore.Score >= 0m);
        Assert.True(snapshot.Diagnostics.TransferScore.Score >= 0m);
        Assert.Contains(snapshot.Diagnostics.Assumptions, assumption => assumption.Contains("does not evaluate transfer eligibility", StringComparison.Ordinal));
        Assert.Contains(snapshot.Evaluation.ContributingFactors, factor => factor.Contains("Cache miss risk", StringComparison.Ordinal));
    }

    private static async Task<DecisionSession> CreateActiveSessionAsync(DecisionSessionTestHarness harness)
    {
        DecisionSession created = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        return await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);
    }

    private static DecisionSessionLifecyclePolicy CreateService(
        DecisionSessionTestHarness harness,
        DecisionSession active,
        DecisionSessionMetricsSnapshot metrics,
        DecisionSessionEconomicsSnapshot economics,
        DecisionSessionCoherenceSnapshot coherence,
        DateTimeOffset generatedAt,
        DecisionSessionLifecyclePolicyOptions? options = null)
    {
        return new DecisionSessionLifecyclePolicy(
            harness.RepositoryService,
            harness.RepositoryStore,
            new FixedMetricsService(metrics),
            new FixedEconomicsService(economics),
            new FixedCoherenceService(coherence),
            options ?? new DecisionSessionLifecyclePolicyOptions(),
            new FixedTimeProvider(generatedAt));
    }

    private static DecisionSessionMetricsSnapshot CreateMetricsSnapshot(
        Guid repositoryId,
        DateTimeOffset generatedAt,
        decimal growthRate = 100_000m,
        decimal cacheRisk = 0.20m)
    {
        DateTimeOffset lastActivityAt = generatedAt.AddMinutes(-5);
        var metrics = new DecisionSessionMetrics(
            25_000,
            100_000,
            20,
            10,
            10,
            8,
            2,
            3,
            4,
            lastActivityAt,
            generatedAt);
        var statistics = new DecisionSessionStatistics(
            TimeSpan.FromHours(4),
            TimeSpan.FromHours(4),
            TimeSpan.FromMinutes(5),
            growthRate,
            12m);
        var activity = new DecisionSessionActivity(47, lastActivityAt, TimeSpan.FromMinutes(5), 12m);
        var growth = new DecisionSessionGrowth(100_000, 25_000, TimeSpan.FromHours(4), growthRate);
        var cache = new DecisionSessionCacheMetrics(TimeSpan.FromHours(1), cacheRisk, lastActivityAt.AddHours(1));
        var diagnostics = new DecisionSessionMetricsDiagnostics(repositoryId, generatedAt, [], [], []);
        return new DecisionSessionMetricsSnapshot(repositoryId, metrics, statistics, activity, growth, cache, diagnostics, generatedAt);
    }

    private static DecisionSessionEconomicsSnapshot CreateEconomicsSnapshot(
        Guid repositoryId,
        DateTimeOffset generatedAt,
        decimal reuseValue = 0.60m,
        decimal transferValue = 0.40m,
        decimal continuityBenefit = 0.50m,
        decimal cacheBenefit = 0.50m)
    {
        var economics = new DecisionSessionEconomics(reuseValue, transferValue, 0.30m, 0.20m, continuityBenefit, cacheBenefit, 0.20m);
        DecisionSessionMetricsSnapshot metrics = CreateMetricsSnapshot(repositoryId, generatedAt);
        var inputs = new DecisionSessionEconomicsInputs(metrics.Metrics, metrics.Statistics, metrics.Activity, metrics.Growth, metrics.Cache);
        var diagnostics = new DecisionSessionEconomicsDiagnostics(
            repositoryId,
            generatedAt,
            inputs,
            new ReuseValueAssessment(reuseValue, continuityBenefit, cacheBenefit, 0.50m, 0.50m),
            new TransferValueAssessment(transferValue, 0.20m, 0.10m, 0.20m, 0.30m),
            new CacheBenefitAssessment(cacheBenefit, 0.50m, 0.10m, 0.80m),
            new CacheRiskAssessment(0.20m, TimeSpan.FromHours(1), generatedAt.AddHours(1)),
            new ContinuityBenefitAssessment(continuityBenefit, 0.40m, 0.30m, 0.20m),
            [],
            []);
        return new DecisionSessionEconomicsSnapshot(repositoryId, economics, diagnostics, generatedAt);
    }

    private static DecisionSessionCoherenceSnapshot CreateCoherenceSnapshot(
        Guid repositoryId,
        DateTimeOffset generatedAt,
        decimal coherence = 0.60m,
        decimal fragmentation = 0.20m,
        decimal transferPressure = 0.30m)
    {
        var coherenceModel = new DecisionSessionCoherence(coherence, fragmentation, 0.50m, 0.60m, transferPressure);
        DecisionSessionMetricsSnapshot metrics = CreateMetricsSnapshot(repositoryId, generatedAt);
        DecisionSessionEconomicsSnapshot economics = CreateEconomicsSnapshot(repositoryId, generatedAt);
        var inputs = new DecisionSessionCoherenceInputs(
            metrics.Metrics,
            metrics.Statistics,
            metrics.Cache,
            economics.Economics,
            5,
            4,
            1,
            1,
            5,
            0);
        var diagnostics = new DecisionSessionCoherenceDiagnostics(
            repositoryId,
            generatedAt,
            inputs,
            new FragmentationAssessment(fragmentation, fragmentation, 0m, 0m),
            new DensityAssessment(0.50m, 0.50m, 5, 4),
            new ContinuityQualityAssessment(0.60m, 0.50m, 0.50m, 0.50m, 0.50m),
            new TransferPressureAssessment(transferPressure, fragmentation, 0.10m, 1m - coherence, 0.20m, 0.30m),
            [],
            []);
        return new DecisionSessionCoherenceSnapshot(repositoryId, coherenceModel, diagnostics, generatedAt);
    }

    private sealed class FixedMetricsService(DecisionSessionMetricsSnapshot snapshot) : IDecisionSessionMetricsService
    {
        public Task<DecisionSessionMetricsSnapshot> GetMetricsAsync(Guid repositoryId)
        {
            return Task.FromResult(snapshot);
        }
    }

    private sealed class FixedEconomicsService(DecisionSessionEconomicsSnapshot snapshot) : IDecisionSessionEconomicsService
    {
        public Task<DecisionSessionEconomicsSnapshot> GetEconomicsAsync(Guid repositoryId)
        {
            return Task.FromResult(snapshot);
        }
    }

    private sealed class FixedCoherenceService(DecisionSessionCoherenceSnapshot snapshot) : IDecisionSessionCoherenceService
    {
        public Task<DecisionSessionCoherenceSnapshot> GetCoherenceAsync(Guid repositoryId)
        {
            return Task.FromResult(snapshot);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
