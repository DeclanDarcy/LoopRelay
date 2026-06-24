using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowGate(
    string GateId,
    WorkflowGateType Type,
    Guid RepositoryId,
    WorkflowStage Stage,
    WorkflowGateStatus Status,
    string RequiredAction,
    string SatisfyingCommand,
    IReadOnlyList<string> SatisfyingCommands,
    string SourceDomain,
    string SourceArtifact,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SatisfiedAt,
    string? SatisfiedActor,
    string Reason,
    IReadOnlyList<WorkflowGateEvidence> Evidence);
