namespace LoopRelay.Orchestration.Models.RepositorySlices;

public sealed record RepositoryChangedFileFacts(
    string ExecutionSliceId,
    string Path,
    string? PreviousPath,
    string Status,
    string? BaselineStatus,
    string? PostStatus,
    bool PreExisted,
    bool Exists,
    bool IsDeleted,
    string Extension,
    long? Size,
    string? BaselineContentSha256,
    string? PostContentSha256,
    IReadOnlyList<RepositoryGitDiffNameStatus> TrackedDiffMetadata);
