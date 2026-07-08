using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.TransitionInputs;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Primitives.Projections;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Decisions;
using LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.State;
using LoopRelay.Roadmap.Cli.Services.TransitionState;
using LoopRelay.Roadmap.Cli.Tests.Services.Execution;
using LoopRelay.Roadmap.Cli.Tests.Services.Projections;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.Services.Projections.ProjectContextLoader;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Selection;

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

        ProjectContext projectContext = await new ProjectContextLoader(repo.Artifacts).LoadAsync(CancellationToken.None);
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
