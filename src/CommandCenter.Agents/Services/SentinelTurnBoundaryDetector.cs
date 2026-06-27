using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Services;

/// <summary>
/// Detects per-turn completion from a line-oriented stream using a literal sentinel.
/// This is the seam where real Codex <c>proto</c>/app-server event parsing plugs in once
/// the held-open protocol is validated against the installed Codex (see m1 "Validate the
/// Codex protocol that can support held-open interaction"). The sentinel keeps the runtime
/// transport-agnostic and testable until then.
/// </summary>
public sealed class SentinelTurnBoundaryDetector(
    string sentinel = SentinelTurnBoundaryDetector.DefaultSentinel) : IAgentTurnBoundaryDetector
{
    public const string DefaultSentinel = "<<<CC_TURN_COMPLETE>>>";

    public AgentLineInspection Inspect(string line) =>
        line.StartsWith(sentinel, StringComparison.Ordinal)
            ? new AgentLineInspection(AgentLineClassification.TurnCompleted)
            : new AgentLineInspection(AgentLineClassification.Output);
}
