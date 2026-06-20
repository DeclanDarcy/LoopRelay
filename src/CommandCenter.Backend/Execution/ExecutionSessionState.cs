namespace CommandCenter.Backend.Execution;

public enum ExecutionSessionState
{
    Created,
    Executing,
    Completed,
    Failed,
    Cancelled
}
