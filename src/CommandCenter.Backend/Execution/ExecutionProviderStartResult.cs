namespace CommandCenter.Backend.Execution;

public sealed class ExecutionProviderStartResult
{
    public string ProviderName { get; init; } = string.Empty;

    public string? ExecutablePath { get; init; }

    public int? ProcessId { get; init; }

    public DateTimeOffset StartedAt { get; init; }
}
