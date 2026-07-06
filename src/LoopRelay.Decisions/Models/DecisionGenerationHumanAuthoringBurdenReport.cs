namespace LoopRelay.Decisions.Models;

public sealed record DecisionGenerationHumanAuthoringBurdenReport(
    int DecisionCount,
    int ReviewOnlyCount,
    double ReviewOnlyRate,
    int MinorEditCount,
    double MinorEditRate,
    int MajorRefinementCount,
    double MajorRefinementRate,
    int FullRewriteCount,
    double FullRewriteRate,
    int GenerationBypassedCount,
    double GenerationBypassedRate,
    bool PrimaryAuthoringReplaced,
    IReadOnlyList<string> Diagnostics);
