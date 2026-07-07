namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record RepositoryGitDiffNameStatus(
    string Status,
    string Path,
    string? PreviousPath = null);

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

public sealed record RepositorySliceSnapshot(
    string ExecutionSliceId,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<RepositoryFileSnapshotEntry> Files);

public sealed record RepositorySliceBaseline(
    string ExecutionSliceId,
    RepositorySliceSnapshot Snapshot,
    string? PersistedPath);

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

public sealed record RepositorySliceDelta(
    string ExecutionSliceId,
    IReadOnlyList<RepositoryChangedFileFacts> ChangedFiles);

public sealed record NonImplementationArtifactClassification(
    RepositoryChangedFileFacts File,
    NonImplementationArtifactRoute Route,
    string RuleId,
    IReadOnlyList<string> PathFacts,
    string Rationale,
    string ClassifierVersion);

public sealed record NonImplementationArtifactClassificationSet(
    string ExecutionSliceId,
    IReadOnlyList<NonImplementationArtifactClassification> Classifications);
