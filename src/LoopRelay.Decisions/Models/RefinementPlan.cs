namespace LoopRelay.Decisions.Models;

public sealed record RefinementPlan(
    Guid RepositoryId,
    string ProposalId,
    DateTimeOffset AnalyzedAt,
    string BaseProposalFingerprint,
    IReadOnlyList<RefinementDirective> Directives,
    bool RegenerateOptions,
    bool ReevaluateTradeoffs,
    bool ReevaluateRecommendation,
    bool FullRegeneration,
    IReadOnlyList<string> AppliedConstraints,
    IReadOnlyList<string> Diagnostics);
