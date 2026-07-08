namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed class NonImplementationSemanticConfirmationParseException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
