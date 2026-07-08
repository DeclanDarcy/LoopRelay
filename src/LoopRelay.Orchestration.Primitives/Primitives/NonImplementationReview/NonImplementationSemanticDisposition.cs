namespace LoopRelay.Orchestration.Primitives.NonImplementationReview;

/// <summary>
/// Semantic confirmation outcomes for files routed to read-only review.
/// </summary>
public enum NonImplementationSemanticDisposition
{
    ConfirmedNonImplementation,
    FalsePositive,
    Uncertain,
}
