using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Services.Transitions;

internal sealed class ActiveEpicPromotionCoordinator(
    ArtifactPromotionService promotionService,
    HitlArtifactCapture hitlArtifactCapture,
    TransitionJournalStore journalStore,
    RoadmapTransitionPersistence transitionPersistence)
{
    public async Task<ArtifactPromotionResult> PromoteAsync(
        RoadmapState from,
        string prompt,
        string projectionPath,
        PromptTransitionCompletion completion,
        string? lifecycleNotes = null)
    {
        ArtifactPromotionResult result = await promotionService.PromoteAsync(new ArtifactPromotionRequest(
            RoadmapArtifactPaths.ActiveEpic,
            completion.Output,
            RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "active-epic-promotion",
            "active epic",
            new EpicAuthoringOutputClassifier(),
            new EpicArtifactValidator(),
            ArtifactLifecycleState.Ready,
            lifecycleNotes ?? $"Promoted by {prompt}."));

        DateTimeOffset completed = DateTimeOffset.UtcNow;
        if (result.Promoted)
        {
            await hitlArtifactCapture.CaptureAsync(RoadmapArtifactPaths.ActiveEpic, completion.Output);
            await journalStore.AppendAsync(new TransitionJournalRecord(
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
            await transitionPersistence.SaveAsync(
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

        await journalStore.AppendAsync(new TransitionJournalRecord(
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
        await transitionPersistence.SaveAsync(
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
