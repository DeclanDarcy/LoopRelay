using LoopRelay.Agents.Models;

namespace LoopRelay.Agents.Abstractions;

public interface IAgentTurnBoundaryDetector
{
    AgentLineInspection Inspect(string line);
}
