namespace LoopRelay.Execution.Primitives;

public enum ExecutionEventType
{
    Info,
    StdOut,
    StdErr,
    ProviderStarted,
    ProviderExited,
    HandoffValidated,
    GitCommitPreparationCreated,
    GitCommitSucceeded,
    GitPushAttempted,
    GitPushSucceeded,
    GitPushFailed,
    Failure,
    Cancellation,
    Recovery
}
