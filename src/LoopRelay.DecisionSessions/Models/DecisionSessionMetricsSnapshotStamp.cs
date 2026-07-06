namespace LoopRelay.DecisionSessions.Models;

/// <summary>
/// The warm-restart staleness stamp read from a persisted metrics snapshot document. All fields are
/// nullable: a snapshot persisted before staleness stamping (or by the live metrics GET path) carries
/// null values, which never equal a present source fingerprint and therefore always force a rebuild.
///
/// As of Phase 2 of the Derivation Cache refactor the staleness KEY is <see cref="SourceFingerprint"/> —
/// a deterministic per-family CONTENT hash from <c>ISourceFingerprintProvider</c> — not the legacy
/// <see cref="SourceMaxWriteUtc"/> mtime probe. The mtime field is retained only so snapshots written by
/// earlier builds deserialize cleanly (their null fingerprint forces a conservative rebuild); it is no
/// longer compared for the skip decision.
/// </summary>
public sealed record DecisionSessionMetricsSnapshotStamp(
    DateTimeOffset? SourceMaxWriteUtc,
    string? AnalysisOptionsVersion,
    string? SourceFingerprint = null);
