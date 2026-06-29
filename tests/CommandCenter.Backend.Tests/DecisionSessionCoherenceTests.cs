using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;
using CommandCenter.DecisionSessions.Services;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionSessionCoherenceTests
{
    [Fact]
    public async Task SameInputsProduceSameCoherence()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, decisionCount: 10, operationalContextRevisionCount: 5),
            CreateEconomicsSnapshot(harness.Repository.Id, generatedAt),
            ConnectedGraph(harness.Repository.Id, generatedAt),
            generatedAt);

        DecisionSessionCoherenceSnapshot first = await service.GetCoherenceAsync(harness.Repository.Id);
        DecisionSessionCoherenceSnapshot second = await service.GetCoherenceAsync(harness.Repository.Id);

        Assert.Equal(first.Coherence, second.Coherence);
        Assert.Equal(first.Diagnostics.Fragmentation, second.Diagnostics.Fragmentation);
        Assert.Equal(first.Diagnostics.Density, second.Diagnostics.Density);
        Assert.Equal(first.Diagnostics.ContinuityQuality, second.Diagnostics.ContinuityQuality);
    }

    [Fact]
    public async Task DisconnectedReasoningIncreasesFragmentation()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        DecisionSessionMetricsSnapshot metrics = CreateMetricsSnapshot(harness.Repository.Id, generatedAt);
        DecisionSessionEconomicsSnapshot economics = CreateEconomicsSnapshot(harness.Repository.Id, generatedAt);

        DecisionSessionCoherenceSnapshot connected = await CreateService(
            harness,
            metrics,
            economics,
            ConnectedGraph(harness.Repository.Id, generatedAt),
            generatedAt).GetCoherenceAsync(harness.Repository.Id);
        DecisionSessionCoherenceSnapshot disconnected = await CreateService(
            harness,
            metrics,
            economics,
            DisconnectedGraph(harness.Repository.Id, generatedAt),
            generatedAt).GetCoherenceAsync(harness.Repository.Id);

        Assert.True(disconnected.Coherence.FragmentationScore > connected.Coherence.FragmentationScore);
        Assert.True(disconnected.Coherence.CoherenceScore < connected.Coherence.CoherenceScore);
    }

    [Fact]
    public async Task MoreRelationshipsIncreaseDensity()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        DecisionSessionMetricsSnapshot metrics = CreateMetricsSnapshot(harness.Repository.Id, generatedAt);
        DecisionSessionEconomicsSnapshot economics = CreateEconomicsSnapshot(harness.Repository.Id, generatedAt);

        DecisionSessionCoherenceSnapshot sparse = await CreateService(
            harness,
            metrics,
            economics,
            SparseGraph(harness.Repository.Id, generatedAt),
            generatedAt).GetCoherenceAsync(harness.Repository.Id);
        DecisionSessionCoherenceSnapshot dense = await CreateService(
            harness,
            metrics,
            economics,
            DenseGraph(harness.Repository.Id, generatedAt),
            generatedAt).GetCoherenceAsync(harness.Repository.Id);

        Assert.True(dense.Coherence.DensityScore > sparse.Coherence.DensityScore);
        Assert.True(dense.Diagnostics.Density.RelationshipDensity > sparse.Diagnostics.Density.RelationshipDensity);
    }

    [Fact]
    public async Task MoreGovernanceEvidenceIncreasesContinuityScore()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        ReasoningGraph graph = ConnectedGraph(harness.Repository.Id, generatedAt);
        DecisionSessionEconomicsSnapshot economics = CreateEconomicsSnapshot(harness.Repository.Id, generatedAt);

        DecisionSessionCoherenceSnapshot low = await CreateService(
            harness,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, decisionCount: 1, operationalContextRevisionCount: 1),
            economics,
            graph,
            generatedAt).GetCoherenceAsync(harness.Repository.Id);
        DecisionSessionCoherenceSnapshot high = await CreateService(
            harness,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, decisionCount: 90, decisionProposalCount: 25, operationalContextRevisionCount: 40),
            economics,
            graph,
            generatedAt).GetCoherenceAsync(harness.Repository.Id);

        Assert.True(high.Coherence.ContinuityScore > low.Coherence.ContinuityScore);
        Assert.True(high.Diagnostics.ContinuityQuality.GovernanceEvidenceContribution > low.Diagnostics.ContinuityQuality.GovernanceEvidenceContribution);
    }

    [Fact]
    public async Task HigherFragmentationPlusGrowthIncreasesTransferPressure()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        DecisionSessionEconomicsSnapshot economics = CreateEconomicsSnapshot(harness.Repository.Id, generatedAt, contextCost: 0.60m);

        DecisionSessionCoherenceSnapshot low = await CreateService(
            harness,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, byteCount: 40_000, cacheRisk: 0.10m),
            economics,
            ConnectedGraph(harness.Repository.Id, generatedAt),
            generatedAt).GetCoherenceAsync(harness.Repository.Id);
        DecisionSessionCoherenceSnapshot high = await CreateService(
            harness,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, byteCount: 2_400_000, cacheRisk: 0.80m),
            economics,
            DisconnectedGraph(harness.Repository.Id, generatedAt),
            generatedAt).GetCoherenceAsync(harness.Repository.Id);

        Assert.True(high.Coherence.TransferPressure > low.Coherence.TransferPressure);
        Assert.True(high.Diagnostics.TransferPressure.GrowthContribution > low.Diagnostics.TransferPressure.GrowthContribution);
        Assert.True(high.Diagnostics.TransferPressure.FragmentationContribution > low.Diagnostics.TransferPressure.FragmentationContribution);
    }

    // Phase 3 retarget (refactor-lazy-sqlite.md): coherence is no longer persisted as a derived FILE, so there
    // is no "invalid persisted snapshot" to validate-and-rebuild. The preserved invariant is that the served
    // coherence snapshot is always freshly COMPUTED from the analysis + reasoning graph — a leftover corrupt
    // analysis file cannot corrupt the result and is irrelevant to it.
    [Fact]
    public async Task CoherenceIsAlwaysComputedFromAnalysisAndGraphRegardlessOfStaleAnalysisFile()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, decisionCount: 5),
            CreateEconomicsSnapshot(harness.Repository.Id, generatedAt),
            ConnectedGraph(harness.Repository.Id, generatedAt),
            generatedAt);
        await harness.Store.WriteAsync(
            DecisionSessionArtifactPaths.Resolve(harness.Repository, DecisionSessionArtifactPaths.CoherenceSnapshotJson()),
            "{ not valid json");

        DecisionSessionCoherenceSnapshot snapshot = await service.GetCoherenceAsync(harness.Repository.Id);

        Assert.True(snapshot.Coherence.CoherenceScore > 0m);
        Assert.Equal(harness.Repository.Id, snapshot.RepositoryId);
    }

    [Fact]
    public async Task DiagnosticsExplainTopologyCacheRiskAndTransferPressure()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            CreateMetricsSnapshot(harness.Repository.Id, generatedAt, cacheRisk: 0.40m),
            CreateEconomicsSnapshot(harness.Repository.Id, generatedAt, contextCost: 0.30m),
            ConnectedGraph(harness.Repository.Id, generatedAt, ["graph diagnostic"]),
            generatedAt);

        DecisionSessionCoherenceSnapshot snapshot = await service.GetCoherenceAsync(harness.Repository.Id);

        Assert.Equal(0.40m, snapshot.Diagnostics.Inputs.Cache.EstimatedCacheMissRisk);
        Assert.Equal(0.30m, snapshot.Diagnostics.Inputs.Economics.EstimatedContextCost);
        Assert.True(snapshot.Diagnostics.Inputs.GraphNodeCount > 0);
        Assert.Contains(snapshot.Diagnostics.Assumptions, assumption => assumption.Contains("Density", StringComparison.Ordinal));
        Assert.Contains(snapshot.Diagnostics.Assumptions, assumption => assumption.Contains("Transfer pressure", StringComparison.Ordinal));
        Assert.Contains(snapshot.Diagnostics.Warnings, warning => warning.Contains("graph diagnostic", StringComparison.Ordinal));
    }

    private static DecisionSessionCoherenceService CreateService(
        DecisionSessionTestHarness harness,
        DecisionSessionMetricsSnapshot metrics,
        DecisionSessionEconomicsSnapshot economics,
        ReasoningGraph graph,
        DateTimeOffset generatedAt)
    {
        return new DecisionSessionCoherenceService(
            harness.RepositoryService,
            harness.RepositoryStore,
            new FixedMetricsService(metrics),
            new FixedEconomicsService(economics),
            new FixedGraphService(graph),
            new DecisionSessionCoherenceOptions(),
            new FixedTimeProvider(generatedAt));
    }

    private static DecisionSessionMetricsSnapshot CreateMetricsSnapshot(
        Guid repositoryId,
        DateTimeOffset generatedAt,
        long decisionCount = 0,
        long decisionProposalCount = 0,
        long operationalContextRevisionCount = 0,
        long tokenCount = 10_000,
        long byteCount = 40_000,
        decimal cacheRisk = 0.20m)
    {
        DateTimeOffset lastActivityAt = generatedAt.AddMinutes(-12);
        var metrics = new DecisionSessionMetrics(
            tokenCount,
            byteCount,
            20,
            10,
            10,
            decisionCount,
            0,
            decisionProposalCount,
            operationalContextRevisionCount,
            lastActivityAt,
            generatedAt);
        var statistics = new DecisionSessionStatistics(
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(2),
            TimeSpan.FromMinutes(12),
            byteCount / 2m,
            15m);
        var activity = new DecisionSessionActivity(decisionCount + decisionProposalCount + 30, lastActivityAt, TimeSpan.FromMinutes(12), 15m);
        var growth = new DecisionSessionGrowth(byteCount, tokenCount, TimeSpan.FromHours(2), byteCount / 2m);
        var cache = new DecisionSessionCacheMetrics(TimeSpan.FromHours(1), cacheRisk, lastActivityAt.AddHours(1));
        var diagnostics = new DecisionSessionMetricsDiagnostics(repositoryId, generatedAt, [], [], []);
        return new DecisionSessionMetricsSnapshot(repositoryId, metrics, statistics, activity, growth, cache, diagnostics, generatedAt);
    }

    private static DecisionSessionEconomicsSnapshot CreateEconomicsSnapshot(
        Guid repositoryId,
        DateTimeOffset generatedAt,
        decimal contextCost = 0.20m)
    {
        var economics = new DecisionSessionEconomics(0.50m, 0.30m, contextCost, 0.20m, 0.40m, 0.30m, 0.20m);
        var metrics = CreateMetricsSnapshot(repositoryId, generatedAt).Metrics;
        var statistics = CreateMetricsSnapshot(repositoryId, generatedAt).Statistics;
        var activity = CreateMetricsSnapshot(repositoryId, generatedAt).Activity;
        var growth = CreateMetricsSnapshot(repositoryId, generatedAt).Growth;
        var cache = CreateMetricsSnapshot(repositoryId, generatedAt).Cache;
        var inputs = new DecisionSessionEconomicsInputs(metrics, statistics, activity, growth, cache);
        var diagnostics = new DecisionSessionEconomicsDiagnostics(
            repositoryId,
            generatedAt,
            inputs,
            new ReuseValueAssessment(0.50m, 0.40m, 0.30m, 0.50m, 0.60m),
            new TransferValueAssessment(0.30m, 0.20m, 0.10m, 0.20m, contextCost),
            new CacheBenefitAssessment(0.30m, 0.40m, 0.10m, 0.80m),
            new CacheRiskAssessment(0.20m, TimeSpan.FromHours(1), generatedAt.AddHours(1)),
            new ContinuityBenefitAssessment(0.40m, 0.30m, 0.20m, 0.10m),
            [],
            []);
        return new DecisionSessionEconomicsSnapshot(repositoryId, economics, diagnostics, generatedAt);
    }

    private static ReasoningGraph ConnectedGraph(Guid repositoryId, DateTimeOffset generatedAt, IReadOnlyList<string>? diagnostics = null)
    {
        ReasoningGraphNode[] nodes =
        [
            Node("e1"),
            Node("e2"),
            Node("t1", ReasoningReferenceKind.ReasoningThread),
            Node("d1", ReasoningReferenceKind.Decision)
        ];
        ReasoningGraphRelationship[] relationships =
        [
            Relationship("r1", "ReasoningEvent:e1", "ReasoningEvent:e2"),
            Relationship("r2", "ReasoningEvent:e2", "ReasoningThread:t1"),
            Relationship("r3", "ReasoningEvent:e1", "Decision:d1")
        ];
        return new ReasoningGraph(repositoryId, generatedAt, nodes, relationships, diagnostics ?? []);
    }

    private static ReasoningGraph SparseGraph(Guid repositoryId, DateTimeOffset generatedAt)
    {
        ReasoningGraphNode[] nodes = [Node("e1"), Node("e2"), Node("e3"), Node("e4")];
        ReasoningGraphRelationship[] relationships = [Relationship("r1", "ReasoningEvent:e1", "ReasoningEvent:e2")];
        return new ReasoningGraph(repositoryId, generatedAt, nodes, relationships, []);
    }

    private static ReasoningGraph DenseGraph(Guid repositoryId, DateTimeOffset generatedAt)
    {
        ReasoningGraphNode[] nodes = [Node("e1"), Node("e2"), Node("e3"), Node("e4")];
        ReasoningGraphRelationship[] relationships =
        [
            Relationship("r1", "ReasoningEvent:e1", "ReasoningEvent:e2"),
            Relationship("r2", "ReasoningEvent:e2", "ReasoningEvent:e3"),
            Relationship("r3", "ReasoningEvent:e3", "ReasoningEvent:e4"),
            Relationship("r4", "ReasoningEvent:e4", "ReasoningEvent:e1")
        ];
        return new ReasoningGraph(repositoryId, generatedAt, nodes, relationships, []);
    }

    private static ReasoningGraph DisconnectedGraph(Guid repositoryId, DateTimeOffset generatedAt)
    {
        ReasoningGraphNode[] nodes = [Node("e1"), Node("e2"), Node("e3"), Node("e4"), Node("e5")];
        ReasoningGraphRelationship[] relationships = [Relationship("r1", "ReasoningEvent:e1", "ReasoningEvent:e2")];
        return new ReasoningGraph(repositoryId, generatedAt, nodes, relationships, []);
    }

    private static ReasoningGraphNode Node(string id, ReasoningReferenceKind kind = ReasoningReferenceKind.ReasoningEvent)
    {
        return new ReasoningGraphNode($"{kind}:{id}", kind, id, id, true, new ReasoningReference(kind, id));
    }

    private static ReasoningGraphRelationship Relationship(string id, string sourceNodeId, string targetNodeId)
    {
        return new ReasoningGraphRelationship(id, ReasoningRelationshipType.Supports, sourceNodeId, targetNodeId, id, "test", id);
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

    private sealed class FixedGraphService(ReasoningGraph graph) : IReasoningGraphService
    {
        public Task<ReasoningGraph> GetGraphAsync(Guid repositoryId)
        {
            return Task.FromResult(graph);
        }

        public Task<ReasoningTrace> TraceBackwardAsync(Guid repositoryId, ReasoningReference target)
        {
            throw new NotSupportedException();
        }

        public Task<ReasoningTrace> TraceForwardAsync(Guid repositoryId, ReasoningReference target)
        {
            throw new NotSupportedException();
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
