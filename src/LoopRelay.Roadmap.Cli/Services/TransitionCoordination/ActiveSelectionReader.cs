using LoopRelay.Roadmap.Cli.Models.DerivedArtifacts;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.TransitionInputs;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Decisions;

namespace LoopRelay.Roadmap.Cli.Services.TransitionCoordination;

internal sealed class ActiveSelectionReader(
    RoadmapArtifacts _artifacts,
    State.RoadmapStateStore _stateStore,
    SelectionProvenanceService _selectionProvenance)
{
    public async Task<string> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string selection = await _artifacts.ReadRequiredAsync(RoadmapArtifactPaths.Selection);
        string projectionPath = RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"];
        string? selectionProjection = await _artifacts.ReadAsync(projectionPath);
        if (string.IsNullOrWhiteSpace(selectionProjection))
        {
            throw new RoadmapStepException("Active selection cannot be used because its SelectNextEpic projection is missing.");
        }

        RoadmapStateDocument? state = await _stateStore.LoadAsync();
        IReadOnlyList<RetiredEpic> retiredEpics = state?.RetiredEpics ?? [];
        TransitionInputSnapshot currentCycle = await _selectionProvenance.CaptureCurrentCycleAsync(
            selectionProjection,
            retiredEpics,
            cancellationToken);
        DerivedArtifactFreshness freshness = await _selectionProvenance.EvaluateActiveSelectionFreshnessAsync(
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
