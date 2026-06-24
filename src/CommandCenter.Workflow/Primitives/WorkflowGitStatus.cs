namespace CommandCenter.Workflow.Primitives;

public enum WorkflowGitStatus
{
    NotReady,
    AwaitingCommit,
    Committed,
    AwaitingPush,
    Pushed,
    NoChangesProduced,
    PushSkipped,
    Failed
}
