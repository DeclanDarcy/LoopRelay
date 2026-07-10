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
    string? Content,
    LoopHistoryProducerCorrelation? Producer = null);

internal sealed record LoopHistoryProducerCorrelation(
    string TransitionRunId,
    string LineageId,
    string ProviderThreadId,
    string? ProviderTurnId,
    string? RecoveryAttemptId);

internal interface ILoopHistoryStore
{
    Task<LoopHistoryRecord> AppendAsync(
        LoopHistoryKind kind,
        string content,
        LoopHistoryProducerCorrelation? producer = null);

    Task<LoopHistoryRecord?> ReadLatestAsync(LoopHistoryKind kind);
}
