using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Models.Invocation;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.TransitionInputs;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;

namespace LoopRelay.Roadmap.Cli.Services.EpicTransitions;

internal sealed class RoadmapCompletionContextUpdateTransition(
    RoadmapArtifacts artifacts,
    PromptContractRegistry contractRegistry,
    ProjectionCache projectionCache,
    RoadmapPromptContextBuilder contextBuilder,
    RoadmapPromptTransitionRunner promptTransitionRunner,
    SelectionSuperseder selectionSuperseder,
    DecisionRecorder decisionRecorder,
    HitlArtifactCapture hitlArtifactCapture,
    ILoopConsole console)
{
    private readonly RoadmapArtifacts _artifacts = artifacts;
    private readonly PromptContractRegistry _contractRegistry = contractRegistry;
    private readonly ProjectionCache _projectionCache = projectionCache;
    private readonly RoadmapPromptContextBuilder _contextBuilder = contextBuilder;
    private readonly RoadmapPromptTransitionRunner _promptTransitionRunner = promptTransitionRunner;
    private readonly SelectionSuperseder _selectionSuperseder = selectionSuperseder;
    private readonly DecisionRecorder _decisionRecorder = decisionRecorder;
    private readonly HitlArtifactCapture _hitlArtifactCapture = hitlArtifactCapture;
    private readonly ILoopConsole _console = console;
    public async Task ExecuteAsync(
        ProjectContext projectContext,
        string evaluationPath,
        string completedEpicSynthesisPath,
        string completedEpicSynthesis,
        CancellationToken cancellationToken)
    {
        const string runtimePrompt = "UpdateRoadmapCompletionContext";
        _console.Phase("Update roadmap completion context");
        PromptContract contract = _contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await _projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = await _contextBuilder.BuildCompletionUpdateContextAsync(
            projection.Content,
            evaluationPath,
            completedEpicSynthesisPath,
            completedEpicSynthesis);
        string output = await _promptTransitionRunner.RunNormalAsync(
            RoadmapState.CompletionEvaluationAndContextUpdate,
            RoadmapState.SelectNextStrategicInitiative,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            completedEpicSynthesis,
            [RoadmapArtifactPaths.RoadmapCompletionContext],
            cancellationToken,
            TransitionInputContext.CompletionEvaluation(evaluationPath));
        await _artifacts.WriteAsync(RoadmapArtifactPaths.RoadmapCompletionContext, output);
        await _hitlArtifactCapture.CaptureAsync(RoadmapArtifactPaths.RoadmapCompletionContext, output);
        await _artifacts.WriteNumberedEvidenceAsync(RoadmapArtifactPaths.EvaluationEvidenceDirectory, "roadmap-completion-update", output);
        await _selectionSuperseder.SupersedeForRoadmapCompletionContextAsync();
        await _decisionRecorder.AppendAsync(
            RoadmapState.CompletionEvaluationAndContextUpdate,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            RoadmapArtifactPaths.RoadmapCompletionContext,
            "Roadmap Completion Context Updated",
            "Unclear",
            "Completion context updated after certification.");
    }
}
