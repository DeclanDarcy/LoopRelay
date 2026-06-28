namespace CommandCenter.DecisionSessions.Models;

/// <summary>
/// The warm-restart staleness stamp read from a persisted metrics snapshot document. Both fields are
/// nullable: a snapshot persisted before staleness stamping (or by the live metrics GET path) carries
/// null values, which never equal a non-null source probe and therefore always force a rebuild.
/// </summary>
public sealed record DecisionSessionMetricsSnapshotStamp(
    DateTimeOffset? SourceMaxWriteUtc,
    string? AnalysisOptionsVersion);
