namespace LoopRelay.Orchestration.Primitives.NonImplementationReview;

/// <summary>
/// Deterministic classifier routes for changed files before any semantic review runs.
/// </summary>
public enum NonImplementationArtifactRoute
{
    ExcludedImplementationArtifact,
    ExcludedMachineRequiredArtifact,
    ExcludedSanctionedOperationalArtifact,
    SemanticReviewCandidate,
    AmbiguousForSemanticReview,
}
