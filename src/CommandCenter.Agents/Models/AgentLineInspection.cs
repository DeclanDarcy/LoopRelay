namespace CommandCenter.Agents.Models;

public enum AgentLineClassification
{
    Output,
    TurnCompleted
}

public sealed record AgentLineInspection(
    AgentLineClassification Classification,
    AgentTokenUsage? Usage = null);
