namespace LoopRelay.Decisions.Models;

public sealed record DecisionTradeoffAnalysisDiagnostics(
    int AnalyzedOptionCount,
    string ContextFingerprint,
    IReadOnlyList<string> Unknowns,
    IReadOnlyList<string> ValidationWarnings,
    IReadOnlyList<string> Diagnostics);
