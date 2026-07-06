namespace LoopRelay.Decisions.Models;

public sealed record SupersedeDecisionCommand(
    string? ReplacementDecisionId,
    string? Rationale,
    string? Resolver);
