using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Agents.Models.Process;

public sealed record EffortProfile(AgentEffortLevel Level, string? Identifier = null);
