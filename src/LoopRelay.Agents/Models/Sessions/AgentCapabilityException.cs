namespace LoopRelay.Agents.Models.Sessions;

/// <summary>
/// A session spec required a capability the runtime does not declare. D5 (M7): there is no
/// multi-provider fallback — a capability gap is a typed, recorded outcome, never a silent
/// reroute. The message leads with the specific MissingRuntimeCapability label so every
/// surface that renders it names the actual failure.
/// </summary>
public sealed class AgentCapabilityException : Exception
{
    public AgentCapabilityException(string capability, string message)
        : base($"MissingRuntimeCapability: {message}")
    {
        Capability = capability;
    }

    public string Capability { get; }
}
