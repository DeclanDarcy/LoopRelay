namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record RepositorySliceDelta(
    string ExecutionSliceId,
    IReadOnlyList<RepositoryChangedFileFacts> ChangedFiles);
