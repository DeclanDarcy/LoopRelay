namespace LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;

internal sealed record EpicPreparationAuditDecision(
    string EpicId,
    string EpicName,
    string Disposition,
    string Confidence,
    string PrimaryReason,
    string EvidenceStrength,
    string RecommendedNextStep);
