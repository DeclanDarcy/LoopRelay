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

internal interface ILoopHistoryStore
{
    Task<LoopHistoryRecord> AppendAsync(LoopHistoryKind kind, string content);

    Task<LoopHistoryRecord?> ReadLatestAsync(LoopHistoryKind kind);
}
