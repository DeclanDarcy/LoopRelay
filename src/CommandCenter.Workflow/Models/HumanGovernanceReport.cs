using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record HumanGovernanceReport(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    WorkflowGateType BlockingGate,
    string RequiredHumanAction,
    int OpenGateCount,
    int SatisfiedGateCount,
    IReadOnlyList<string> OpenGates,
    IReadOnlyList<string> SatisfiedGates,
    IReadOnlyList<string> AuthorityFindings,
    IReadOnlyList<string> Diagnostics);
