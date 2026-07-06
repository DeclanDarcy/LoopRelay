namespace LoopRelay.Execution.Primitives;

public enum CommitChangeType
{
    Staged,
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked
}
