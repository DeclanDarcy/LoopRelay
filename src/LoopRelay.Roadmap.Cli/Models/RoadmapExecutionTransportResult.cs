using LoopRelay.Agents.Primitives;
using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record RoadmapExecutionTransportResult(
    ExecutionTransportStatus Status,
    string AgentState,
    string Output,
    string Diagnostics,
    string? EvidencePath = null)
{
    public static RoadmapExecutionTransportResult Completed(string output, string? evidencePath = null) =>
        new(ExecutionTransportStatus.Completed, AgentTurnState.Completed.ToString(), output, string.Empty, evidencePath);

    public static RoadmapExecutionTransportResult Failed(
        string agentState,
        string diagnostics,
        string output = "",
        string? evidencePath = null) =>
        new(ExecutionTransportStatus.Failed, agentState, output, diagnostics, evidencePath);
}
