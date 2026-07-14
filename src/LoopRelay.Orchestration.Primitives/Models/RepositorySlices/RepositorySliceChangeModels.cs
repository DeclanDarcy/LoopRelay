namespace LoopRelay.Orchestration.Models.RepositorySlices;

public sealed record RepositoryGitDiffNameStatus(
    string Status,
    string Path,
    string? PreviousPath = null);
