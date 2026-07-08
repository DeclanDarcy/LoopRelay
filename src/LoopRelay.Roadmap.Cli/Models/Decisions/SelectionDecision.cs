namespace LoopRelay.Roadmap.Cli.Models.Decisions;

internal sealed record SelectionDecision(
    string RecommendedOutcome,
    string RecommendedInitiative,
    string InitiativeType,
    string Confidence,
    string PrimaryReason,
    string? ExistingEpicId,
    string? ExistingEpicName);
