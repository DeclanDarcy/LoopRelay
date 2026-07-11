namespace LoopRelay.Core.Models.Identity;

/// <summary>
/// Prefixed-ULID identities forming the causal spine:
/// workspace → run → workflow instance → transition run → attempt → agent session → turn.
/// Values are opaque TEXT; legacy 32-hex ids coexist in existing databases.
/// </summary>
public readonly record struct WorkspaceIdentity(string Value)
{
    public static WorkspaceIdentity New() => new(CausalUlid.NewId("ws"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct RunIdentity(string Value)
{
    public static RunIdentity New() => new(CausalUlid.NewId("run"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct WorkflowInstanceIdentity(string Value)
{
    public static WorkflowInstanceIdentity New() => new(CausalUlid.NewId("wfi"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct TransitionRunIdentity(string Value)
{
    public static TransitionRunIdentity New() => new(CausalUlid.NewId("tr"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct AttemptIdentity(string Value)
{
    public static AttemptIdentity New() => new(CausalUlid.NewId("att"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct AgentSessionIdentity(string Value)
{
    public static AgentSessionIdentity New() => new(CausalUlid.NewId("ses"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct TurnIdentity(string Value)
{
    public static TurnIdentity New() => new(CausalUlid.NewId("turn"));

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}
