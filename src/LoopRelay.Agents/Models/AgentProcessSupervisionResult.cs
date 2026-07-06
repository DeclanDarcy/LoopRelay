namespace LoopRelay.Agents.Models;

public sealed record AgentProcessSupervisionResult(
    AgentProcessState State,
    int? ExitCode);
