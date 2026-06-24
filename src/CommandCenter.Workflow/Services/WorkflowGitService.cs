using CommandCenter.Core.Repositories;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Primitives;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowGitService(
    IRepositoryService repositoryService,
    IExecutionSessionService executionSessionService,
    IGitService gitService) : IWorkflowGitService
{
    public async Task<WorkflowGitProjection> ProjectGitAsync(
        Guid repositoryId,
        WorkflowExecutionProjection execution,
        WorkflowOperationalContextProjection operationalContext)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        RepositoryExecutionState repositoryState = await executionSessionService.GetRepositoryStateAsync(repositoryId);
        ExecutionSessionSummary? summary = await executionSessionService.GetRepositorySessionSummaryAsync(repositoryId);
        RepositoryGitStatus gitStatus = await gitService.GetStatusAsync(repository);

        List<string> includedEvidence =
        [
            $"repository-execution-state:{repositoryState}",
            $"git-status:{gitStatus.Branch}:ahead={gitStatus.AheadCount}:behind={gitStatus.BehindCount}:clean={gitStatus.DirtyState.IsClean}:captured={gitStatus.CapturedAt:O}",
            $"execution:{execution.ExecutionId?.ToString() ?? "none"}:{execution.Status}:changes={execution.HasChanges}:commit={execution.CommitSha ?? "none"}:push={execution.PushedCommitSha ?? "none"}",
            $"operational-context:{operationalContext.ProposalId ?? "none"}:{operationalContext.Status}:commitEligible={operationalContext.IsCommitEligible}"
        ];
        List<string> missingEvidence = [];
        List<string> commitSignals = [];
        List<string> pushSignals = [];
        List<string> reasoning = [];
        List<string> conflicts = [];

        if (summary is null)
        {
            missingEvidence.Add("No execution session summary exists for git workflow projection.");
        }
        else
        {
            includedEvidence.Add($"execution-session:{summary.SessionId}:{summary.State}:{summary.RepositoryState}:preparation={summary.PreparationSnapshotId ?? "none"}");
        }

        if (summary is not null && summary.RepositoryState != repositoryState)
        {
            conflicts.Add($"Execution repository state is {repositoryState}, but session reports {summary.RepositoryState}.");
        }

        bool hasPendingChanges = !gitStatus.DirtyState.IsClean || repositoryState is RepositoryExecutionState.AwaitingCommit;
        bool hasCommitEvidence = execution.CommittedAt is not null ||
            !string.IsNullOrWhiteSpace(execution.CommitSha) ||
            summary?.CommittedAt is not null ||
            !string.IsNullOrWhiteSpace(summary?.CommitSha);
        bool hasPushEvidence = execution.PushedAt is not null ||
            !string.IsNullOrWhiteSpace(execution.PushedCommitSha) ||
            summary?.PushedAt is not null ||
            !string.IsNullOrWhiteSpace(summary?.PushedCommitSha);
        bool hasAcceptedExecution = execution.AcceptedAt is not null ||
            repositoryState is RepositoryExecutionState.Accepted or RepositoryExecutionState.AwaitingCommit or RepositoryExecutionState.AwaitingPush;
        bool noChangesProduced = hasAcceptedExecution &&
            operationalContext.IsCommitEligible &&
            !execution.HasChanges &&
            !hasPendingChanges &&
            !hasCommitEvidence &&
            !hasPushEvidence;

        WorkflowGitStatus commitStatus;
        WorkflowGitStatus pushStatus;

        if (repositoryState is RepositoryExecutionState.Failed || execution.Status is WorkflowExecutionStatus.Failed)
        {
            commitStatus = WorkflowGitStatus.Failed;
            pushStatus = WorkflowGitStatus.Failed;
            reasoning.Add("Execution evidence reports failure; git workflow cannot progress.");
        }
        else if (hasPushEvidence)
        {
            commitStatus = WorkflowGitStatus.Committed;
            pushStatus = WorkflowGitStatus.Pushed;
            commitSignals.Add("Execution-owned commit evidence exists.");
            pushSignals.Add("Execution-owned push evidence exists.");
            reasoning.Add("Push evidence completes the git workflow.");
        }
        else if (noChangesProduced)
        {
            commitStatus = WorkflowGitStatus.NoChangesProduced;
            pushStatus = WorkflowGitStatus.NoChangesProduced;
            commitSignals.Add("Accepted execution has no change evidence and git status is clean.");
            pushSignals.Add("No push is required because no commit is required.");
            reasoning.Add("No changes produced is explicit completion evidence for git workflow projection.");
        }
        else if (hasCommitEvidence || repositoryState is RepositoryExecutionState.AwaitingPush)
        {
            commitStatus = WorkflowGitStatus.Committed;
            pushStatus = WorkflowGitStatus.AwaitingPush;
            commitSignals.Add("Execution-owned commit evidence exists.");
            pushSignals.Add("Commit evidence exists and push evidence is absent.");
            reasoning.Add("Committed changes are awaiting human push approval.");
        }
        else if (hasPendingChanges || execution.HasChanges)
        {
            commitStatus = WorkflowGitStatus.AwaitingCommit;
            pushStatus = WorkflowGitStatus.NotReady;
            commitSignals.Add("Repository has pending changes or execution reports change evidence.");
            reasoning.Add("Changes are awaiting human commit approval.");
        }
        else
        {
            commitStatus = WorkflowGitStatus.NotReady;
            pushStatus = WorkflowGitStatus.NotReady;
            reasoning.Add("No commit or push evidence is ready for workflow progression.");
        }

        bool hasUnpushedChanges = pushStatus is WorkflowGitStatus.AwaitingPush ||
            (gitStatus.AheadCount > 0 && pushStatus is not WorkflowGitStatus.Pushed and not WorkflowGitStatus.NoChangesProduced);
        bool isCommitRequired = commitStatus is WorkflowGitStatus.AwaitingCommit;
        bool isPushRequired = pushStatus is WorkflowGitStatus.AwaitingPush;
        WorkflowCompletionEvaluation completion = EvaluateCompletion(
            repository.Id,
            commitStatus,
            pushStatus,
            execution,
            includedEvidence);

        return new WorkflowGitProjection(
            repository.Id,
            commitStatus,
            pushStatus,
            execution.PushedCommitSha ?? execution.CommitSha ?? summary?.PushedCommitSha ?? summary?.CommitSha,
            gitStatus.Branch,
            execution.CommittedAt ?? summary?.CommittedAt,
            execution.PushedAt ?? summary?.PushedAt,
            hasPendingChanges,
            hasUnpushedChanges,
            isCommitRequired,
            isPushRequired,
            isCommitRequired,
            isPushRequired,
            completion,
            new WorkflowGitDiagnostics(
                repository.Id,
                includedEvidence,
                missingEvidence,
                commitSignals,
                pushSignals,
                reasoning,
                conflicts));
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(candidate => candidate.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static WorkflowCompletionEvaluation EvaluateCompletion(
        Guid repositoryId,
        WorkflowGitStatus commitStatus,
        WorkflowGitStatus pushStatus,
        WorkflowExecutionProjection execution,
        IReadOnlyList<string> evidence)
    {
        if (pushStatus is WorkflowGitStatus.Pushed)
        {
            return new WorkflowCompletionEvaluation(
                repositoryId,
                true,
                "Push completed through Execution-owned git evidence.",
                execution.PushedCommitSha ?? execution.CommitSha ?? execution.ExecutionId?.ToString(),
                evidence,
                []);
        }

        if (pushStatus is WorkflowGitStatus.PushSkipped)
        {
            return new WorkflowCompletionEvaluation(
                repositoryId,
                true,
                "Push was explicitly skipped by domain-owned evidence.",
                execution.ExecutionId?.ToString(),
                evidence,
                []);
        }

        if (commitStatus is WorkflowGitStatus.NoChangesProduced &&
            pushStatus is WorkflowGitStatus.NoChangesProduced)
        {
            return new WorkflowCompletionEvaluation(
                repositoryId,
                true,
                "Accepted execution produced no changes, so commit and push are not required.",
                execution.ExecutionId?.ToString(),
                evidence,
                []);
        }

        return new WorkflowCompletionEvaluation(
            repositoryId,
            false,
            "Workflow is not complete until push completes, push is explicitly skipped, or no changes are produced.",
            null,
            evidence,
            []);
    }
}
