using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionGovernanceFinding(
    string Id,
    DecisionGovernanceCategory Category,
    DecisionGovernanceSeverity Severity,
    bool BlocksExecutionProjection,
    string Title,
    string Detail,
    IReadOnlyList<DecisionSourceReference> Sources,
    IReadOnlyList<string> RelatedDecisionIds,
    IReadOnlyList<string> RelatedCandidateIds,
    IReadOnlyList<string> RelatedProposalIds);
