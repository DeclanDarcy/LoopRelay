using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Primitives;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowExecutionService(
    IExecutionSessionService executionSessionService) : IWorkflowExecutionService
{
    public async Task<WorkflowExecutionProjection> ProjectExecutionAsync(Guid repositoryId)
    {
        RepositoryExecutionState repositoryState = await executionSessionService.GetRepositoryStateAsync(repositoryId);
        ExecutionSessionSummary? summary = await executionSessionService.GetRepositorySessionSummaryAsync(repositoryId);

        List<string> includedEvidence = [$"repository-execution-state:{repositoryState}"];
        List<string> missingEvidence = [];
        List<string> conflicts = [];
        List<string> reasoning = [];

        if (summary is null)
        {
            missingEvidence.Add("No execution session summary exists for the repository.");
            reasoning.Add("Execution has not started for this repository.");
            return CreateProjection(
                repositoryId,
                null,
                repositoryState,
                WorkflowExecutionStatus.NotStarted,
                true,
                includedEvidence,
                missingEvidence,
                conflicts,
                reasoning);
        }

        includedEvidence.Add($"execution-session:{summary.SessionId}:{summary.State}:{summary.RepositoryState}");

        if (summary.RepositoryState != repositoryState)
        {
            conflicts.Add($"Execution repository state is {repositoryState}, but latest session summary reports {summary.RepositoryState}.");
        }

        WorkflowExecutionStatus status = ResolveStatus(repositoryState, summary);
        bool isExecutionEligible = status is WorkflowExecutionStatus.NotStarted;

        reasoning.Add(status switch
        {
            WorkflowExecutionStatus.Running => "Execution session is active.",
            WorkflowExecutionStatus.Completed => "Execution session completed and is past handoff acceptance or later repository workflow stages.",
            WorkflowExecutionStatus.AwaitingAcceptance => "Execution session completed and is awaiting handoff acceptance.",
            WorkflowExecutionStatus.Failed => "Execution evidence reports failure.",
            WorkflowExecutionStatus.Cancelled => "Execution evidence reports cancellation.",
            _ => "Execution has not started for this repository."
        });

        return CreateProjection(
            repositoryId,
            summary,
            repositoryState,
            status,
            isExecutionEligible,
            includedEvidence,
            missingEvidence,
            conflicts,
            reasoning);
    }

    private static WorkflowExecutionProjection CreateProjection(
        Guid repositoryId,
        ExecutionSessionSummary? summary,
        RepositoryExecutionState repositoryState,
        WorkflowExecutionStatus status,
        bool isExecutionEligible,
        IReadOnlyList<string> includedEvidence,
        IReadOnlyList<string> missingEvidence,
        IReadOnlyList<string> conflicts,
        IReadOnlyList<string> reasoning)
    {
        DateTimeOffset? failedAt = status is WorkflowExecutionStatus.Failed
            ? summary?.LastActivityAt ?? summary?.CompletedAt ?? summary?.StartedAt
            : null;
        string? failureReason = status is WorkflowExecutionStatus.Failed or WorkflowExecutionStatus.Cancelled
            ? summary?.FailureReason ?? status.ToString()
            : null;
        var failure = failureReason is null
            ? null
            : new WorkflowExecutionFailure(
                failureReason,
                failedAt,
                summary?.SessionId.ToString() ?? repositoryId.ToString());

        return new WorkflowExecutionProjection(
            repositoryId,
            summary?.SessionId,
            status,
            summary?.StartedAt,
            summary?.CompletedAt,
            failedAt,
            summary?.AcceptedAt,
            summary?.RejectedAt,
            summary?.CommittedAt,
            summary?.PushedAt,
            summary?.CommitSha,
            summary?.PushedCommitSha,
            !string.IsNullOrWhiteSpace(summary?.HandoffPath),
            HasChangeEvidence(summary, repositoryState),
            failureReason,
            isExecutionEligible,
            failure,
            new WorkflowExecutionDiagnostics(
                repositoryId,
                includedEvidence,
                missingEvidence,
                conflicts,
                reasoning));
    }

    private static WorkflowExecutionStatus ResolveStatus(
        RepositoryExecutionState repositoryState,
        ExecutionSessionSummary summary)
    {
        if (repositoryState is RepositoryExecutionState.Failed ||
            summary.State is ExecutionSessionState.Failed)
        {
            return WorkflowExecutionStatus.Failed;
        }

        if (repositoryState is RepositoryExecutionState.Cancelled ||
            summary.State is ExecutionSessionState.Cancelled)
        {
            return WorkflowExecutionStatus.Cancelled;
        }

        if (repositoryState is RepositoryExecutionState.AwaitingAcceptance)
        {
            return WorkflowExecutionStatus.AwaitingAcceptance;
        }

        if (repositoryState is RepositoryExecutionState.Executing ||
            summary.State is ExecutionSessionState.Created or ExecutionSessionState.Executing)
        {
            return WorkflowExecutionStatus.Running;
        }

        if (summary.State is ExecutionSessionState.Completed)
        {
            return WorkflowExecutionStatus.Completed;
        }

        return WorkflowExecutionStatus.NotStarted;
    }

    private static bool HasChangeEvidence(ExecutionSessionSummary? summary, RepositoryExecutionState repositoryState)
    {
        if (summary is null)
        {
            return false;
        }

        return repositoryState is RepositoryExecutionState.AwaitingCommit or RepositoryExecutionState.AwaitingPush ||
            summary.PreparationSnapshotId is not null ||
            summary.CommitSha is not null ||
            summary.PushedCommitSha is not null ||
            summary.CommittedAt is not null ||
            summary.PushedAt is not null;
    }
}
