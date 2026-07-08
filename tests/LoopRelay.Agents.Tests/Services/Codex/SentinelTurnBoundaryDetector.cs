using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;

namespace LoopRelay.Agents.Tests.Services;

public sealed class SentinelTurnBoundaryDetector(
    string sentinel = SentinelTurnBoundaryDetector.DefaultSentinel) : IAgentTurnBoundaryDetector
{
    public const string DefaultSentinel = "<<<CC_TURN_COMPLETE>>>";

    public AgentLineInspection Inspect(string line) =>
        line.StartsWith(sentinel, StringComparison.Ordinal)
            ? new AgentLineInspection(AgentLineClassification.TurnCompleted)
            : new AgentLineInspection(AgentLineClassification.Output);
}
