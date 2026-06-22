namespace CommandCenter.Execution.Primitives;

public enum ExecutionSessionState
{
    Created,
    Executing,
    Completed,
    Failed,
    Cancelled
}
