using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowContinuationDiagnostics(
    Guid RepositoryId,
    IReadOnlyList<string> ProjectionInputs,
    IReadOnlyList<string> StateMachineReasoning,
    IReadOnlyList<string> GateReasoning,
    IReadOnlyList<string> CompletionEvidence,
    IReadOnlyList<string> Reasoning,
    IReadOnlyList<string> StopReasons,
    IReadOnlyList<string> Conflicts,
    int OpenGateCount,
    int SatisfiedGateCount,
    WorkflowContinuationFingerprint Fingerprint);
