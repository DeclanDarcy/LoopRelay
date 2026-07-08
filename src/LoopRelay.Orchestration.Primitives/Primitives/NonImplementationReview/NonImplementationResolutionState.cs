namespace LoopRelay.Orchestration.Models.NonImplementationReview;

/// <summary>
/// Durable HITL resolution state for ledger entries.
/// </summary>
public enum NonImplementationResolutionState
{
    Unresolved,
    HitlKept,
    HitlDeleted,
    HitlFalsePositive,
    HitlDeferred,
}
