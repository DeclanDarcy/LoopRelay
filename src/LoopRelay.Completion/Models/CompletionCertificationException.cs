namespace LoopRelay.Completion;

public sealed class CompletionCertificationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
