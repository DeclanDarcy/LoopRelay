namespace CommandCenter.Backend.Execution;

public enum ExecutionEventType
{
    Info,
    StdOut,
    StdErr,
    ProviderStarted,
    ProviderExited,
    Failure,
    Recovery
}
