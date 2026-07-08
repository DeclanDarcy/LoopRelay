namespace LoopRelay.Completion.Models.Certification;

public sealed class CompletionCertificationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
