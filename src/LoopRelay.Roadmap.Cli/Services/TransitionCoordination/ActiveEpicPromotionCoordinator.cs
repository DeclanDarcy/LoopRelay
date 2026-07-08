using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.TransitionState;

namespace LoopRelay.Roadmap.Cli.Services.TransitionCoordination;

internal sealed class ActiveEpicPromotionCoordinator(
    ArtifactPromotionService promotionService,
    HitlArtifactCapture hitlArtifactCapture,
    TransitionJournalStore journalStore,
    RoadmapTransitionPersistence transitionPersistence)
{
    private readonly ArtifactPromotionService _promotionService = promotionService;
    private readonly HitlArtifactCapture _hitlArtifactCapture = hitlArtifactCapture;
    private readonly TransitionJournalStore _journalStore = journalStore;
    private readonly RoadmapTransitionPersistence _transitionPersistence = transitionPersistence;
    public async Task<ArtifactPromotionResult> PromoteAsync(
        RoadmapState from,
        string prompt,
        string projectionPath,
        PromptTransitionCompletion completion,
        string? lifecycleNotes = null)
    {
        ArtifactPromotionResult result = await _promotionService.PromoteAsync(new ArtifactPromotionRequest(
            RoadmapArtifactPaths.ActiveEpic,
            completion.Output,
            RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "active-epic-promotion",
            "active epic",
            new ArtifactManagement.EpicAuthoringOutputClassifier(),
            new EpicArtifactValidator(),
            ArtifactLifecycleState.Ready,
            lifecycleNotes ?? $"Promoted by {prompt}."));

        DateTimeOffset completed = DateTimeOffset.UtcNow;
        if (result.Promoted)
        {
            await _hitlArtifactCapture.CaptureAsync(RoadmapArtifactPaths.ActiveEpic, completion.Output);
            await _journalStore.AppendAsync(new TransitionJournalRecord(
                "ArtifactPromoted",
                completion.CorrelationId,
                completed,
                from,
                RoadmapState.ActiveEpicReady,
                prompt,
                projectionPath,
                "ArtifactPromotionService",
                completion.InputSnapshot.ToInputArtifactHashes(),
                [RoadmapArtifactPaths.ActiveEpic],
                completion.ElapsedMilliseconds,
                "Promoted",
                "Active epic promoted",
                null,
                completion.InputSnapshot));
            await _transitionPersistence.SaveAsync(
                RoadmapState.ActiveEpicReady,
                TransitionStatus.Completed,
                from,
                RoadmapState.ActiveEpicReady,
                prompt,
                projectionPath,
                RoadmapArtifactPaths.ActiveEpic,
                "Artifact Promoted",
                completion.Started,
                completed,
                null,
                null);
            return result;
        }

        string evidencePath = result.EvidencePath ?? RoadmapArtifactPaths.BlockerEvidenceDirectory;
        string decision = result.Status switch
        {
            ArtifactPromotionStatus.Blocked => "Artifact Promotion Blocked",
            ArtifactPromotionStatus.Ambiguous => "Artifact Promotion Ambiguous",
            ArtifactPromotionStatus.StructurallyInvalid => "Artifact Promotion Invalid",
            _ => "Artifact Promotion Rejected",
        };

        await _journalStore.AppendAsync(new TransitionJournalRecord(
            "ArtifactPromotionBlocked",
            completion.CorrelationId,
            completed,
            from,
            RoadmapState.ActiveEpicReady,
            prompt,
            projectionPath,
            "ArtifactPromotionService",
            completion.InputSnapshot.ToInputArtifactHashes(),
            [evidencePath],
            completion.ElapsedMilliseconds,
            result.Status.ToString(),
            decision,
            result.Reason,
            completion.InputSnapshot));
        await _transitionPersistence.SaveAsync(
            RoadmapState.EvidenceBlocked,
            TransitionStatus.Paused,
            from,
            RoadmapState.ActiveEpicReady,
            prompt,
            projectionPath,
            evidencePath,
            decision,
            completion.Started,
            completed,
            null,
            [new BlockerRow(result.Reason, $"Review {evidencePath} and rerun the roadmap CLI after resolving the blocker.")],
            new RoadmapTransitionIntent("ResolveArtifactPromotionBlocker", RoadmapState.EvidenceBlocked, [evidencePath]),
            ["Resolve blocker and rerun"]);
        return result;
    }
}
