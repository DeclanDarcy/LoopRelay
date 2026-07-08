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
    public async Task<SelectionDecision> ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "SelectNextEpic";
        console.Phase("Select next strategic initiative");
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        RoadmapStateDocument? existing = await stateStore.LoadAsync();
        string context = await contextBuilder.BuildSelectionContextAsync(projection.Content, existing?.RetiredEpics ?? []);
        PromptTransitionCompletion completion = await promptTransitionRunner.RunNormalWithCompletionAsync(
            RoadmapState.RoadmapCompletionContextReady,
            RoadmapState.SelectNextStrategicInitiative,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            string.Empty,
            [RoadmapArtifactPaths.Selection],
            cancellationToken);
        await artifacts.WriteAsync(RoadmapArtifactPaths.Selection, completion.Output);
        await hitlArtifactCapture.CaptureAsync(RoadmapArtifactPaths.Selection, completion.Output);
        string evidencePath = await artifacts.WriteNumberedEvidenceAsync(RoadmapArtifactPaths.SelectionEvidenceDirectory, "selection", completion.Output);
        await selectionProvenance.RecordActiveSelectionAsync(
            completion.Output,
            completion.InputSnapshot,
            existing?.RetiredEpics ?? [],
            cancellationToken);
        await lifecycleStore.UpsertAsync(RoadmapArtifactPaths.Selection, ArtifactLifecycleState.Ready, evidencePath);

        SelectionDecision decision = new SelectionParser().Parse(completion.Output);
        await decisionRecorder.AppendAsync(RoadmapState.SelectNextStrategicInitiative, runtimePrompt, projection.Definition.ProjectionPath, RoadmapArtifactPaths.Selection, decision.RecommendedOutcome, decision.Confidence, decision.PrimaryReason);
        return decision;
    }
}
