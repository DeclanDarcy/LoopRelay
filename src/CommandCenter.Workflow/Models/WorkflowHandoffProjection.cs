using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowHandoffProjection(
    Guid RepositoryId,
    Guid? ExecutionId,
    string? HandoffId,
    string? HandoffPath,
    WorkflowHandoffStatus Status,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? RejectedAt,
    bool HasChanges,
    string? Summary,
    WorkflowHandoffValidation Validation,
    WorkflowHandoffDiagnostics Diagnostics);
