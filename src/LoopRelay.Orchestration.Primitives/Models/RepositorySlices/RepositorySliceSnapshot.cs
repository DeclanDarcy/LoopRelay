namespace LoopRelay.Orchestration.Models.RepositorySlices;

public sealed record RepositorySliceSnapshot(
    string ExecutionSliceId,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<RepositoryFileSnapshotEntry> Files);
