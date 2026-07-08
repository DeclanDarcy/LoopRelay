namespace LoopRelay.Roadmap.Cli;

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
