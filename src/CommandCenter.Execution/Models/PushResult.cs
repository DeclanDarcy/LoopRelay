namespace CommandCenter.Execution.Models;

public sealed class PushResult
{
    public DateTimeOffset PushAttemptedAt { get; init; }

    public DateTimeOffset PushedAt { get; init; }

    public string? PushedCommitSha { get; init; }

    public string? RemoteName { get; init; }

    public string? BranchName { get; init; }
}
