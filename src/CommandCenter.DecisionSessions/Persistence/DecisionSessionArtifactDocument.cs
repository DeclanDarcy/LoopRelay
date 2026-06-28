namespace CommandCenter.DecisionSessions.Persistence;

public sealed record DecisionSessionArtifactDocument<T>(
    string SchemaVersion,
    Guid RepositoryId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    T Payload)
{
    /// <summary>
    /// The maximum source last-write timestamp captured when this snapshot was rebuilt. Nullable so that
    /// snapshots persisted before warm-restart staleness detection (and non-metrics documents that never
    /// stamp it) deserialize cleanly and are treated as stale (never equal to a non-null probe).
    /// </summary>
    public DateTimeOffset? SourceMaxWriteUtc { get; init; }

    /// <summary>
    /// A hand-bumped version constant identifying the analysis formulas used to build the payload. When the
    /// metrics/economics/coherence/lifecycle formulas change this is bumped so older snapshots are rebuilt.
    /// Nullable so pre-existing documents deserialize cleanly and are treated as stale.
    /// </summary>
    public string? AnalysisOptionsVersion { get; init; }
}
