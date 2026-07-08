namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed class NonImplementationReviewRunnerException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
