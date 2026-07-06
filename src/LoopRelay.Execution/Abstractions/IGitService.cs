using LoopRelay.Core.Repositories;
using LoopRelay.Execution.Models;

namespace LoopRelay.Execution.Abstractions;

public interface IGitService
{
    Task<RepositorySnapshot> GetSnapshotAsync(Repository repository);

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
