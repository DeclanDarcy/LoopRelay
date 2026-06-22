namespace CommandCenter.Decisions.Models;

public sealed record DecisionProposalRevision(
    string Id,
    Guid RepositoryId,
    string ProposalId,
    DateTimeOffset CreatedAt,
    string Reason,
    IReadOnlyList<string> ChangedFields,
    string SourceProposalFingerprint,
    IReadOnlyList<DecisionSourceReference> Sources);
