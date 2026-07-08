using LoopRelay.Agents.Primitives;

namespace LoopRelay.Agents.Models;

public sealed record EffortProfile(AgentEffortLevel Level, string? Identifier = null);
