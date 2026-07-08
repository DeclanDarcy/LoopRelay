namespace LoopRelay.Orchestration.Models.NonImplementationLedger;

public sealed class NonImplementationReviewLedgerException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
