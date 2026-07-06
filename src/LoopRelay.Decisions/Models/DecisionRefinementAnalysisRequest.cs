namespace LoopRelay.Decisions.Models;

public sealed record DecisionRefinementAnalysisRequest(
    string Guidance,
    string? RequestedBy = null,
    string? BaseProposalFingerprint = null);
