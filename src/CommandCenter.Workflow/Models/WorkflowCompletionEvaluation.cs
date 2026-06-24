namespace CommandCenter.Workflow.Models;

public sealed record WorkflowCompletionEvaluation(
    Guid RepositoryId,
    bool IsComplete,
    string CompletionReason,
    string? CompletionArtifact,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Diagnostics);
