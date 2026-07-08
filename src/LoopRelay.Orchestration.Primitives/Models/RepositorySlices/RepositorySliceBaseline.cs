namespace LoopRelay.Orchestration.Models.RepositorySlices;

public sealed record RepositorySliceBaseline(
    string ExecutionSliceId,
    RepositorySliceSnapshot Snapshot,
    string? PersistedPath);
