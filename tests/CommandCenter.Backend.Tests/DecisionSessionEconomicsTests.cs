using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;
using CommandCenter.DecisionSessions.Services;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionSessionEconomicsTests
{
    [Fact]
    public async Task SameInputsProduceSameEconomics()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        var metrics = CreateMetricsSnapshot(harness.Repository.Id, generatedAt, decisionCount: 10, tokenCount: 10_000, cacheRisk: 0.25m);
        var service = CreateService(harness, metrics, generatedAt);

        DecisionSessionEconomicsSnapshot first = await service.GetEconomicsAsync(harness.Repository.Id);
        DecisionSessionEconomicsSnapshot second = await service.GetEconomicsAsync(harness.Repository.Id);

        Assert.Equal(first.Economics, second.Economics);
        Assert.Equal(first.Diagnostics.ReuseValue, second.Diagnostics.ReuseValue);
        Assert.Equal(first.Diagnostics.TransferValue, second.Diagnostics.TransferValue);
        Assert.Equal(first.Diagnostics.CacheBenefit, second.Diagnostics.CacheBenefit);
    }

    [Fact]
    public async Task MoreContinuityIncreasesReuseValue()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        DecisionSessionEconomicsSnapshot low = await CreateService(
            harness,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, decisionCount: 1, operationalContextRevisionCount: 1),
            generatedAt).GetEconomicsAsync(harness.Repository.Id);
        DecisionSessionEconomicsSnapshot high = await CreateService(
            harness,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, decisionCount: 80, reasoningRelationshipCount: 60, operationalContextRevisionCount: 40),
            generatedAt).GetEconomicsAsync(harness.Repository.Id);

        Assert.True(high.Economics.EstimatedContinuityBenefit > low.Economics.EstimatedContinuityBenefit);
        Assert.True(high.Economics.EstimatedReuseValue > low.Economics.EstimatedReuseValue);
    }

    [Fact]
    public async Task HigherCacheMissRiskIncreasesTransferValue()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        DecisionSessionEconomicsSnapshot low = await CreateService(
            harness,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, cacheRisk: 0.10m, idleMinutes: 6),
            generatedAt).GetEconomicsAsync(harness.Repository.Id);
        DecisionSessionEconomicsSnapshot high = await CreateService(
            harness,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, cacheRisk: 0.90m, idleMinutes: 54),
            generatedAt).GetEconomicsAsync(harness.Repository.Id);

        Assert.True(high.Economics.EstimatedCacheMissRisk > low.Economics.EstimatedCacheMissRisk);
        Assert.True(high.Economics.EstimatedTransferValue > low.Economics.EstimatedTransferValue);
    }

    [Fact]
    public async Task LargerReusableCorpusIncreasesCacheBenefit()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        DecisionSessionEconomicsSnapshot small = await CreateService(
            harness,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, tokenCount: 1_000, byteCount: 4_000, cacheRisk: 0.10m),
            generatedAt).GetEconomicsAsync(harness.Repository.Id);
        DecisionSessionEconomicsSnapshot large = await CreateService(
            harness,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, tokenCount: 120_000, byteCount: 480_000, cacheRisk: 0.10m),
            generatedAt).GetEconomicsAsync(harness.Repository.Id);

        Assert.True(large.Economics.EstimatedCacheBenefit > small.Economics.EstimatedCacheBenefit);
        Assert.True(large.Diagnostics.CacheBenefit.ReusableCorpusScore > small.Diagnostics.CacheBenefit.ReusableCorpusScore);
    }

    // Phase 3 retarget (refactor-lazy-sqlite.md): economics is no longer persisted as a derived FILE, so there
    // is no "invalid persisted snapshot" to validate-and-rebuild. The preserved invariant is that the served
    // economics snapshot is always freshly COMPUTED from the metrics base (a stale/corrupt cache row is a miss,
    // never a corrupt read) — so even with a leftover corrupt analysis FILE present, the snapshot is valid and
    // the file is irrelevant to the result.
    [Fact]
    public async Task EconomicsIsAlwaysComputedFromMetricsRegardlessOfStaleAnalysisFile()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, decisionCount: 5),
            generatedAt);
        await harness.Store.WriteAsync(
            DecisionSessionArtifactPaths.Resolve(harness.Repository, DecisionSessionArtifactPaths.EconomicsSnapshotJson()),
            "{ not valid json");

        DecisionSessionEconomicsSnapshot snapshot = await service.GetEconomicsAsync(harness.Repository.Id);

        Assert.True(snapshot.Economics.EstimatedReuseValue > 0m);
        Assert.Equal(harness.Repository.Id, snapshot.RepositoryId);
    }

    [Fact]
    public async Task DiagnosticsExplainInputsAssumptionsTtlAndCacheRisk()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, tokenCount: 25_000, cacheRisk: 0.40m),
            generatedAt);

        DecisionSessionEconomicsSnapshot snapshot = await service.GetEconomicsAsync(harness.Repository.Id);

        Assert.Equal(25_000, snapshot.Diagnostics.Inputs.Metrics.EstimatedTokenCount);
        Assert.Equal(TimeSpan.FromHours(1), snapshot.Diagnostics.CacheRisk.EstimatedCacheTtl);
        Assert.Equal(0.40m, snapshot.Diagnostics.CacheRisk.Value);
        Assert.Contains(snapshot.Diagnostics.Assumptions, assumption => assumption.Contains("cached-token cost factor", StringComparison.Ordinal));
        Assert.Contains(snapshot.Diagnostics.Assumptions, assumption => assumption.Contains("deterministic analysis", StringComparison.Ordinal));
    }

    private static DecisionSessionEconomicsService CreateService(
        DecisionSessionTestHarness harness,
        DecisionSessionMetricsSnapshot metrics,
        DateTimeOffset generatedAt)
    {
        return new DecisionSessionEconomicsService(
            harness.RepositoryService,
            harness.RepositoryStore,
            new FixedMetricsService(metrics),
            new DecisionSessionEconomicsOptions(),
            new FixedTimeProvider(generatedAt));
    }

    private static DecisionSessionMetricsSnapshot CreateMetricsSnapshot(
        Guid repositoryId,
        DateTimeOffset generatedAt,
        long decisionCount = 0,
        long reasoningRelationshipCount = 0,
        long operationalContextRevisionCount = 0,
        long tokenCount = 10_000,
        long byteCount = 40_000,
        decimal cacheRisk = 0.20m,
        double idleMinutes = 12)
    {
        DateTimeOffset lastActivityAt = generatedAt.AddMinutes(-idleMinutes);
        var metrics = new DecisionSessionMetrics(
            tokenCount,
            byteCount,
            20,
            10,
            reasoningRelationshipCount,
            decisionCount,
            0,
            0,
            operationalContextRevisionCount,
            lastActivityAt,
            generatedAt);
        var statistics = new DecisionSessionStatistics(
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(2),
            TimeSpan.FromMinutes(idleMinutes),
            byteCount / 2m,
            15m);
        var activity = new DecisionSessionActivity(decisionCount + 30, lastActivityAt, TimeSpan.FromMinutes(idleMinutes), 15m);
        var growth = new DecisionSessionGrowth(byteCount, tokenCount, TimeSpan.FromHours(2), byteCount / 2m);
        var cache = new DecisionSessionCacheMetrics(TimeSpan.FromHours(1), cacheRisk, lastActivityAt.AddHours(1));
        var diagnostics = new DecisionSessionMetricsDiagnostics(repositoryId, generatedAt, [], [], []);
        return new DecisionSessionMetricsSnapshot(repositoryId, metrics, statistics, activity, growth, cache, diagnostics, generatedAt);
    }

    private sealed class FixedMetricsService(DecisionSessionMetricsSnapshot snapshot) : IDecisionSessionMetricsService
    {
        public Task<DecisionSessionMetricsSnapshot> GetMetricsAsync(Guid repositoryId)
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
