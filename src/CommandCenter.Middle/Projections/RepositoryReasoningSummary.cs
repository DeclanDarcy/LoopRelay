namespace CommandCenter.Middle.Projections;

public sealed class RepositoryReasoningSummary
{
    public int EventCount { get; init; }

    public int ThreadCount { get; init; }

    public int RelationshipCount { get; init; }

    public int HypothesisEventCount { get; init; }

    public int AlternativeEventCount { get; init; }

    public int ContradictionEventCount { get; init; }

    public int DirectionEventCount { get; init; }

    public int DecisionEvolutionEventCount { get; init; }

    public int AssumptionEvolutionEventCount { get; init; }

    public int ConstraintEvolutionEventCount { get; init; }

    public int EvidenceEventCount { get; init; }

    public DateTimeOffset? LastEventAt { get; init; }

    public DateTimeOffset? LastThreadActivityAt { get; init; }

    public DateTimeOffset? LastRelationshipAt { get; init; }

    public DateTimeOffset? LastActivityAt { get; init; }

    public DateTimeOffset? LastReconstructionAt { get; init; }

    public DateTimeOffset? LastCertificationAt { get; init; }

    public string? CertificationResult { get; init; }
}
