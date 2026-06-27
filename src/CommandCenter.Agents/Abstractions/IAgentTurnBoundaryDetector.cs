using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Abstractions;

public interface IAgentTurnBoundaryDetector
{
    AgentLineInspection Inspect(string line);
}
