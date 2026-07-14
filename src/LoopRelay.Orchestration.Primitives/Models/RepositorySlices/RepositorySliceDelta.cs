namespace LoopRelay.Orchestration.Models.RepositorySlices;

public sealed record RepositorySliceDelta(
    string ExecutionSliceId,
    IReadOnlyList<RepositoryChangedFileFacts> ChangedFiles);
