using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Decisions;

namespace LoopRelay.Roadmap.Cli.Services.TransitionCoordination;

internal sealed class SelectionSuperseder(
    SelectionProvenanceService _selectionProvenance,
    ArtifactLifecycleStore _lifecycleStore)
{
    public Task SupersedeForRetiredEpicAsync() =>
        SupersedeAsync(
            [DerivedArtifactStaleReason.RetiredEpicStateDrift],
            "Retired epic state changed after EpicPreparationAudit.");

    public Task SupersedeForRoadmapCompletionContextAsync() =>
        SupersedeAsync(
            [DerivedArtifactStaleReason.RoadmapCompletionContextDrift],
            "Roadmap completion context changed after completion certification.");

    private async Task SupersedeAsync(
        IReadOnlyList<DerivedArtifactStaleReason> reasons,
        string lifecycleNotes)
    {
        await _selectionProvenance.SupersedeActiveSelectionAsync(reasons);
        await _lifecycleStore.UpsertAsync(
            RoadmapArtifactPaths.Selection,
            ArtifactLifecycleState.Superseded,
            lifecycleNotes);
    }
}
