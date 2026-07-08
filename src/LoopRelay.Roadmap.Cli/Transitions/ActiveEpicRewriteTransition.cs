namespace LoopRelay.Roadmap.Cli;

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
    public async Task<ArtifactPromotionResult> ExecuteAsync(
        string runtimePrompt,
        RoadmapState state,
        ProjectContext projectContext,
        string auditPath,
        CancellationToken cancellationToken)
    {
        console.Phase(runtimePrompt);
        string selectionOrEpic = await artifacts.ReadAsync(RoadmapArtifactPaths.ActiveEpic) ?? await activeSelectionReader.ReadAsync(cancellationToken);
        string audit = await artifacts.ReadRequiredAsync(auditPath);
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = contextBuilder.BuildRealignOrReimagineContext(projection.Content, selectionOrEpic, audit);
        PromptTransitionCompletion completion = await promptTransitionRunner.RunPromotionCandidateAsync(
            state,
            RoadmapState.ActiveEpicReady,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            audit,
            [RoadmapArtifactPaths.ActiveEpic],
            cancellationToken,
            TransitionInputContext.AuditEvidence(auditPath));
        return await activeEpicPromotionCoordinator.PromoteAsync(state, runtimePrompt, projection.Definition.ProjectionPath, completion);
    }
}
