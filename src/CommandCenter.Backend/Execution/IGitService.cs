namespace CommandCenter.Backend.Execution;

using CommandCenter.Backend.Repositories;

public interface IGitService
{
    Task<ExecutionRepositorySnapshot> GetSnapshotAsync(Repository repository);

    Task<RepositoryGitStatus> GetStatusAsync(Repository repository);

    Task<CommitPreparation> PrepareCommitAsync(Repository repository, ExecutionSession session);

    Task<CommitStatusSnapshot> GetCommitStatusSnapshotAsync(Repository repository);

    Task<CommitResult> CommitAsync(
        Repository repository,
        string message,
        IReadOnlyList<string> selectedPaths,
        string preparationSnapshotId);

    Task<PushResult> PushAsync(Repository repository, string? commitSha);
}
