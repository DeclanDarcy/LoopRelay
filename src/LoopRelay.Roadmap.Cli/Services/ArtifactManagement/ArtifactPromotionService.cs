using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Services.Artifacts;

namespace LoopRelay.Roadmap.Cli.Services.ArtifactManagement;

internal sealed class ArtifactPromotionService(RoadmapArtifacts _artifacts, IArtifactLifecycleStore _lifecycleStore)
{
    public async Task<ArtifactPromotionResult> PromoteAsync(ArtifactPromotionRequest request)
    {
        ArtifactOutputClassification classification = request.Classifier.Classify(request.CandidateContent);
        if (classification.Kind != ArtifactOutputKind.Promotable)
        {
            return await PreserveEvidenceAsync(
                request,
                ToPromotionStatus(classification.Kind),
                classification.Reason);
        }

        ArtifactValidationResult validation = request.Validator.Validate(request.CandidateContent);
        if (!validation.IsValid)
        {
            return await PreserveEvidenceAsync(
                request,
                ArtifactPromotionStatus.StructurallyInvalid,
                validation.Error ?? $"{request.ArtifactName} failed validation.");
        }

        await _artifacts.WriteAsync(request.TargetPath, request.CandidateContent);
        await _lifecycleStore.UpsertAsync(
            request.TargetPath,
            request.PromotedLifecycleState,
            request.LifecycleNotes);

        return ArtifactPromotionResult.PromotedResult(request.TargetPath);
    }

    private async Task<ArtifactPromotionResult> PreserveEvidenceAsync(
        ArtifactPromotionRequest request,
        ArtifactPromotionStatus status,
        string reason)
    {
        string evidencePath = await _artifacts.WriteNumberedEvidenceAsync(
            request.EvidenceDirectory,
            request.EvidenceStem,
            request.CandidateContent);
        await _lifecycleStore.UpsertAsync(evidencePath, ArtifactLifecycleState.Blocked, reason);

        return ArtifactPromotionResult.NotPromoted(
            status,
            request.TargetPath,
            evidencePath,
            reason);
    }

    private static ArtifactPromotionStatus ToPromotionStatus(ArtifactOutputKind kind) =>
        kind switch
        {
            ArtifactOutputKind.Blocked => ArtifactPromotionStatus.Blocked,
            ArtifactOutputKind.Malformed => ArtifactPromotionStatus.StructurallyInvalid,
            ArtifactOutputKind.Ambiguous => ArtifactPromotionStatus.Ambiguous,
            _ => ArtifactPromotionStatus.StructurallyInvalid,
        };
}
