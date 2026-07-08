using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Services.Transitions;

internal sealed class SelectionSuperseder(
    SelectionProvenanceService selectionProvenance,
    ArtifactLifecycleStore lifecycleStore)
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
        await selectionProvenance.SupersedeActiveSelectionAsync(reasons);
        await lifecycleStore.UpsertAsync(
            RoadmapArtifactPaths.Selection,
            ArtifactLifecycleState.Superseded,
            lifecycleNotes);
    }
}
