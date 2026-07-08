namespace LoopRelay.Orchestration.Abstractions;

/// <summary>
/// The economic policy the <see cref="IDecisionSessionRouter"/> applies to decide reuse-vs-transfer (the hard
/// capacity guard always applies on top, regardless of policy). These are DISTINCT policies, not fallbacks of
/// one another — they encode different assumptions, so they are named explicitly to keep that clear.
/// </summary>
public enum DecisionTransferPolicy
{
    /// <summary>
    /// Default. Online average-cost optimization: transfer once the predicted next cycle's cost would raise the
    /// current run's amortized average — <c>eNext ≥ (R + C) / n</c>. Optimal for any increasing per-cycle cost
    /// curve (no linear-growth assumption), using only measured quantities.
    /// </summary>
    MarginalAverageCost,

    /// <summary>
    /// Diagnostic / legacy. Transfer once accumulated reuse cost reaches the transfer cost — <c>R ≥ C</c>. This
    /// is the cost-minimizing point ONLY under linearly-growing reuse cost (a strictly stronger assumption than
    /// <see cref="MarginalAverageCost"/>); kept as a labelled comparison policy, never as a silent fallback.
    /// </summary>
    LinearReuseApprox,

    /// <summary>
    /// Safety baseline. No economic transfer at all — only the hard capacity guard fires. Recovers a pure
    /// "recycle only when the window is about to overflow" behaviour for rollback/diagnosis.
    /// </summary>
    CapacityOnly
}
