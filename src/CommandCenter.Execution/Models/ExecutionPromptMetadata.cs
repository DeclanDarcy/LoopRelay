namespace CommandCenter.Execution.Models;

public sealed class ExecutionPromptMetadata
{
    public DateTimeOffset GeneratedAt { get; init; }

    public string RepositoryPath { get; init; } = string.Empty;

    public IReadOnlyList<string> IncludedArtifactPaths { get; init; } = Array.Empty<string>();

    public long TotalContextBytes { get; init; }

    public long TotalContextCharacters { get; init; }

    public bool DirtyRepository { get; init; }
}
