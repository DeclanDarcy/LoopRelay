using LoopRelay.Core.Artifacts;

namespace LoopRelay.Core.Repositories;

/// <summary>
/// Generic, repository-scoped context shared across the different kinds of codex sessions
/// (execution, decision, and beyond). Carries the loaded repository identity, its artifact
/// contents, and an optional git snapshot — the reusable substrate. Session-kind-specific
/// concerns (e.g. execution governance/diagnostics) live on derived types.
/// </summary>
public class RepoContext
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; init; }

    public IReadOnlyList<LoadedArtifact> Artifacts { get; init; } = Array.Empty<LoadedArtifact>();

    public RepositorySnapshot? Snapshot { get; init; }
}
