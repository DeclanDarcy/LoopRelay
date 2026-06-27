namespace CommandCenter.Agents.Models;

public readonly record struct SessionIdentity(Guid Value)
{
    public static SessionIdentity New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
