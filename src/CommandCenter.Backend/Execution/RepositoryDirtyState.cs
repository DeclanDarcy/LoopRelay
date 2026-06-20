namespace CommandCenter.Backend.Execution;

public sealed class RepositoryDirtyState
{
    public IReadOnlyList<string> StagedPaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ModifiedPaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AddedPaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> DeletedPaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RenamedPaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> UntrackedPaths { get; init; } = Array.Empty<string>();

    public bool IsClean { get; init; } = true;
}
