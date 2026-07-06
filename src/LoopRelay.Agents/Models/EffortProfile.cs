namespace LoopRelay.Agents.Models;

public sealed record EffortProfile(AgentEffortLevel Level, string? Identifier = null);

public enum AgentEffortLevel
{
    Low,
    Medium,
    High
}
