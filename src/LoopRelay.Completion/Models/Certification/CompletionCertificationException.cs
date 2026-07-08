namespace LoopRelay.Completion.Models;

public sealed class CompletionCertificationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
