namespace LoopRelay.Decisions.Models;

public sealed record DecisionCertificationEvidence(
    string Id,
    string Area,
    bool Passed,
    string Detail,
    IReadOnlyList<DecisionSourceReference> Sources,
    IReadOnlyList<string> RelatedDecisionIds,
    IReadOnlyList<string> RelatedCandidateIds,
    IReadOnlyList<string> RelatedProposalIds);
