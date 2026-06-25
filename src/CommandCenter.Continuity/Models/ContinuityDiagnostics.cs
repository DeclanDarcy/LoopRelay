namespace CommandCenter.Continuity.Models;

public sealed class ContinuityDiagnostics
{
    public Guid RepositoryId { get; init; }

    public DateTimeOffset GeneratedAt { get; init; }

    public int RevisionCount { get; init; }

    public int CurrentContextByteCount { get; init; }

    public int CurrentContextCharacterCount { get; init; }

    public int ContextByteGrowth { get; init; }

    public double AverageBytesPerRevision { get; init; }

    public TimeSpan? RevisionFrequency { get; init; }

    public UnderstandingEvolutionLedger EvolutionLedger { get; init; } = new();

    public OperationalEvolutionSummary OperationalEvolution { get; init; } = new();

    public ContinuityTrend ArchitectureTrend { get; init; } = new();

    public ContinuityTrend ConstraintTrend { get; init; } = new();

    public ContinuityTrend DecisionTrend { get; init; } = new();

    public ContinuityTrend RationaleTrend { get; init; } = new();

    public ContinuityTrend OpenQuestionTrend { get; init; } = new();

    public ContinuityTrend ActiveRiskTrend { get; init; } = new();

    public CompressionTrend CompressionTrend { get; init; } = new();

    public IReadOnlyList<string> RepeatedInvestigationIndicators { get; init; } = [];

    public IReadOnlyList<string> RepeatedQuestionIndicators { get; init; } = [];

    public IReadOnlyList<string> DecisionReworkIndicators { get; init; } = [];

    public IReadOnlyList<string> ContinuityWarnings { get; init; } = [];

    public IReadOnlyList<ContinuityDiagnosticGroup> DiagnosticGroups { get; init; } = [];
}
