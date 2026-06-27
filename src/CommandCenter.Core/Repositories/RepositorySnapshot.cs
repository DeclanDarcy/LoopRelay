namespace CommandCenter.Core.Repositories;

/// <summary>
/// A point-in-time git snapshot of a repository (branch + working-tree dirty state).
/// Repository-scoped and reusable across the different kinds of Codex sessions — not
/// execution-specific.
/// </summary>
public sealed class RepositorySnapshot
{
    public string Branch { get; init; } = string.Empty;

    public RepositoryDirtyState DirtyState { get; init; } = new();

    public DateTimeOffset CapturedAt { get; init; }
}
