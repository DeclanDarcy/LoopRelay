namespace CommandCenter.Workflow.Models;

public sealed record WorkflowGitDiagnostics(
    Guid RepositoryId,
    IReadOnlyList<string> IncludedEvidence,
    IReadOnlyList<string> MissingEvidence,
    IReadOnlyList<string> CommitSignals,
    IReadOnlyList<string> PushSignals,
    IReadOnlyList<string> Reasoning,
    IReadOnlyList<string> Conflicts);
