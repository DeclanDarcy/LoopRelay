namespace LoopRelay.Roadmap.Cli;

internal sealed class EpicPreparationAuditTransition(
    RoadmapArtifacts artifacts,
    PromptContractRegistry contractRegistry,
    ProjectionCache projectionCache,
    RoadmapPromptContextBuilder contextBuilder,
    ActiveSelectionReader activeSelectionReader,
    RoadmapPromptTransitionRunner promptTransitionRunner,
    HitlArtifactCapture hitlArtifactCapture,
    DecisionRecorder decisionRecorder,
    RoadmapStateStore stateStore,
    RoadmapTransitionPersistence transitionPersistence,
    SelectionSuperseder selectionSuperseder,
    ActiveEpicRewriteTransition activeEpicRewriteTransition,
    ILoopConsole console)
{
    public async Task<EpicPreparationResult> ExecuteAsync(
        SelectionDecision selectionDecision,
        ProjectContext projectContext,
        CancellationToken cancellationToken)
    {
        const string runtimePrompt = "EpicPreparationAudit";
        console.Phase("Audit selected epic");
        string selection = await activeSelectionReader.ReadAsync(cancellationToken);
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = contextBuilder.BuildAuditContext(projection.Content, selection);
        string output = await promptTransitionRunner.RunNormalAsync(
            RoadmapState.ExistingEpicSelected,
            RoadmapState.EpicPreparationAudit,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            selection,
            [RoadmapArtifactPaths.AuditEvidenceDirectory],
            cancellationToken);
        string auditPath = await artifacts.WriteNumberedEvidenceAsync(RoadmapArtifactPaths.AuditEvidenceDirectory, "epic-preparation-audit", output);
        await hitlArtifactCapture.CaptureAsync(auditPath, output);
        EpicPreparationAuditDecision decision = new EpicPreparationAuditParser().Parse(output);
        await decisionRecorder.AppendAsync(RoadmapState.EpicPreparationAudit, runtimePrompt, projection.Definition.ProjectionPath, auditPath, decision.Disposition, decision.Confidence, decision.RecommendedNextStep);

        if (decision.Disposition == "Retire")
        {
            RoadmapStateDocument? existing = await stateStore.LoadAsync();
            DateTimeOffset retiredAt = DateTimeOffset.UtcNow;
            RetiredEpic retired = RetiredEpic.FromSelectionAndAudit(selectionDecision, decision, auditPath, retiredAt);
            IReadOnlyList<RetiredEpic> retiredEpics = RetiredEpic.Upsert(existing?.RetiredEpics ?? [], retired);
            await decisionRecorder.AppendAsync(RoadmapState.RetireEpic, runtimePrompt, projection.Definition.ProjectionPath, auditPath, "Retired Epic", decision.Confidence, $"{retired.IdentityKind} {retired.StableIdentity}: {decision.PrimaryReason}");
            await transitionPersistence.SaveAsync(RoadmapState.RetireEpic, TransitionStatus.Completed, RoadmapState.EpicPreparationAudit, RoadmapState.RetireEpic, runtimePrompt, projection.Definition.ProjectionPath, auditPath, "Retired Epic", retiredAt, retiredAt, retiredEpics, null);
            await selectionSuperseder.SupersedeForRetiredEpicAsync();
            return EpicPreparationResult.Retired;
        }

        if (decision.Disposition == "Insufficient Evidence")
        {
            throw new RoadmapStepException("Epic preparation audit requires more evidence.");
        }

        if (decision.Disposition == "Realign")
        {
            ArtifactPromotionResult promotion = await activeEpicRewriteTransition.ExecuteAsync("RealignEpic", RoadmapState.RealignEpic, projectContext, auditPath, cancellationToken);
            return promotion.Promoted ? EpicPreparationResult.ActiveEpicReady : EpicPreparationResult.Blocked;
        }

        ArtifactPromotionResult reimaginePromotion = await activeEpicRewriteTransition.ExecuteAsync("ReimagineEpic", RoadmapState.ReimagineEpic, projectContext, auditPath, cancellationToken);
        return reimaginePromotion.Promoted ? EpicPreparationResult.ActiveEpicReady : EpicPreparationResult.Blocked;
    }
}
