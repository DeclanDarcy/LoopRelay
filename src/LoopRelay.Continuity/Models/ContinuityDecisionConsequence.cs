namespace LoopRelay.Continuity.Models;

public sealed class ContinuityDecisionConsequence
{
    public string ConsequenceId { get; init; } = string.Empty;

    public ContinuityDecisionReference OriginatingDecision { get; init; } = new();

    public string OperationalStatement { get; init; } = string.Empty;

    public string AffectedArea { get; init; } = string.Empty;

    public IReadOnlyList<string> SupportingEvidence { get; init; } = [];

    public string OperationalImpact { get; init; } = string.Empty;
}
