namespace LoopRelay.Decisions.Models;

public sealed record DecisionReviewDiagnostics(
    bool HasRecommendation,
    bool HasEvidence,
    int OptionCount,
    int TradeoffCount,
    int AssumptionCount,
    int NoteCount,
    IReadOnlyList<string> Warnings);
