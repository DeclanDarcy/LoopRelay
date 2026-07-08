using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Completion;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Roadmap.Cli;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.ProjectContextLoader;
using RoadmapArtifacts = LoopRelay.Roadmap.Cli.RoadmapArtifacts;

namespace LoopRelay.Roadmap.Cli.Tests;

internal static class SelectionProvenanceTestSupport
{
    public static Cli.SelectionProvenanceService CreateProvenance(
        TempRepo repo,
        Cli.ExecutionPreparationProvenanceService? executionPreparation = null)
    {
        Cli.ExecutionPreparationProvenanceService provenance = executionPreparation ?? ExecutionPreparationTestSupport.CreateProvenance(repo);
        var contextBuilder = new Cli.RoadmapPromptContextBuilder(repo.Artifacts, provenance);
        var inputResolver = new Cli.TransitionInputResolver(repo.Artifacts, provenance);
        return new Cli.SelectionProvenanceService(
            repo.Artifacts,
            new Cli.SelectionProvenanceManifestStore(repo.Artifacts),
            contextBuilder,
            inputResolver);
    }

    public static async Task SeedCurrentSelectionAsync(
        TempRepo repo,
        string selection,
        IReadOnlyList<Cli.RetiredEpic>? retiredEpics = null)
    {
        IReadOnlyList<Cli.RetiredEpic> effectiveRetiredEpics = retiredEpics ?? [];
        string projectionPath = Cli.RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"];
        if (!await repo.Artifacts.ExistsAsync(projectionPath))
        {
            repo.Write(projectionPath, ProjectionSamples.Valid("SelectNextEpic"));
        }

        Cli.ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync(CancellationToken.None);
        var projections = new Cli.ProjectionRegistry();
        Cli.ProjectionProvenance projectionProvenance = new Cli.ProjectionProvenanceFactory(projections)
            .Create("SelectNextEpic", projectContext);
        await new Cli.ProjectionManifestStore(repo.Artifacts).UpsertAsync(Cli.ProjectionManifestEntry.FromTrustedProvenance(
            projectionProvenance,
            Cli.RoadmapHash.Sha256(repo.Read(projectionPath)),
            DateTimeOffset.UtcNow,
            Cli.ProjectionValidationStatus.Valid,
            Cli.ProjectionFreshness.Fresh,
            null));

        repo.Write(Cli.RoadmapArtifactPaths.Selection, selection);
        var provenance = CreateProvenance(repo);
        Cli.TransitionInputSnapshot cycle = await provenance.CaptureCurrentCycleAsync(
            repo.Read(projectionPath),
            effectiveRetiredEpics);
        await provenance.RecordActiveSelectionAsync(selection, cycle, effectiveRetiredEpics);
        await new Cli.ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(Cli.RoadmapArtifactPaths.Selection, Cli.ArtifactLifecycleState.Ready);
    }
}
