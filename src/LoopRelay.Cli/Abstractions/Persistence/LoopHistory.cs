namespace LoopRelay.Cli.Abstractions.Persistence;

internal enum LoopHistoryKind
{
    Decisions,
    Handoff,
    OperationalDelta
}

internal sealed record LoopHistoryRecord(
    LoopHistoryKind Kind,
    int Sequence,
    string RelativePath,
    string? Content);

// Causal spine ids of the transition whose effect appended the history fact; null fields mean
// the writer ran outside a canonical transition (legacy loop bodies until M17-M19).
internal sealed record LoopHistoryLineage(
    string? RunId,
    string? TransitionRunId,
    string? AttemptId);

internal interface ILoopHistoryStore
{
    Task<LoopHistoryRecord> AppendAsync(LoopHistoryKind kind, string content, LoopHistoryLineage? lineage = null);

    Task<LoopHistoryRecord?> ReadLatestAsync(LoopHistoryKind kind);
}
