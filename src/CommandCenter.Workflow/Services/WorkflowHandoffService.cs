using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Primitives;
using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowHandoffService(
    IRepositoryService repositoryService,
    IExecutionSessionService executionSessionService,
    IArtifactStore artifactStore) : IWorkflowHandoffService
{
    private const string CurrentHandoffPath = ".agents/handoffs/handoff.md";

    public async Task<WorkflowHandoffProjection> ProjectHandoffAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        ExecutionSessionSummary? summary = await executionSessionService.GetRepositorySessionSummaryAsync(repositoryId);

        List<string> includedEvidence = [$"repository:{repository.Id}"];
        List<string> missingEvidence = [];
        List<string> conflicts = [];
        List<string> reasoning = [];
        List<string> checks = [];
        List<string> failures = [];

        if (summary is null)
        {
            missingEvidence.Add("No execution session summary exists for the repository.");
            reasoning.Add("No execution session exists, so no handoff is authoritative.");
            return CreateProjection(
                repository.Id,
                null,
                WorkflowHandoffStatus.Missing,
                null,
                null,
                null,
                null,
                false,
                "No handoff evidence exists.",
                checks,
                failures,
                includedEvidence,
                missingEvidence,
                conflicts,
                reasoning);
        }

        includedEvidence.Add($"execution-session:{summary.SessionId}:{summary.State}:{summary.RepositoryState}");

        string? handoffPath = string.IsNullOrWhiteSpace(summary.HandoffPath)
            ? null
            : summary.HandoffPath;
        string? content = null;

        if (handoffPath is null)
        {
            missingEvidence.Add("The execution session does not identify an authoritative handoff path.");
            checks.Add("Execution session handoff path is absent.");
        }
        else
        {
            checks.Add($"Execution session identifies authoritative handoff path {handoffPath}.");
            string absoluteHandoffPath = Path.Combine(
                repository.Path,
                handoffPath.Replace('/', Path.DirectorySeparatorChar));
            content = await artifactStore.ReadAsync(absoluteHandoffPath);
            if (content is null)
            {
                missingEvidence.Add($"Authoritative handoff artifact was not found at {handoffPath}.");
                failures.Add($"Missing handoff artifact: {handoffPath}.");
            }
            else if (string.IsNullOrWhiteSpace(content))
            {
                includedEvidence.Add($"handoff-artifact:{handoffPath}:empty");
                failures.Add($"Handoff artifact is empty: {handoffPath}.");
            }
            else
            {
                includedEvidence.Add($"handoff-artifact:{handoffPath}:length={content.Length}");
                checks.Add("Authoritative handoff artifact exists and is not empty.");
            }
        }

        if (handoffPath is not null &&
            !string.Equals(handoffPath, CurrentHandoffPath, StringComparison.Ordinal))
        {
            conflicts.Add($"Execution session points to {handoffPath}, not the current handoff path {CurrentHandoffPath}.");
        }

        WorkflowHandoffStatus status = ResolveStatus(summary, handoffPath, content, failures);
        reasoning.Add(status switch
        {
            WorkflowHandoffStatus.Pending => "Execution completed and the authoritative handoff awaits human acceptance or rejection.",
            WorkflowHandoffStatus.Accepted => "Execution handoff was accepted through Execution authority.",
            WorkflowHandoffStatus.Rejected => "Execution handoff was rejected through Execution authority.",
            WorkflowHandoffStatus.Invalid => "Execution handoff evidence is present but failed validation.",
            _ => "No authoritative handoff is available for workflow projection."
        });

        return CreateProjection(
            repository.Id,
            summary,
            status,
            handoffPath,
            summary.CompletedAt,
            summary.AcceptedAt,
            summary.RejectedAt,
            HasChangeEvidence(summary),
            BuildSummary(status, handoffPath, content),
            checks,
            failures,
            includedEvidence,
            missingEvidence,
            conflicts,
            reasoning);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(candidate => candidate.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static WorkflowHandoffStatus ResolveStatus(
        ExecutionSessionSummary summary,
        string? handoffPath,
        string? content,
        IReadOnlyList<string> failures)
    {
        if (summary.RejectedAt is not null)
        {
            return WorkflowHandoffStatus.Rejected;
        }

        if (failures.Count > 0)
        {
            return WorkflowHandoffStatus.Invalid;
        }

        if (summary.AcceptedAt is not null ||
            summary.RepositoryState is RepositoryExecutionState.Accepted or RepositoryExecutionState.AwaitingCommit or RepositoryExecutionState.AwaitingPush)
        {
            return handoffPath is null || content is null
                ? WorkflowHandoffStatus.Missing
                : WorkflowHandoffStatus.Accepted;
        }

        if (summary.RepositoryState is RepositoryExecutionState.AwaitingAcceptance)
        {
            return handoffPath is null || content is null
                ? WorkflowHandoffStatus.Missing
                : WorkflowHandoffStatus.Pending;
        }

        return handoffPath is null
            ? WorkflowHandoffStatus.Missing
            : WorkflowHandoffStatus.Pending;
    }

    private static WorkflowHandoffProjection CreateProjection(
        Guid repositoryId,
        ExecutionSessionSummary? summary,
        WorkflowHandoffStatus status,
        string? handoffPath,
        DateTimeOffset? createdAt,
        DateTimeOffset? acceptedAt,
        DateTimeOffset? rejectedAt,
        bool hasChanges,
        string? projectionSummary,
        IReadOnlyList<string> checks,
        IReadOnlyList<string> failures,
        IReadOnlyList<string> includedEvidence,
        IReadOnlyList<string> missingEvidence,
        IReadOnlyList<string> conflicts,
        IReadOnlyList<string> reasoning) =>
        new(
            repositoryId,
            summary?.SessionId,
            summary?.SessionId is null || handoffPath is null ? null : $"{summary.SessionId}:{handoffPath}",
            handoffPath,
            status,
            createdAt,
            acceptedAt,
            rejectedAt,
            hasChanges,
            projectionSummary,
            new WorkflowHandoffValidation(failures.Count == 0 && status is not WorkflowHandoffStatus.Invalid, checks, failures),
            new WorkflowHandoffDiagnostics(repositoryId, includedEvidence, missingEvidence, conflicts, reasoning));

    private static bool HasChangeEvidence(ExecutionSessionSummary summary) =>
        summary.RepositoryState is RepositoryExecutionState.AwaitingCommit or RepositoryExecutionState.AwaitingPush ||
        summary.PreparationSnapshotId is not null ||
        summary.CommitSha is not null ||
        summary.PushedCommitSha is not null ||
        summary.CommittedAt is not null ||
        summary.PushedAt is not null;

    private static string BuildSummary(WorkflowHandoffStatus status, string? handoffPath, string? content)
    {
        if (handoffPath is null)
        {
            return "No authoritative handoff path.";
        }

        if (content is null)
        {
            return $"{status} handoff at {handoffPath}; artifact content is missing.";
        }

        string firstLine = content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "Empty handoff.";

        return $"{status} handoff at {handoffPath}: {firstLine}";
    }
}
