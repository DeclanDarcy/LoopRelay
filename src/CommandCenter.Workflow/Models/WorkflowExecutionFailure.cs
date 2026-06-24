namespace CommandCenter.Workflow.Models;

public sealed record WorkflowExecutionFailure(
    string Reason,
    DateTimeOffset? FailedAt,
    string SourceArtifact);
