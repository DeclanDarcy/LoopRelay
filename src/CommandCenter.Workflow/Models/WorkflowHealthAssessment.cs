namespace CommandCenter.Workflow.Models;

public sealed record WorkflowHealthAssessment(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    string OverallStatus,
    IReadOnlyList<WorkflowHealthDimension> Dimensions,
    WorkflowInfluenceTrace InfluenceTrace,
    IReadOnlyList<string> Diagnostics);
