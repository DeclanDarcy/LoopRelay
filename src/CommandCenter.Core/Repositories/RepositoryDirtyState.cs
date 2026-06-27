namespace CommandCenter.Core.Repositories;

/// <summary>
/// The working-tree dirty state of a repository. Repository-scoped and reusable across the
/// different kinds of Codex sessions — not execution-specific.
/// </summary>
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
