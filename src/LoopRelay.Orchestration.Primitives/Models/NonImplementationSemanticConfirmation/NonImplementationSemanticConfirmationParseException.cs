namespace LoopRelay.Orchestration.Models.NonImplementationSemanticConfirmation;

public sealed class NonImplementationSemanticConfirmationParseException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
