namespace CommandCenter.Workflow.Models;

public sealed record WorkflowGateCatalogProjection(
    IReadOnlyList<WorkflowGate> OpenGates,
    IReadOnlyList<WorkflowGate> SatisfiedGates,
    IReadOnlyList<WorkflowGate> GateHistory,
    WorkflowGateDiagnostics Diagnostics);
