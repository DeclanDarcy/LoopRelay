namespace CommandCenter.Decisions.Models;

public sealed record DecisionReviewNote(
    string Id,
    Guid RepositoryId,
    string ProposalId,
    DateTimeOffset CreatedAt,
    string Reviewer,
    string Body,
    IReadOnlyList<DecisionSourceReference> Sources);
