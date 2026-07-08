namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record RepositoryGitDiffNameStatus(
    string Status,
    string Path,
    string? PreviousPath = null);
