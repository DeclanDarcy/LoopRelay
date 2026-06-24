using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowGateDiagnostics(
    Guid RepositoryId,
    WorkflowGateType BlockingGate,
    IReadOnlyList<WorkflowGate> OpenGates,
    IReadOnlyList<WorkflowGate> SatisfiedGates,
    IReadOnlyList<string> GateCommandMap,
    IReadOnlyList<string> Reasoning,
    IReadOnlyList<string> MissingEvidence,
    IReadOnlyList<string> Conflicts);
