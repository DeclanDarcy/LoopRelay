namespace LoopRelay.Roadmap.Cli;

internal sealed record EpicPreparationAuditDecision(
    string EpicId,
    string EpicName,
    string Disposition,
    string Confidence,
    string PrimaryReason,
    string EvidenceStrength,
    string RecommendedNextStep);
