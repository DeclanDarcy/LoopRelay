namespace LoopRelay.Execution.Models;

public sealed class ImplementationExecutionContextArtifactDiagnostic
{
    public string Role { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public long ByteCount { get; init; }

    public long CharacterCount { get; init; }

    public long WarningThresholdBytes { get; init; }

    public long HardLimitBytes { get; init; }

    public bool WarningThresholdExceeded { get; init; }

    public bool HardLimitExceeded { get; init; }
}
