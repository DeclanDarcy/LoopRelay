namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record RepositorySliceSnapshot(
    string ExecutionSliceId,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<RepositoryFileSnapshotEntry> Files);
