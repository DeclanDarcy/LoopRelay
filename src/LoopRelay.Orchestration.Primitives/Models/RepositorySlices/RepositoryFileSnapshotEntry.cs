namespace LoopRelay.Orchestration.Models.RepositorySlices;

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
