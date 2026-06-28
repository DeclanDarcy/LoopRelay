using CommandCenter.Core.Repositories;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Orchestration.Abstractions;

namespace CommandCenter.Orchestration.Services;

/// <summary>
/// Default <see cref="IPlanArtifactPublisher"/> over the Execution layer's <see cref="IGitService"/>.
/// It mirrors the established commit flow (snapshot the working tree, commit the selected paths against
/// that snapshot id, then push) without dragging an <c>ExecutionSession</c> into the orchestrator. Any
/// Git failure is captured as a failed result so a broken remote/commit boundary stays recoverable.
/// </summary>
internal sealed class GitPlanArtifactPublisher(IGitService gitService) : IPlanArtifactPublisher
{
    public async Task<PlanPublicationResult> PublishAsync(
        Repository repository,
        string commitMessage,
        IReadOnlyList<string> repositoryRelativePaths,
        CancellationToken cancellationToken = default)
    {
        try
        {
            CommitStatusSnapshot snapshot = await gitService.GetCommitStatusSnapshotAsync(repository).ConfigureAwait(false);
            CommitResult commit = await gitService
                .CommitAsync(repository, commitMessage, repositoryRelativePaths, snapshot.Id)
                .ConfigureAwait(false);
            PushResult push = await gitService.PushAsync(repository, commit.CommitSha).ConfigureAwait(false);
            return PlanPublicationResult.Success(commit.CommitSha, pushed: push.PushedCommitSha is not null);
        }
        catch (Exception exception)
        {
            return PlanPublicationResult.Failed(exception.Message);
        }
    }
}
