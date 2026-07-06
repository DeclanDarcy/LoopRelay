namespace LoopRelay.Execution.Primitives;

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
