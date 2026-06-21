using CommandCenter.Execution.Primitives;

namespace CommandCenter.Execution.Models;

public sealed class ExecutionEvent
{
    public long Sequence { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public ExecutionEventType Type { get; init; }

    public string Message { get; init; } = string.Empty;
}
