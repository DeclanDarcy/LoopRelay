namespace CommandCenter.Reasoning.Services;

public class ReasoningValidationException(string message) : InvalidOperationException(message);

public sealed class ReasoningConflictException(string message) : ReasoningValidationException(message);
