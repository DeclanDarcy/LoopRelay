namespace LoopRelay.Execution.Models;

public sealed class ImplementationExecutionContextDiagnostics
{
    public long TotalBytes { get; init; }

    public long TotalCharacters { get; init; }

    public long WarningThresholdBytes { get; init; }

    public long HardLimitBytes { get; init; }

    public bool WarningThresholdExceeded { get; init; }

    public bool HardLimitExceeded { get; init; }

    public IReadOnlyList<ImplementationExecutionContextArtifactDiagnostic> ArtifactDiagnostics { get; init; } =
        Array.Empty<ImplementationExecutionContextArtifactDiagnostic>();

    public IReadOnlyList<string> ValidationErrors { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ExecutionGovernedConflictDiagnostic> GovernedConflicts { get; init; } =
        Array.Empty<ExecutionGovernedConflictDiagnostic>();

    public IReadOnlyList<string> MissingOptionalArtifacts { get; init; } = Array.Empty<string>();

    public bool LaunchBlocked { get; init; }
}
