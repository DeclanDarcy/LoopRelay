using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowExecutionProjection(
    Guid RepositoryId,
    Guid? ExecutionId,
    WorkflowExecutionStatus Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? FailedAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? RejectedAt,
    DateTimeOffset? CommittedAt,
    DateTimeOffset? PushedAt,
    string? CommitSha,
    string? PushedCommitSha,
    bool HasHandoff,
    bool HasChanges,
    string? FailureReason,
    bool IsExecutionEligible,
    WorkflowExecutionFailure? Failure,
    WorkflowExecutionDiagnostics Diagnostics);
