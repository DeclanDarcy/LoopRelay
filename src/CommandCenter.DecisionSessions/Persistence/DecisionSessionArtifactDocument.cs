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

    /// <summary>
    /// The deterministic per-family source CONTENT fingerprint captured when this snapshot was rebuilt
    /// (Phase 2 of the Derivation Cache refactor). It is the staleness KEY: warm-restart recovery skips the
    /// rebuild only when a freshly computed fingerprint over the same source families equals this value.
    /// Nullable so documents written before fingerprinting (or non-metrics documents) deserialize cleanly and
    /// are treated as stale (a null fingerprint never equals a present one, forcing a conservative rebuild).
    /// </summary>
    public string? SourceFingerprint { get; init; }
}
