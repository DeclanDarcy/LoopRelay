namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record RepositoryFileSnapshotEntry(
    string Path,
    string? PreviousPath,
    string Status,
    bool Exists,
    bool IsDeleted,
    string Extension,
    long? Size,
    string? ContentSha256,
    IReadOnlyList<RepositoryGitDiffNameStatus> TrackedDiffMetadata);
