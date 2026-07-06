namespace LoopRelay.Decisions.Models;

public sealed record ArchiveDecisionCommand(
    string? Rationale,
    string? Resolver);
