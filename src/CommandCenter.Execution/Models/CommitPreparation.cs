namespace CommandCenter.Execution.Models;

public sealed class CommitPreparation
{
    public Guid Id { get; init; }

    public Guid SessionId { get; init; }

    public Guid RepositoryId { get; init; }

    public string RepositoryPath { get; init; } = string.Empty;

    public string ProposedMessage { get; init; } = string.Empty;

    public IReadOnlyList<CommitScopeItem> ScopeItems { get; init; } = Array.Empty<CommitScopeItem>();

    public CommitStatusSnapshot StatusSnapshot { get; init; } = new();

    public DateTimeOffset GeneratedAt { get; init; }

    public bool HasPreExistingChanges { get; init; }
}
