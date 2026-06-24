namespace CommandCenter.Workflow.Models;

public sealed record WorkflowDecisionDiagnostics(
    Guid RepositoryId,
    IReadOnlyList<string> ProjectionInputs,
    IReadOnlyList<string> Reasoning,
    IReadOnlyList<string> GovernanceSignals,
    IReadOnlyList<string> QualitySignals,
    IReadOnlyList<string> CertificationSignals,
    IReadOnlyList<string> SupersessionSignals,
    IReadOnlyList<string> Conflicts);
