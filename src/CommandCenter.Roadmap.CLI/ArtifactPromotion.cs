namespace CommandCenter.Roadmap.Cli;

internal interface IArtifactOutputClassifier
{
    ArtifactOutputClassification Classify(string content);
}

internal interface IArtifactValidator
{
    ArtifactValidationResult Validate(string content);
}

internal enum ArtifactOutputKind
{
    Promotable,
    Blocked,
    Malformed,
    Ambiguous,
}

internal sealed record ArtifactOutputClassification(
    ArtifactOutputKind Kind,
    string Reason);

internal sealed record ArtifactValidationResult(
    bool IsValid,
    string? Error)
{
    public static ArtifactValidationResult Valid() => new(true, null);

    public static ArtifactValidationResult Invalid(string error) => new(false, error);
}

internal enum ArtifactPromotionStatus
{
    Promoted,
    Blocked,
    StructurallyInvalid,
    Ambiguous,
}

internal sealed record ArtifactPromotionRequest(
    string TargetPath,
    string CandidateContent,
    string EvidenceDirectory,
    string EvidenceStem,
    string ArtifactName,
    IArtifactOutputClassifier Classifier,
    IArtifactValidator Validator,
    ArtifactLifecycleState PromotedLifecycleState,
    string LifecycleNotes);

internal sealed record ArtifactPromotionResult(
    ArtifactPromotionStatus Status,
    string TargetPath,
    string? EvidencePath,
    string Reason)
{
    public bool Promoted => Status == ArtifactPromotionStatus.Promoted;

    public static ArtifactPromotionResult PromotedResult(string targetPath) =>
        new(ArtifactPromotionStatus.Promoted, targetPath, null, "Artifact promoted.");

    public static ArtifactPromotionResult NotPromoted(
        ArtifactPromotionStatus status,
        string targetPath,
        string evidencePath,
        string reason) =>
        new(status, targetPath, evidencePath, reason);
}

internal sealed class ArtifactPromotionService(RoadmapArtifacts artifacts, ArtifactLifecycleStore lifecycleStore)
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

        await artifacts.WriteAsync(request.TargetPath, request.CandidateContent);
        await lifecycleStore.UpsertAsync(
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
        string evidencePath = await artifacts.WriteNumberedEvidenceAsync(
            request.EvidenceDirectory,
            request.EvidenceStem,
            request.CandidateContent);
        await lifecycleStore.UpsertAsync(evidencePath, ArtifactLifecycleState.Blocked, reason);

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
