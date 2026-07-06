namespace LoopRelay.Decisions.Primitives;

public readonly record struct DecisionId(string Value)
{
    public static DecisionId Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Decision ID is required.", nameof(value));
        }

        return new DecisionId(value.Trim());
    }

    public override string ToString()
    {
        return Value;
    }
}
