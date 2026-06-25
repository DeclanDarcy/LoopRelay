namespace CommandCenter.Decisions.Models;

public sealed record HumanAuthoringBurdenReport(
    Guid RepositoryId,
    int DecisionCount,
    int ReviewOnlyCount,
    int MinorEditCount,
    int MajorRefinementCount,
    int FullRewriteCount,
    int GenerationBypassedCount,
    int UnknownCount,
    IReadOnlyList<HumanAuthoringBurdenSignal> Signals,
    IReadOnlyList<HumanAuthoringBurdenExplanation>? DecisionExplanations = null);
