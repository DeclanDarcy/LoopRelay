namespace CommandCenter.Backend.Execution;

public sealed class ExecutionContext
{
    public Guid RepositoryId { get; init; }

    public string RepositoryName { get; init; } = string.Empty;

    public string RepositoryPath { get; init; } = string.Empty;

    public string MilestonePath { get; init; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; init; }

    public IReadOnlyList<ExecutionContextArtifact> Artifacts { get; init; } = Array.Empty<ExecutionContextArtifact>();

    public ExecutionRepositorySnapshot? RepositorySnapshot { get; init; }

    public ExecutionContextDiagnostics Diagnostics { get; init; } = new();
}
