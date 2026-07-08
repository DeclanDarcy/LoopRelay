namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed class NonImplementationReviewLedgerException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
