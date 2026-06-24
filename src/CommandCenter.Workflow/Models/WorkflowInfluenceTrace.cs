using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowInfluenceTrace(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    WorkflowStage CurrentStage,
    WorkflowProgressState ProgressState,
    WorkflowGateType BlockingGate,
    IReadOnlyList<string> EvidencePaths,
    IReadOnlyList<string> StageInfluences,
    IReadOnlyList<string> ProgressionInfluences,
    IReadOnlyList<string> PreparationInfluences,
    IReadOnlyList<string> GateInfluences,
    IReadOnlyList<string> BlockingInfluences,
    IReadOnlyList<string> Conflicts,
    string Fingerprint)
{
    public WorkflowGovernanceInfluenceProjection? GovernanceInfluence { get; init; }
}
