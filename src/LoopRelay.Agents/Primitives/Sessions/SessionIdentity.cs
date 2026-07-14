namespace LoopRelay.Agents.Primitives.Sessions;

public readonly record struct SessionIdentity(Guid Value)
{
    public static SessionIdentity New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
