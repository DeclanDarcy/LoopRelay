namespace LoopRelay.Orchestration.Models;

/// <summary>
/// Snapshot of the CLI DecisionSession's resumable state: the codex app-server thread id plus the
/// per-process router accounting and context-health counters, captured after every successful proposal
/// turn. The state is ONLY written after a successful turn, so its existence implies the thread was
/// primed with the operational context (no seeded flag is needed).
/// </summary>
public sealed record DecisionSessionResumeState(
    string ThreadId,
    int OccupancyTokens,
    double ReuseCost,
    int ReuseCycles,
    double LastCycleCost,
    double PrevCycleCost,
    double TransferCost,
    int TransferCount,
    int? PreviousOperationalContextSize,
    int OperationalContextGrowthStreak)
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public DateTimeOffset SavedAtUtc { get; init; }
}
