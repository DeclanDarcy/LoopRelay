using CommandCenter.Continuity.Primitives;

namespace CommandCenter.Continuity.Models;

public sealed class ContinuityDecisionContradiction
{
    public string ContradictionId { get; init; } = string.Empty;

    public ContinuityDecisionReference DecisionA { get; init; } = new();

    public ContinuityDecisionReference DecisionB { get; init; } = new();

    public DecisionContradictionConflictType ConflictType { get; init; }

    public IReadOnlyList<string> ConflictEvidence { get; init; } = [];

    public DecisionContradictionSeverity Severity { get; init; }

    public string ResolutionGuidance { get; init; } = string.Empty;

    public string CompatibilityWarning { get; init; } = string.Empty;
}
