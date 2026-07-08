using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Primitives;
using LoopRelay.Roadmap.Cli.Services;

namespace LoopRelay.Roadmap.Cli.Tests.Services;

internal static class SelectionProvenanceTestSupport
{
    public static SelectionProvenanceService CreateProvenance(
        TempRepo repo,
        ExecutionPreparationProvenanceService? executionPreparation = null)
    {
        ExecutionPreparationProvenanceService provenance = executionPreparation ?? ExecutionPreparationTestSupport.CreateProvenance(repo);
        var contextBuilder = new RoadmapPromptContextBuilder(repo.Artifacts, provenance);
        var inputResolver = new TransitionInputResolver(repo.Artifacts, provenance);
        return new SelectionProvenanceService(
            repo.Artifacts,
            new SelectionProvenanceManifestStore(repo.Artifacts),
            contextBuilder,
            inputResolver);
    }

    public static async Task SeedCurrentSelectionAsync(
        TempRepo repo,
        string selection,
        IReadOnlyList<RetiredEpic>? retiredEpics = null)
    {
        IReadOnlyList<RetiredEpic> effectiveRetiredEpics = retiredEpics ?? [];
        string projectionPath = RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"];
        if (!await repo.Artifacts.ExistsAsync(projectionPath))
        {
            repo.Write(projectionPath, ProjectionSamples.Valid("SelectNextEpic"));
        }

        ProjectContext projectContext = await new Cli.Services.ProjectContextLoader(repo.Artifacts).LoadAsync(CancellationToken.None);
        var projections = new ProjectionRegistry();
        ProjectionProvenance projectionProvenance = new ProjectionProvenanceFactory(projections)
            .Create("SelectNextEpic", projectContext);
        await new ProjectionManifestStore(repo.Artifacts).UpsertAsync(ProjectionManifestEntry.FromTrustedProvenance(
            projectionProvenance,
            RoadmapHash.Sha256(repo.Read(projectionPath)),
            DateTimeOffset.UtcNow,
            ProjectionValidationStatus.Valid,
            ProjectionFreshness.Fresh,
            null));

        repo.Write(RoadmapArtifactPaths.Selection, selection);
        var provenance = CreateProvenance(repo);
        TransitionInputSnapshot cycle = await provenance.CaptureCurrentCycleAsync(
            repo.Read(projectionPath),
            effectiveRetiredEpics);
        await provenance.RecordActiveSelectionAsync(selection, cycle, effectiveRetiredEpics);
        await new ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(RoadmapArtifactPaths.Selection, ArtifactLifecycleState.Ready);
    }
}
