namespace CommandCenter.Backend.Continuity;

public sealed class UnderstandingRevisionSnapshot
{
    public int RevisionNumber { get; init; }

    public string RelativePath { get; init; } = string.Empty;

    public DateTimeOffset? LastUpdatedAt { get; init; }

    public int ByteCount { get; init; }

    public int CharacterCount { get; init; }

    public int ArchitectureItemCount { get; init; }

    public int ConstraintCount { get; init; }

    public int StableDecisionCount { get; init; }

    public int DecisionRationaleCount { get; init; }

    public int OpenQuestionCount { get; init; }

    public int ActiveRiskCount { get; init; }
}
