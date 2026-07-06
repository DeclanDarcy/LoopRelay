namespace LoopRelay.Execution.Primitives;

public static class ImplementationExecutionContextSizePolicy
{
    public const long AggregateWarningThresholdBytes = 128 * 1024;

    public const long AggregateHardLimitBytes = 512 * 1024;

    public const long ArtifactWarningThresholdBytes = 96 * 1024;

    public const long ArtifactHardLimitBytes = 256 * 1024;
}
