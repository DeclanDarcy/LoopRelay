using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Streams;

namespace LoopRelay.Agents.Tests.Services.Codex;

public sealed class SentinelTurnBoundaryDetector(
    string sentinel = SentinelTurnBoundaryDetector.DefaultSentinel) : IAgentTurnBoundaryDetector
{
    public const string DefaultSentinel = "<<<CC_TURN_COMPLETE>>>";

    public AgentLineInspection Inspect(string line) =>
        line.StartsWith(sentinel, StringComparison.Ordinal)
            ? new AgentLineInspection(AgentLineClassification.TurnCompleted)
            : new AgentLineInspection(AgentLineClassification.Output);
}
