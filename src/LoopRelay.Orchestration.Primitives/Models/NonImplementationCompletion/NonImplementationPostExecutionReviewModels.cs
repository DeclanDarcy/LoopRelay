namespace LoopRelay.Orchestration.Models.NonImplementationCompletion;

public sealed record NonImplementationPostExecutionReviewSummary(
    int ChangedFileCount,
    int ClassifiedFileCount,
    int SemanticCandidateCount,
    int ConfirmedCount,
    int ReusedSemanticDispositionCount,
    int IgnoredCount,
    int ConfirmedNonImplementationCount,
    int FalsePositiveCount,
    int SemanticUncertaintyCount);
