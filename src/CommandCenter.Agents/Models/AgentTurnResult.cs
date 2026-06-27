namespace CommandCenter.Agents.Models;

public sealed record AgentTurnResult(
    int TurnIndex,
    AgentTurnState State,
    string Output,
    AgentTokenUsage Usage);
