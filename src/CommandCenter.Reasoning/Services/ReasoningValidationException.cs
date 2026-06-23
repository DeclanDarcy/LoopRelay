namespace CommandCenter.Reasoning.Services;

public sealed class ReasoningValidationException(string message) : InvalidOperationException(message);
