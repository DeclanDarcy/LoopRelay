using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record RefinementDirective(
    string Id,
    RefinementDirectiveType Type,
    string Summary,
    string? TargetOptionId = null,
    string? TargetField = null,
    string? Instruction = null,
    IReadOnlyList<DecisionSourceReference>? Sources = null);
