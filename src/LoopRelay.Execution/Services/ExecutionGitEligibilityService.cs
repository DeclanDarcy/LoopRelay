using LoopRelay.Core.Repositories;
using LoopRelay.Execution.Abstractions;
using LoopRelay.Execution.Models;
using LoopRelay.Execution.Primitives;

namespace LoopRelay.Execution.Services;

public sealed class ExecutionGitEligibilityService(
    IExecutionSessionStore sessionStore,
    IGitService gitService) : IExecutionGitEligibilityService
{
    public async Task<ExecutionGitActionEligibility> GetEligibilityAsync(
        Guid sessionId,
        ExecutionGitActionEligibilityRequest request)
    {
        ExecutionSession session = (await sessionStore.LoadAsync())
                                   .FirstOrDefault(session => session.Id == sessionId)
                                   ?? throw new KeyNotFoundException($"Execution session was not found: {sessionId}");

        var repository = new Repository
        {
            Id = session.RepositoryId,
            Name = Path.GetFileName(session.RepositoryPath),
            Path = session.RepositoryPath
        };

        var diagnostics = new List<string>();
        CommitStatusSnapshot? currentSnapshot = null;
        RepositoryGitStatus? gitStatus = null;

        if (session.RepositoryState == RepositoryExecutionState.AwaitingCommit)
        {
            try
            {
                currentSnapshot = await gitService.GetCommitStatusSnapshotAsync(repository);
            }
            catch (InvalidOperationException exception)
            {
                diagnostics.Add($"Commit status snapshot unavailable: {exception.Message}");
            }
        }

        if (session.RepositoryState == RepositoryExecutionState.AwaitingPush ||
            session.RepositoryState == RepositoryExecutionState.Ready)
        {
            try
            {
                gitStatus = await gitService.GetStatusAsync(repository);
            }
            catch (InvalidOperationException exception)
            {
                diagnostics.Add($"Remote branch state unavailable: {exception.Message}");
            }
        }

        CommitPreparation? preparation = session.CommitPreparation;
        bool preparationLoaded = preparation is not null;
        bool preparationCurrent = preparationLoaded &&
            currentSnapshot is not null &&
            string.Equals(
                currentSnapshot.Id,
                preparation!.StatusSnapshot.Id,
                StringComparison.Ordinal);
        IReadOnlyList<string> selectedPaths = NormalizeSelectedPaths(request.SelectedPaths);
        HashSet<string> preparedPaths = preparation?.ScopeItems
            .Select(item => item.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        string[] unknownSelectedPaths = selectedPaths
            .Where(path => !preparedPaths.Contains(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        bool repositoryAllowsCommit = session.RepositoryState == RepositoryExecutionState.AwaitingCommit;
        bool commitMessagePresent = !string.IsNullOrWhiteSpace(request.CommitMessage);
        bool awaitingPush = session.RepositoryState == RepositoryExecutionState.AwaitingPush;
        bool commitShaExists = !string.IsNullOrWhiteSpace(session.CommitSha);

        List<string> commitDisabledReasons = BuildCommitDisabledReasons(
            repositoryAllowsCommit,
            preparationLoaded,
            preparationCurrent,
            selectedPaths.Count,
            unknownSelectedPaths,
            commitMessagePresent,
            diagnostics);
        List<string> pushDisabledReasons = BuildPushDisabledReasons(
            awaitingPush,
            commitShaExists,
            gitStatus);

        return new ExecutionGitActionEligibility
        {
            SessionId = session.Id,
            SessionExists = true,
            RepositoryState = session.RepositoryState,
            CommitPreparationLoaded = preparationLoaded,
            CommitPreparationCurrent = preparationCurrent,
            CommitPreparationId = preparation?.Id,
            PreparedStatusSnapshotId = preparation?.StatusSnapshot.Id,
            CurrentStatusSnapshotId = currentSnapshot?.Id,
            SelectedPathCount = selectedPaths.Count,
            PreparedPathCount = preparation?.ScopeItems.Count ?? 0,
            UnknownSelectedPaths = unknownSelectedPaths,
            CommitMessagePresent = commitMessagePresent,
            RepositoryAllowsCommit = repositoryAllowsCommit,
            AwaitingPush = awaitingPush,
            CommitShaExists = commitShaExists,
            CommitSha = session.CommitSha,
            PreviousPushAttemptedAt = session.PushAttemptedAt,
            PreviousPushFailure = session.FailureReason,
            RemoteBranchState = gitStatus is null ? null : new ExecutionGitRemoteBranchState
            {
                Branch = gitStatus.Branch,
                AheadCount = gitStatus.AheadCount,
                BehindCount = gitStatus.BehindCount,
                HasUnpushedChanges = gitStatus.AheadCount > 0,
                HasRemoteDivergence = gitStatus.BehindCount > 0,
                CapturedAt = gitStatus.CapturedAt
            },
            CanCommit = commitDisabledReasons.Count == 0,
            CanPush = pushDisabledReasons.Count == 0,
            CommitDisabledReasons = commitDisabledReasons,
            PushDisabledReasons = pushDisabledReasons,
            Diagnostics = diagnostics
        };
    }

    private static IReadOnlyList<string> NormalizeSelectedPaths(IEnumerable<string>? selectedPaths)
    {
        return (selectedPaths ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Replace('\\', '/').Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static List<string> BuildCommitDisabledReasons(
        bool repositoryAllowsCommit,
        bool preparationLoaded,
        bool preparationCurrent,
        int selectedPathCount,
        IReadOnlyList<string> unknownSelectedPaths,
        bool commitMessagePresent,
        IReadOnlyList<string> diagnostics)
    {
        var reasons = new List<string>();
        if (!repositoryAllowsCommit)
        {
            reasons.Add("Repository is not awaiting commit.");
        }

        if (!preparationLoaded)
        {
            reasons.Add("Commit preparation is not loaded.");
        }
        else if (!preparationCurrent)
        {
            reasons.Add("Commit preparation is stale.");
        }

        if (selectedPathCount == 0)
        {
            reasons.Add("At least one path must be selected for commit.");
        }

        if (unknownSelectedPaths.Count > 0)
        {
            reasons.Add("Selected paths include entries outside the prepared commit scope.");
        }

        if (!commitMessagePresent)
        {
            reasons.Add("Commit message is required.");
        }

        if (diagnostics.Any(diagnostic => diagnostic.StartsWith("Commit status snapshot unavailable:", StringComparison.Ordinal)))
        {
            reasons.Add("Current commit status snapshot is unavailable.");
        }

        return reasons;
    }

    private static List<string> BuildPushDisabledReasons(
        bool awaitingPush,
        bool commitShaExists,
        RepositoryGitStatus? gitStatus)
    {
        var reasons = new List<string>();
        if (!awaitingPush)
        {
            reasons.Add("Repository is not awaiting push.");
        }

        if (!commitShaExists)
        {
            reasons.Add("Committed execution SHA is not recorded.");
        }

        if (gitStatus is null)
        {
            reasons.Add("Remote branch state is not loaded.");
        }
        else if (gitStatus.BehindCount > 0)
        {
            reasons.Add("Remote branch has new commits; review branch state before pushing.");
        }

        return reasons;
    }
}
