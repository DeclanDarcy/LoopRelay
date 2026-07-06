using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionReviewStatus(
    Guid RepositoryId,
    string ProposalId,
    DecisionReviewState State,
    DateTimeOffset UpdatedAt,
    string? Reason,
    IReadOnlyList<DecisionSourceReference> Sources);
