namespace CommandCenter.Backend.Execution;

public enum RepositoryExecutionState
{
    Ready,
    Executing,
    AwaitingAcceptance,
    Accepted,
    AwaitingCommit,
    AwaitingPush,
    Failed,
    Cancelled
}
