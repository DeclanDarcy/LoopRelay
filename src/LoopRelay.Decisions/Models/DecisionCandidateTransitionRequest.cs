namespace LoopRelay.Decisions.Models;

public sealed record DecisionCandidateTransitionRequest(
    string? Reason = null,
    string? DuplicateOfCandidateId = null);
