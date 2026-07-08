using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Models.Invocation;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Services.ArtifactBundles;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;

namespace LoopRelay.Roadmap.Cli.Services.EpicTransitions;

internal sealed class BootstrapRoadmapCompletionContextTransition(
    RoadmapArtifacts artifacts,
    PromptContractRegistry contractRegistry,
    ProjectionCache projectionCache,
    RoadmapPromptTransitionRunner promptTransitionRunner,
    HitlArtifactCapture hitlArtifactCapture,
    ArtifactLifecycleStore lifecycleStore,
    ILoopConsole console)
{
    public async Task ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "CreateRoadmapCompletionContext";
        console.Phase("Bootstrap roadmap completion context");
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = "# Roadmap Completion Bootstrap\n\n## Projection Content\n\n" + projection.Content;
        string completedEpicEvidence = await new CompletedEpicEvidenceLoader(artifacts).RenderAsync();
        string output = await promptTransitionRunner.RunNormalAsync(
            RoadmapState.CoreReady,
            RoadmapState.RoadmapCompletionContextReady,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            completedEpicEvidence,
            [RoadmapArtifactPaths.RoadmapCompletionContext],
            cancellationToken);
        await artifacts.WriteAsync(RoadmapArtifactPaths.RoadmapCompletionContext, output);
        await hitlArtifactCapture.CaptureAsync(RoadmapArtifactPaths.RoadmapCompletionContext, output);
        await lifecycleStore.UpsertAsync(RoadmapArtifactPaths.RoadmapCompletionContext, ArtifactLifecycleState.Ready);
    }
}
