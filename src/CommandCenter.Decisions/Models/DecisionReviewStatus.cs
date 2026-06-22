using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionReviewStatus(
    Guid RepositoryId,
    string ProposalId,
    DecisionReviewState State,
    DateTimeOffset UpdatedAt,
    string? Reason,
    IReadOnlyList<DecisionSourceReference> Sources);
