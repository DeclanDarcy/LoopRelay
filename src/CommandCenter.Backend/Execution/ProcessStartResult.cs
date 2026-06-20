namespace CommandCenter.Backend.Execution;

public sealed class ProcessStartResult
{
    public int ProcessId { get; init; }

    public bool HasExited { get; init; }

    public int? ExitCode { get; init; }
}
