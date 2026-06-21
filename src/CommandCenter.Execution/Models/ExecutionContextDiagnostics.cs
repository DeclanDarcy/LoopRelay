namespace CommandCenter.Execution.Models;

public sealed class ExecutionContextDiagnostics
{
    public long TotalBytes { get; init; }

    public long TotalCharacters { get; init; }

    public long WarningThresholdBytes { get; init; }

    public long HardLimitBytes { get; init; }

    public bool WarningThresholdExceeded { get; init; }

    public bool HardLimitExceeded { get; init; }

    public IReadOnlyList<ExecutionContextArtifactDiagnostic> ArtifactDiagnostics { get; init; } =
        Array.Empty<ExecutionContextArtifactDiagnostic>();

    public IReadOnlyList<string> ValidationErrors { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> MissingOptionalArtifacts { get; init; } = Array.Empty<string>();

    public bool LaunchBlocked { get; init; }
}
