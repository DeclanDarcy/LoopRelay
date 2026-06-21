namespace CommandCenter.Execution.Primitives;

public enum ExecutionEventType
{
    Info,
    StdOut,
    StdErr,
    ProviderStarted,
    ProviderExited,
    HandoffValidated,
    Failure,
    Cancellation,
    Recovery
}
