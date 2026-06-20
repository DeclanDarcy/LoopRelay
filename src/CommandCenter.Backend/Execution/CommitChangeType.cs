namespace CommandCenter.Backend.Execution;

public enum CommitChangeType
{
    Staged,
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked
}
