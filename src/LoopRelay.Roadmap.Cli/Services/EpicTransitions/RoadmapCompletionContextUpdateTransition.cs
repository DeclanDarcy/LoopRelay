using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Services.Transitions;

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
    public async Task ExecuteAsync(
        ProjectContext projectContext,
        string evaluationPath,
        string completedEpicSynthesisPath,
        string completedEpicSynthesis,
        CancellationToken cancellationToken)
    {
        const string runtimePrompt = "UpdateRoadmapCompletionContext";
        console.Phase("Update roadmap completion context");
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = await contextBuilder.BuildCompletionUpdateContextAsync(
            projection.Content,
            evaluationPath,
            completedEpicSynthesisPath,
            completedEpicSynthesis);
        string output = await promptTransitionRunner.RunNormalAsync(
            RoadmapState.CompletionEvaluationAndContextUpdate,
            RoadmapState.SelectNextStrategicInitiative,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            completedEpicSynthesis,
            [RoadmapArtifactPaths.RoadmapCompletionContext],
            cancellationToken,
            TransitionInputContext.CompletionEvaluation(evaluationPath));
        await artifacts.WriteAsync(RoadmapArtifactPaths.RoadmapCompletionContext, output);
        await hitlArtifactCapture.CaptureAsync(RoadmapArtifactPaths.RoadmapCompletionContext, output);
        await artifacts.WriteNumberedEvidenceAsync(RoadmapArtifactPaths.EvaluationEvidenceDirectory, "roadmap-completion-update", output);
        await selectionSuperseder.SupersedeForRoadmapCompletionContextAsync();
        await decisionRecorder.AppendAsync(
            RoadmapState.CompletionEvaluationAndContextUpdate,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            RoadmapArtifactPaths.RoadmapCompletionContext,
            "Roadmap Completion Context Updated",
            "Unclear",
            "Completion context updated after certification.");
    }
}
