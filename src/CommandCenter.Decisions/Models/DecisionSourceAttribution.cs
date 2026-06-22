namespace CommandCenter.Decisions.Models;

public sealed record DecisionSourceAttribution(
    string AppliesToKind,
    string? ItemId,
    string SourceKind,
    string? RelativePath,
    string? Section,
    string? Excerpt,
    DecisionSourceReference Source);
