namespace CommandCenter.Execution.Models;

public sealed class ExecutionEventRetentionPolicy
{
    public const int DefaultMaximumEventCount = 200;
    public const int DefaultMaximumEventBytes = 64 * 1024;

    public int MaximumEventCount { get; init; } = DefaultMaximumEventCount;

    public int MaximumEventBytes { get; init; } = DefaultMaximumEventBytes;
}
