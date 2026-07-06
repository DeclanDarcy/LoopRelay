namespace LoopRelay.Decisions.Models;

public sealed record DecisionEvidenceInspectionItem(
    string AppliesToKind,
    string? ItemId,
    string Summary,
    IReadOnlyList<DecisionSourceAttribution> Sources);
