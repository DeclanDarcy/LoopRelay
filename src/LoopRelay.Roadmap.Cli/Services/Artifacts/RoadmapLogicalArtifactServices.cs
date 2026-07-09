using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Abstractions.Persistence;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Roadmap.Cli.Services.Artifacts;

internal static class RoadmapLogicalArtifactServices
{
    public static ICanonicalArtifactHasher CreateCanonicalHasher(RoadmapArtifacts artifacts) =>
        CreateCanonicalHasher(
            artifacts.Store,
            artifacts.Repository,
            artifacts.ExecutionEvidenceStore);

    public static ICanonicalArtifactHasher CreateCanonicalHasher(
        IArtifactStore store,
        Repository repository,
        IExecutionEvidenceStore executionEvidenceStore)
    {
        var resolver = new LogicalArtifactResolver(
        [
            new RetainedFilesystemLogicalArtifactProvider(
                store,
                repository,
                RetainedExactPaths(),
                RetainedPatterns()),
            new FileBackedMigratedDomainLogicalArtifactProvider(
                store,
                repository,
                FileBackedMigratedExactPaths(),
                FileBackedMigratedPatterns()),
            new FileBackedExecutionEvidenceLogicalArtifactProvider(executionEvidenceStore),
        ]);

        return new CanonicalArtifactHasher(resolver);
    }

    private static IReadOnlyDictionary<string, LogicalArtifactDomain> RetainedExactPaths()
    {
        var paths = new Dictionary<string, LogicalArtifactDomain>(StringComparer.OrdinalIgnoreCase)
        {
            [OrchestrationArtifactPaths.SpecsEpic] = LogicalArtifactDomain.RetainedFile,
            [RoadmapArtifactPaths.ActiveEpic] = LogicalArtifactDomain.RetainedFile,
            [OrchestrationArtifactPaths.Plan] = LogicalArtifactDomain.RetainedFile,
            [OrchestrationArtifactPaths.Details] = LogicalArtifactDomain.RetainedFile,
            [RoadmapArtifactPaths.OperationalContext] = LogicalArtifactDomain.RetainedFile,
            [OrchestrationArtifactPaths.OperationalDelta] = LogicalArtifactDomain.RetainedFile,
            [OrchestrationArtifactPaths.Decisions] = LogicalArtifactDomain.RetainedFile,
            [OrchestrationArtifactPaths.LiveHandoff] = LogicalArtifactDomain.RetainedFile,
            [RoadmapArtifactPaths.Selection] = LogicalArtifactDomain.RetainedFile,
            [RoadmapArtifactPaths.RoadmapCompletionContext] = LogicalArtifactDomain.RetainedFile,
            [RoadmapArtifactPaths.ExecutionPrompt] = LogicalArtifactDomain.RetainedFile,
            [RoadmapArtifactPaths.PromptContracts] = LogicalArtifactDomain.RetainedFile,
        };

        foreach (string path in RoadmapArtifactPaths.ProjectContextSourceFiles)
        {
            paths[path] = LogicalArtifactDomain.RetainedFile;
        }

        foreach (string path in RoadmapArtifactPaths.ProjectionPaths.Values)
        {
            paths[path] = LogicalArtifactDomain.ProjectionBody;
        }

        return paths;
    }

    private static IReadOnlyList<LogicalArtifactPathPattern> RetainedPatterns() =>
    [
        new(RoadmapArtifactPaths.SpecsDirectory, "*.md", LogicalArtifactDomain.RetainedFile),
        new(RoadmapArtifactPaths.ExecutionMilestonesDirectory, "m*.md", LogicalArtifactDomain.RetainedFile),
        new(RoadmapArtifactPaths.RoadmapDirectory, "*.md", LogicalArtifactDomain.RetainedFile),
        new(RoadmapArtifactPaths.CompletedEpicsDirectory, "*.md", LogicalArtifactDomain.CompletedEpicArchive),
        new(RoadmapArtifactPaths.AuditEvidenceDirectory, "*", LogicalArtifactDomain.RetainedFile),
        new(RoadmapArtifactPaths.BlockerEvidenceDirectory, "*", LogicalArtifactDomain.RetainedFile),
        new(RoadmapArtifactPaths.EvaluationEvidenceDirectory, "*", LogicalArtifactDomain.RetainedFile),
        new(RoadmapArtifactPaths.OrchestrationEvidenceDirectory, "*", LogicalArtifactDomain.RetainedFile),
        new(OrchestrationArtifactPaths.NonImplementationReviewDirectory, "*", LogicalArtifactDomain.RetainedFile),
    ];

    private static IReadOnlyDictionary<string, LogicalArtifactDomain> FileBackedMigratedExactPaths() =>
        new Dictionary<string, LogicalArtifactDomain>(StringComparer.OrdinalIgnoreCase)
        {
            [RoadmapArtifactPaths.DecisionLedgerJson] = LogicalArtifactDomain.DecisionLedger,
            [RoadmapArtifactPaths.StateJson] = LogicalArtifactDomain.RoadmapState,
            [RoadmapArtifactPaths.LifecycleJson] = LogicalArtifactDomain.ArtifactLifecycle,
            [RoadmapArtifactPaths.ExecutionPreparationManifest] = LogicalArtifactDomain.ExecutionPreparationManifest,
            [RoadmapArtifactPaths.SelectionProvenanceManifest] = LogicalArtifactDomain.SelectionProvenanceManifest,
            [RoadmapArtifactPaths.ProjectionsManifest] = LogicalArtifactDomain.ProjectionManifest,
            [RoadmapArtifactPaths.ProjectionsManifestJson] = LogicalArtifactDomain.ProjectionManifest,
            [RoadmapArtifactPaths.TransitionJournal] = LogicalArtifactDomain.TransitionJournal,
        };

    private static IReadOnlyList<LogicalArtifactPathPattern> FileBackedMigratedPatterns() =>
    [
        new(
            OrchestrationArtifactPaths.DecisionsDirectory,
            OrchestrationArtifactPaths.HistoricalDecisionSearchPattern,
            LogicalArtifactDomain.LoopHistory,
            "decisions"),
        new(
            OrchestrationArtifactPaths.HandoffsDirectory,
            OrchestrationArtifactPaths.HistoricalHandoffSearchPattern,
            LogicalArtifactDomain.LoopHistory,
            "handoff"),
        new(
            OrchestrationArtifactPaths.DeltasDirectory,
            OrchestrationArtifactPaths.HistoricalDeltaSearchPattern,
            LogicalArtifactDomain.LoopHistory,
            "operational_delta"),
        new(
            RoadmapArtifactPaths.SplitFamiliesDirectory,
            "split-family-*.json",
            LogicalArtifactDomain.SplitLineage,
            "split-family"),
    ];
}
