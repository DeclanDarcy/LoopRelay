namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record NonImplementationArtifactClassificationSet(
    string ExecutionSliceId,
    IReadOnlyList<NonImplementationArtifactClassification> Classifications);
