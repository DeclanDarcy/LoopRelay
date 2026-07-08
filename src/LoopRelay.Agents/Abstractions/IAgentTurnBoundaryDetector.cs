using LoopRelay.Agents.Models;
using LoopRelay.Agents.Models.Streams;

namespace LoopRelay.Agents.Abstractions;

public interface IAgentTurnBoundaryDetector
{
    AgentLineInspection Inspect(string line);
}
