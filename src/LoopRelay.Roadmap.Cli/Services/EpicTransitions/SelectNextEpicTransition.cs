using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.Invocation;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Decisions;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;

namespace LoopRelay.Roadmap.Cli.Services.EpicTransitions;

internal sealed class SelectNextEpicTransition(
    RoadmapArtifacts _artifacts,
    PromptContractRegistry _contractRegistry,
    ProjectionCache _projectionCache,
    RoadmapPromptContextBuilder _contextBuilder,
    State.RoadmapStateStore _stateStore,
    RoadmapPromptTransitionRunner _promptTransitionRunner,
    SelectionProvenanceService _selectionProvenance,
    DecisionRecorder _decisionRecorder,
    HitlArtifactCapture _hitlArtifactCapture,
    ArtifactLifecycleStore _lifecycleStore,
    ILoopConsole _console)
{
    public async Task<SelectionDecision> ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "SelectNextEpic";
        _console.Phase("Select next strategic initiative");
        PromptContract contract = _contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await _projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        RoadmapStateDocument? existing = await _stateStore.LoadAsync();
        string context = await _contextBuilder.BuildSelectionContextAsync(projection.Content, existing?.RetiredEpics ?? []);
        PromptTransitionCompletion completion = await _promptTransitionRunner.RunNormalWithCompletionAsync(
            RoadmapState.RoadmapCompletionContextReady,
            RoadmapState.SelectNextStrategicInitiative,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            string.Empty,
            [RoadmapArtifactPaths.Selection],
            cancellationToken);
        await _artifacts.WriteAsync(RoadmapArtifactPaths.Selection, completion.Output);
        await _hitlArtifactCapture.CaptureAsync(RoadmapArtifactPaths.Selection, completion.Output);
        string evidencePath = await _artifacts.WriteNumberedEvidenceAsync(RoadmapArtifactPaths.SelectionEvidenceDirectory, "selection", completion.Output);
        await _selectionProvenance.RecordActiveSelectionAsync(
            completion.Output,
            completion.InputSnapshot,
            existing?.RetiredEpics ?? [],
            cancellationToken);
        await _lifecycleStore.UpsertAsync(RoadmapArtifactPaths.Selection, ArtifactLifecycleState.Ready, evidencePath);

        SelectionDecision decision = new SelectionParser().Parse(completion.Output);
        await _decisionRecorder.AppendAsync(RoadmapState.SelectNextStrategicInitiative, runtimePrompt, projection.Definition.ProjectionPath, RoadmapArtifactPaths.Selection, decision.RecommendedOutcome, decision.Confidence, decision.PrimaryReason);
        return decision;
    }
}
