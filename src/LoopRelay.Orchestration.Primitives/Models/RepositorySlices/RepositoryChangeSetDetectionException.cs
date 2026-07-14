namespace LoopRelay.Orchestration.Models.RepositorySlices;

public sealed class RepositoryChangeSetDetectionException(string message)
    : InvalidOperationException(message);
