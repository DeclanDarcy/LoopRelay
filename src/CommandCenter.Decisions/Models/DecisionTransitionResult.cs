namespace CommandCenter.Decisions.Models;

public sealed record DecisionTransitionResult(bool IsValid, string? Error)
{
    public static DecisionTransitionResult Valid { get; } = new(true, null);

    public static DecisionTransitionResult Invalid(string error)
    {
        return new DecisionTransitionResult(false, error);
    }
}
