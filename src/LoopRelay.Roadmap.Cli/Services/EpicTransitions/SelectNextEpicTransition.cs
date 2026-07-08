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
    RoadmapArtifacts artifacts,
    PromptContractRegistry contractRegistry,
    ProjectionCache projectionCache,
    RoadmapPromptContextBuilder contextBuilder,
    State.RoadmapStateStore stateStore,
    RoadmapPromptTransitionRunner promptTransitionRunner,
    SelectionProvenanceService selectionProvenance,
    DecisionRecorder decisionRecorder,
    HitlArtifactCapture hitlArtifactCapture,
    ArtifactLifecycleStore lifecycleStore,
    ILoopConsole console)
{
    private readonly RoadmapArtifacts _artifacts = artifacts;
    private readonly PromptContractRegistry _contractRegistry = contractRegistry;
    private readonly ProjectionCache _projectionCache = projectionCache;
    private readonly RoadmapPromptContextBuilder _contextBuilder = contextBuilder;
    private readonly State.RoadmapStateStore _stateStore = stateStore;
    private readonly RoadmapPromptTransitionRunner _promptTransitionRunner = promptTransitionRunner;
    private readonly SelectionProvenanceService _selectionProvenance = selectionProvenance;
    private readonly DecisionRecorder _decisionRecorder = decisionRecorder;
    private readonly HitlArtifactCapture _hitlArtifactCapture = hitlArtifactCapture;
    private readonly ArtifactLifecycleStore _lifecycleStore = lifecycleStore;
    private readonly ILoopConsole _console = console;
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
