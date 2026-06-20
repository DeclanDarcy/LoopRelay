namespace CommandCenter.Backend.Execution;

public sealed class ExecutionPromptMetadata
{
    public string RepositoryPath { get; init; } = string.Empty;

    public string MilestonePath { get; init; } = string.Empty;

    public IReadOnlyList<string> IncludedArtifactPaths { get; init; } = Array.Empty<string>();

    public long TotalContextBytes { get; init; }

    public long TotalContextCharacters { get; init; }

    public bool DirtyRepository { get; init; }
}
