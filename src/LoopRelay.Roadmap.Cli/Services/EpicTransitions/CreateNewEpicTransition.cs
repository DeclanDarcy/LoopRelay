using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Invocation;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;

namespace LoopRelay.Roadmap.Cli.Services.EpicTransitions;

internal sealed class CreateNewEpicTransition(
    PromptContractRegistry _contractRegistry,
    ProjectionCache _projectionCache,
    RoadmapPromptContextBuilder _contextBuilder,
    ActiveSelectionReader _activeSelectionReader,
    RoadmapPromptTransitionRunner _promptTransitionRunner,
    ActiveEpicPromotionCoordinator _activeEpicPromotionCoordinator,
    ILoopConsole _console)
{
    public async Task<ArtifactPromotionResult> ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "CreateNewEpic";
        _console.Phase("Create new epic");
        string selection = await _activeSelectionReader.ReadAsync(cancellationToken);
        PromptContract contract = _contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await _projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = _contextBuilder.BuildCreateOrSplitContext(projection.Content, selection);
        PromptTransitionCompletion completion = await _promptTransitionRunner.RunPromotionCandidateAsync(
            RoadmapState.NewEpicProposed,
            RoadmapState.ActiveEpicReady,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            selection,
            [RoadmapArtifactPaths.ActiveEpic],
            cancellationToken);
        return await _activeEpicPromotionCoordinator.PromoteAsync(
            RoadmapState.NewEpicProposed,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            completion);
    }
}
