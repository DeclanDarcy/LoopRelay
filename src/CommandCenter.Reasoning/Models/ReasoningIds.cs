namespace CommandCenter.Reasoning.Models;

public readonly record struct ReasoningEventId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct ReasoningThreadId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct ReasoningRelationshipId(string Value)
{
    public override string ToString() => Value;
}
