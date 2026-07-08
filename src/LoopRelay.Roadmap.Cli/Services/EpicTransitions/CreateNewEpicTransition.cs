using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Services.Transitions;

internal sealed class CreateNewEpicTransition(
    PromptContractRegistry contractRegistry,
    ProjectionCache projectionCache,
    RoadmapPromptContextBuilder contextBuilder,
    ActiveSelectionReader activeSelectionReader,
    RoadmapPromptTransitionRunner promptTransitionRunner,
    ActiveEpicPromotionCoordinator activeEpicPromotionCoordinator,
    ILoopConsole console)
{
    public async Task<ArtifactPromotionResult> ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "CreateNewEpic";
        console.Phase("Create new epic");
        string selection = await activeSelectionReader.ReadAsync(cancellationToken);
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = contextBuilder.BuildCreateOrSplitContext(projection.Content, selection);
        PromptTransitionCompletion completion = await promptTransitionRunner.RunPromotionCandidateAsync(
            RoadmapState.NewEpicProposed,
            RoadmapState.ActiveEpicReady,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            selection,
            [RoadmapArtifactPaths.ActiveEpic],
            cancellationToken);
        return await activeEpicPromotionCoordinator.PromoteAsync(
            RoadmapState.NewEpicProposed,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            completion);
    }
}
