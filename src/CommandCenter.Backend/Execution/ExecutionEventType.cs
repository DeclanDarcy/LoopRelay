namespace CommandCenter.Backend.Execution;

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
