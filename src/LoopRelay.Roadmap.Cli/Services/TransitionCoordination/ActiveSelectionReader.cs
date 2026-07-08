using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Services.Transitions;

internal sealed class ActiveSelectionReader(
    RoadmapArtifacts artifacts,
    RoadmapStateStore stateStore,
    SelectionProvenanceService selectionProvenance)
{
    public async Task<string> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string selection = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.Selection);
        string projectionPath = RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"];
        string? selectionProjection = await artifacts.ReadAsync(projectionPath);
        if (string.IsNullOrWhiteSpace(selectionProjection))
        {
            throw new RoadmapStepException("Active selection cannot be used because its SelectNextEpic projection is missing.");
        }

        RoadmapStateDocument? state = await stateStore.LoadAsync();
        IReadOnlyList<RetiredEpic> retiredEpics = state?.RetiredEpics ?? [];
        TransitionInputSnapshot currentCycle = await selectionProvenance.CaptureCurrentCycleAsync(
            selectionProjection,
            retiredEpics,
            cancellationToken);
        DerivedArtifactFreshness freshness = await selectionProvenance.EvaluateActiveSelectionFreshnessAsync(
            currentCycle,
            retiredEpics,
            cancellationToken);
        if (!freshness.IsFresh)
        {
            throw new RoadmapStepException(
                $"Active selection cannot be used because it does not belong to the current selection cycle: {FormatReasons(freshness.Reasons)}.");
        }

        return selection;
    }

    private static string FormatReasons(IReadOnlyList<DerivedArtifactStaleReason> reasons) =>
        reasons.Count == 0 ? "UnknownProvenance" : string.Join(", ", reasons);
}
