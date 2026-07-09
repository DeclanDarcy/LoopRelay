using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Models.Invocation;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;

namespace LoopRelay.Roadmap.Cli.Services.EpicTransitions;

internal sealed class EpicPreparationAuditTransition(
    RoadmapArtifacts _artifacts,
    PromptContractRegistry _contractRegistry,
    ProjectionCache _projectionCache,
    RoadmapPromptContextBuilder _contextBuilder,
    ActiveSelectionReader _activeSelectionReader,
    RoadmapPromptTransitionRunner _promptTransitionRunner,
    HitlArtifactCapture _hitlArtifactCapture,
    DecisionRecorder _decisionRecorder,
    IRoadmapStateStore _stateStore,
    RoadmapTransitionPersistence _transitionPersistence,
    SelectionSuperseder _selectionSuperseder,
    ActiveEpicRewriteTransition _activeEpicRewriteTransition,
    ILoopConsole _console)
{
    public async Task<EpicPreparationResult> ExecuteAsync(
        SelectionDecision selectionDecision,
        ProjectContext projectContext,
        CancellationToken cancellationToken)
    {
        const string runtimePrompt = "EpicPreparationAudit";
        _console.Phase("Audit selected epic");
        string selection = await _activeSelectionReader.ReadAsync(cancellationToken);
        PromptContract contract = _contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await _projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = _contextBuilder.BuildAuditContext(projection.Content, selection);
        string output = await _promptTransitionRunner.RunNormalAsync(
            RoadmapState.ExistingEpicSelected,
            RoadmapState.EpicPreparationAudit,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            selection,
            [RoadmapArtifactPaths.AuditEvidenceDirectory],
            cancellationToken);
        string auditPath = await _artifacts.WriteNumberedEvidenceAsync(RoadmapArtifactPaths.AuditEvidenceDirectory, "epic-preparation-audit", output);
        await _hitlArtifactCapture.CaptureAsync(auditPath, output);
        EpicPreparationAuditDecision decision = new EpicPreparationAuditParser().Parse(output);
        await _decisionRecorder.AppendAsync(RoadmapState.EpicPreparationAudit, runtimePrompt, projection.Definition.ProjectionPath, auditPath, decision.Disposition, decision.Confidence, decision.RecommendedNextStep);

        if (decision.Disposition == "Retire")
        {
            RoadmapStateDocument? existing = await _stateStore.LoadAsync();
            DateTimeOffset retiredAt = DateTimeOffset.UtcNow;
            RetiredEpic retired = RetiredEpic.FromSelectionAndAudit(selectionDecision, decision, auditPath, retiredAt);
            IReadOnlyList<RetiredEpic> retiredEpics = RetiredEpic.Upsert(existing?.RetiredEpics ?? [], retired);
            await _decisionRecorder.AppendAsync(RoadmapState.RetireEpic, runtimePrompt, projection.Definition.ProjectionPath, auditPath, "Retired Epic", decision.Confidence, $"{retired.IdentityKind} {retired.StableIdentity}: {decision.PrimaryReason}");
            await _transitionPersistence.SaveAsync(RoadmapState.RetireEpic, TransitionStatus.Completed, RoadmapState.EpicPreparationAudit, RoadmapState.RetireEpic, runtimePrompt, projection.Definition.ProjectionPath, auditPath, "Retired Epic", retiredAt, retiredAt, retiredEpics, null);
            await _selectionSuperseder.SupersedeForRetiredEpicAsync();
            return EpicPreparationResult.Retired;
        }

        if (decision.Disposition == "Insufficient Evidence")
        {
            throw new RoadmapStepException("Epic preparation audit requires more evidence.");
        }

        if (decision.Disposition == "Realign")
        {
            ArtifactPromotionResult promotion = await _activeEpicRewriteTransition.ExecuteAsync("RealignEpic", RoadmapState.RealignEpic, projectContext, auditPath, cancellationToken);
            return promotion.Promoted ? EpicPreparationResult.ActiveEpicReady : EpicPreparationResult.Blocked;
        }

        ArtifactPromotionResult reimaginePromotion = await _activeEpicRewriteTransition.ExecuteAsync("ReimagineEpic", RoadmapState.ReimagineEpic, projectContext, auditPath, cancellationToken);
        return reimaginePromotion.Promoted ? EpicPreparationResult.ActiveEpicReady : EpicPreparationResult.Blocked;
    }
}
