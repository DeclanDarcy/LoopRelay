namespace LoopRelay.Decisions.Models;

public sealed record ExecutionDecisionConflict(
    string Id,
    string DecisionId,
    string Title,
    string Statement,
    string ConflictingExcerpt,
    IReadOnlyList<DecisionSourceReference> Sources);
