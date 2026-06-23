namespace CommandCenter.Decisions.Models;

public sealed record DecisionGenerationCertificationFinding(
    string Id,
    string Category,
    bool Passed,
    string Summary,
    string Detail,
    IReadOnlyList<DecisionSourceReference> Sources,
    IReadOnlyList<string> RelatedDecisionIds,
    IReadOnlyList<string> RelatedCandidateIds,
    IReadOnlyList<string> RelatedProposalIds);
