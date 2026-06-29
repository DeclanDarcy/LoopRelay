using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.Persistence.Sqlite.Abstractions;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;

namespace CommandCenter.DecisionSessions.Services;

public sealed class DecisionSessionCoherenceService(
    IRepositoryService repositoryService,
    IDecisionSessionRepository sessionRepository,
    IDecisionSessionMetricsService metricsService,
    IDecisionSessionEconomicsService economicsService,
    IReasoningGraphService graphService,
    DecisionSessionCoherenceOptions options,
    TimeProvider timeProvider,
    IDerivedSnapshotReader? derivedReader = null) : IDecisionSessionCoherenceService
{
    public async Task<DecisionSessionCoherenceSnapshot> GetCoherenceAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionSessionMetricsSnapshot metricsSnapshot = await metricsService.GetMetricsAsync(repositoryId);
        DecisionSessionEconomicsSnapshot economicsSnapshot = await economicsService.GetEconomicsAsync(repositoryId);

        if (derivedReader is not null)
        {
            return await derivedReader.ReadDerivedAsync(
                repository,
                DecisionSessionAnalysisCache.CoherenceKind,
                DecisionSessionAnalysisCache.CoherenceFamilies,
                DecisionSessionAnalysisCache.FormulaVersion,
                async _ =>
                {
                    ReasoningGraph graph = await graphService.GetGraphAsync(repositoryId);
                    return BuildBase(metricsSnapshot, graph);
                },
                (coherenceBase, now) => Project(repository.Id, now, metricsSnapshot, economicsSnapshot, coherenceBase),
                CancellationToken.None);
        }

        ReasoningGraph fallbackGraph = await graphService.GetGraphAsync(repositoryId);
        DecisionSessionCoherenceBase fallbackBase = BuildBase(metricsSnapshot, fallbackGraph);
        return Project(repository.Id, timeProvider.GetUtcNow(), metricsSnapshot, economicsSnapshot, fallbackBase);
    }

    // SOURCE-PURE base: topology (incl. the connected-components BFS), density, fragmentation,
    // continuity, and the composite coherenceScore — all deterministic from the graph + pure metrics counts.
    private DecisionSessionCoherenceBase BuildBase(DecisionSessionMetricsSnapshot metricsSnapshot, ReasoningGraph graph)
    {
        GraphTopology topology = AnalyzeTopology(graph);
        DensityAssessment density = CalculateDensity(topology);
        FragmentationAssessment fragmentation = CalculateFragmentation(topology, density.Score);
        ContinuityQualityAssessment continuity = CalculateContinuity(metricsSnapshot, topology, density.Score);
        decimal coherenceScore = Clamp(Round(
            (density.Score * 0.35m) +
            (continuity.Score * 0.35m) +
            ((1m - fragmentation.Score) * 0.30m)));
        return new DecisionSessionCoherenceBase(
            topology.NodeCount,
            topology.RelationshipCount,
            topology.IsolatedNodeCount,
            topology.DisconnectedGroupCount,
            topology.ResolvedNodeCount,
            topology.UnresolvedNodeCount,
            density,
            fragmentation,
            continuity,
            coherenceScore,
            graph.Diagnostics);
    }

    // TIME-DEPENDENT projection: transferPressure recomputes from the metrics snapshot's
    // measuredAt-relative growthRate + cacheMissRisk; everything else comes straight from the pure base.
    private DecisionSessionCoherenceSnapshot Project(
        Guid repositoryId,
        DateTimeOffset generatedAt,
        DecisionSessionMetricsSnapshot metricsSnapshot,
        DecisionSessionEconomicsSnapshot economicsSnapshot,
        DecisionSessionCoherenceBase coherenceBase)
    {
        DensityAssessment density = coherenceBase.Density;
        FragmentationAssessment fragmentation = coherenceBase.Fragmentation;
        ContinuityQualityAssessment continuity = coherenceBase.Continuity;
        decimal coherenceScore = coherenceBase.CoherenceScore;
        TransferPressureAssessment transferPressure = CalculateTransferPressure(metricsSnapshot, economicsSnapshot, fragmentation.Score, coherenceScore);

        var coherence = new DecisionSessionCoherence(
            coherenceScore,
            fragmentation.Score,
            density.Score,
            continuity.Score,
            transferPressure.Score);
        var inputs = new DecisionSessionCoherenceInputs(
            metricsSnapshot.Metrics,
            metricsSnapshot.Statistics,
            metricsSnapshot.Cache,
            economicsSnapshot.Economics,
            coherenceBase.NodeCount,
            coherenceBase.RelationshipCount,
            coherenceBase.IsolatedNodeCount,
            coherenceBase.DisconnectedGroupCount,
            coherenceBase.ResolvedNodeCount,
            coherenceBase.UnresolvedNodeCount);
        var diagnostics = new DecisionSessionCoherenceDiagnostics(
            repositoryId,
            generatedAt,
            inputs,
            fragmentation,
            density,
            continuity,
            transferPressure,
            [
                "Coherence is deterministic analysis and does not make lifecycle decisions.",
                "Density is calculated from reasoning graph relationships relative to graph nodes.",
                "Fragmentation increases with isolated nodes, disconnected graph groups, and low density.",
                "Continuity quality combines governance evidence, cross-references, operational context revisions, and resolved graph references.",
                "Transfer pressure is a signal synthesized from fragmentation, growth, low coherence, cache miss risk, and context cost."
            ],
            metricsSnapshot.Diagnostics.Warnings
                .Concat(economicsSnapshot.Diagnostics.Warnings)
                .Concat(coherenceBase.GraphDiagnostics)
                .Distinct(StringComparer.Ordinal)
                .ToArray());

        return new DecisionSessionCoherenceSnapshot(repositoryId, coherence, diagnostics, generatedAt);
    }

    private DensityAssessment CalculateDensity(GraphTopology topology)
    {
        decimal density = topology.NodeCount <= 1
            ? 0m
            : Clamp(topology.RelationshipCount / Math.Max(1m, topology.NodeCount - 1m));
        decimal thresholdScore = Normalize(topology.RelationshipCount, options.DenseGraphRelationshipThreshold);
        decimal score = topology.NodeCount == 0
            ? 0m
            : Clamp(Round((density * 0.75m) + (thresholdScore * 0.25m)));
        return new DensityAssessment(score, Round(density), topology.NodeCount, topology.RelationshipCount);
    }

    private FragmentationAssessment CalculateFragmentation(GraphTopology topology, decimal densityScore)
    {
        if (topology.NodeCount == 0)
        {
            return new FragmentationAssessment(0m, 0m, 0m, 0m);
        }

        decimal isolatedContribution = topology.IsolatedNodeCount / Math.Max(1m, topology.NodeCount);
        decimal disconnectedContribution = topology.DisconnectedGroupCount <= 1
            ? 0m
            : Clamp((topology.DisconnectedGroupCount - 1m) / Math.Max(1m, topology.NodeCount - 1m));
        decimal lowDensityContribution = 1m - densityScore;
        decimal score = Clamp(Round(
            (isolatedContribution * 0.45m) +
            (disconnectedContribution * 0.35m) +
            (lowDensityContribution * 0.20m)));
        return new FragmentationAssessment(
            score,
            Round(isolatedContribution),
            Round(disconnectedContribution),
            Round(lowDensityContribution));
    }

    private ContinuityQualityAssessment CalculateContinuity(
        DecisionSessionMetricsSnapshot metricsSnapshot,
        GraphTopology topology,
        decimal densityScore)
    {
        decimal governanceEvidence = Normalize(
            metricsSnapshot.Metrics.DecisionCount +
            metricsSnapshot.Metrics.DecisionCandidateCount +
            metricsSnapshot.Metrics.DecisionProposalCount,
            options.GovernanceEvidenceThreshold);
        decimal operationalContext = Normalize(
            metricsSnapshot.Metrics.OperationalContextRevisionCount,
            options.OperationalContextRevisionThreshold);
        decimal resolvedReference = topology.NodeCount == 0
            ? 0m
            : topology.ResolvedNodeCount / Math.Max(1m, topology.NodeCount);
        decimal score = Clamp(Round(
            (governanceEvidence * 0.35m) +
            (densityScore * 0.30m) +
            (operationalContext * 0.20m) +
            (resolvedReference * 0.15m)));
        return new ContinuityQualityAssessment(
            score,
            Round(governanceEvidence),
            Round(densityScore),
            Round(operationalContext),
            Round(resolvedReference));
    }

    private TransferPressureAssessment CalculateTransferPressure(
        DecisionSessionMetricsSnapshot metricsSnapshot,
        DecisionSessionEconomicsSnapshot economicsSnapshot,
        decimal fragmentationScore,
        decimal coherenceScore)
    {
        decimal growthContribution = Normalize(metricsSnapshot.Statistics.GrowthRate, options.HighGrowthRateThreshold);
        decimal lowCoherenceContribution = 1m - coherenceScore;
        decimal cacheRiskContribution = Clamp(metricsSnapshot.Cache.EstimatedCacheMissRisk);
        decimal contextCostContribution = Clamp(economicsSnapshot.Economics.EstimatedContextCost);
        decimal score = Clamp(Round(
            (fragmentationScore * 0.35m) +
            (growthContribution * 0.20m) +
            (lowCoherenceContribution * 0.20m) +
            (cacheRiskContribution * 0.15m) +
            (contextCostContribution * 0.10m)));
        return new TransferPressureAssessment(
            score,
            Round(fragmentationScore),
            Round(growthContribution),
            Round(lowCoherenceContribution),
            Round(cacheRiskContribution),
            Round(contextCostContribution));
    }

    private static GraphTopology AnalyzeTopology(ReasoningGraph graph)
    {
        var adjacency = graph.Nodes.ToDictionary(
            node => node.Id,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        foreach (ReasoningGraphRelationship relationship in graph.Relationships)
        {
            if (adjacency.TryGetValue(relationship.SourceNodeId, out HashSet<string>? sourceEdges) &&
                adjacency.TryGetValue(relationship.TargetNodeId, out HashSet<string>? targetEdges))
            {
                sourceEdges.Add(relationship.TargetNodeId);
                targetEdges.Add(relationship.SourceNodeId);
            }
        }

        int disconnectedGroups = CountGroups(adjacency);
        long isolatedNodes = adjacency.Count(pair => pair.Value.Count == 0);
        long resolvedNodes = graph.Nodes.Count(node => node.Resolved);
        long unresolvedNodes = graph.Nodes.Count - resolvedNodes;
        return new GraphTopology(
            graph.Nodes.Count,
            graph.Relationships.Count,
            isolatedNodes,
            disconnectedGroups,
            resolvedNodes,
            unresolvedNodes);
    }

    private static int CountGroups(IReadOnlyDictionary<string, HashSet<string>> adjacency)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        int groups = 0;
        foreach (string nodeId in adjacency.Keys.Order(StringComparer.Ordinal))
        {
            if (!visited.Add(nodeId))
            {
                continue;
            }

            groups++;
            var queue = new Queue<string>();
            queue.Enqueue(nodeId);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                foreach (string next in adjacency[current])
                {
                    if (visited.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }
        }

        return groups;
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

    private sealed record GraphTopology(
        long NodeCount,
        long RelationshipCount,
        long IsolatedNodeCount,
        long DisconnectedGroupCount,
        long ResolvedNodeCount,
        long UnresolvedNodeCount);
}
