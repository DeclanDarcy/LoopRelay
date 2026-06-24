using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowPreparationDiagnostics(
    Guid RepositoryId,
    IReadOnlyList<string> ProjectionInputs,
    IReadOnlyList<string> GateReasoning,
    IReadOnlyList<string> Reasoning,
    IReadOnlyList<string> RefusalReasons,
    IReadOnlyList<string> DuplicateEvidence,
    IReadOnlyList<string> Conflicts,
    int OpenGateCount,
    int SatisfiedGateCount,
    WorkflowPreparationFingerprint Fingerprint);
