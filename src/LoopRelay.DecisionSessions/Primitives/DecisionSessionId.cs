namespace LoopRelay.DecisionSessions.Primitives;

public readonly record struct DecisionSessionId(Guid Value)
{
    public static DecisionSessionId New() => new(Guid.NewGuid());

    public static DecisionSessionId Parse(string value) => new(Guid.Parse(value));

    public override string ToString() => Value.ToString("D");
}
