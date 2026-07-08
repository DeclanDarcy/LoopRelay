namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record RepositorySliceBaseline(
    string ExecutionSliceId,
    RepositorySliceSnapshot Snapshot,
    string? PersistedPath);
