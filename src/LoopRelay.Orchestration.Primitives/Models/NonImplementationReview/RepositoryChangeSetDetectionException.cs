namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed class RepositoryChangeSetDetectionException(string message)
    : InvalidOperationException(message);
