using CommandCenter.Reasoning.Models;

namespace CommandCenter.Reasoning.Services;

public class ReasoningValidationException : InvalidOperationException
{
    public ReasoningValidationException(string message, ReasoningBoundaryViolation? boundaryViolation = null)
        : base(message)
    {
        BoundaryViolation = boundaryViolation;
    }

    public ReasoningBoundaryViolation? BoundaryViolation { get; }
}

public sealed class ReasoningConflictException : ReasoningValidationException
{
    public ReasoningConflictException(string message, ReasoningBoundaryViolation? boundaryViolation = null)
        : base(message, boundaryViolation)
    {
    }
}
