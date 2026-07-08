using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Invocation;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.TransitionInputs;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;

namespace LoopRelay.Roadmap.Cli.Services.EpicTransitions;

internal sealed class ActiveEpicRewriteTransition(
    RoadmapArtifacts artifacts,
    PromptContractRegistry contractRegistry,
    ProjectionCache projectionCache,
    RoadmapPromptContextBuilder contextBuilder,
    ActiveSelectionReader activeSelectionReader,
    RoadmapPromptTransitionRunner promptTransitionRunner,
    ActiveEpicPromotionCoordinator activeEpicPromotionCoordinator,
    ILoopConsole console)
{
    private readonly RoadmapArtifacts _artifacts = artifacts;
    private readonly PromptContractRegistry _contractRegistry = contractRegistry;
    private readonly ProjectionCache _projectionCache = projectionCache;
    private readonly RoadmapPromptContextBuilder _contextBuilder = contextBuilder;
    private readonly ActiveSelectionReader _activeSelectionReader = activeSelectionReader;
    private readonly RoadmapPromptTransitionRunner _promptTransitionRunner = promptTransitionRunner;
    private readonly ActiveEpicPromotionCoordinator _activeEpicPromotionCoordinator = activeEpicPromotionCoordinator;
    private readonly ILoopConsole _console = console;
    public async Task<ArtifactPromotionResult> ExecuteAsync(
        string runtimePrompt,
        RoadmapState state,
        ProjectContext projectContext,
        string auditPath,
        CancellationToken cancellationToken)
    {
        _console.Phase(runtimePrompt);
        string selectionOrEpic = await _artifacts.ReadAsync(RoadmapArtifactPaths.ActiveEpic) ?? await _activeSelectionReader.ReadAsync(cancellationToken);
        string audit = await _artifacts.ReadRequiredAsync(auditPath);
        PromptContract contract = _contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await _projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = _contextBuilder.BuildRealignOrReimagineContext(projection.Content, selectionOrEpic, audit);
        PromptTransitionCompletion completion = await _promptTransitionRunner.RunPromotionCandidateAsync(
            state,
            RoadmapState.ActiveEpicReady,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            audit,
            [RoadmapArtifactPaths.ActiveEpic],
            cancellationToken,
            TransitionInputContext.AuditEvidence(auditPath));
        return await _activeEpicPromotionCoordinator.PromoteAsync(state, runtimePrompt, projection.Definition.ProjectionPath, completion);
    }
}
