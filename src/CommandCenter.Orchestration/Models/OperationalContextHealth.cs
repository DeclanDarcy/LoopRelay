namespace CommandCenter.Orchestration.Models;

/// <summary>
/// The size-health verdict for one operational-context revision produced by a Transfer. The operational context
/// is a renewal-reward document — each transfer folds a delta into it and reseeds the next decision process from
/// it — so if it RATCHETS upward every transfer, both the reuse cost (R) and the transfer cost (C) drift up and
/// the cost-aware router's stopping rule destabilizes. This makes sustained growth observable so an operator (or
/// a future auto-compaction step) can act before the document bloats.
/// </summary>
/// <param name="Size">Size (characters) of the operational context this transfer produced.</param>
/// <param name="PreviousSize">Size of the previous revision, or null on the first transfer of a process's lifetime.</param>
/// <param name="GrowthStreak">Consecutive transfers (including this one) on which the context grew; 0 when it shrank/held or on the first transfer.</param>
/// <param name="Warning">True once the growth streak reaches the ratchet threshold — a sustained upward trend, not a single bump.</param>
public readonly record struct OperationalContextHealth(
    int Size,
    int? PreviousSize,
    int GrowthStreak,
    bool Warning);
