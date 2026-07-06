namespace LoopRelay.Decisions.Models;

public sealed record DecisionGenerationExecutiveReport(
    bool ReplacementReady,
    string Answer,
    string Summary,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> BlockingGaps,
    IReadOnlyList<string> Diagnostics);
