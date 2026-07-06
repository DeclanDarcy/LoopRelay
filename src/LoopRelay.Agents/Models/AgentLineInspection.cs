namespace LoopRelay.Agents.Models;

public enum AgentLineClassification
{
    /// <summary>The line contributes to the turn's output text (verbatim, or <see cref="AgentLineInspection.Content"/> when extracted).</summary>
    Output,

    /// <summary>The line marks the end of the turn; <see cref="AgentLineInspection.Usage"/> may carry agent-reported token usage.</summary>
    TurnCompleted,

    /// <summary>The line is protocol noise (reasoning, tool calls, lifecycle events) and is neither output nor a boundary.</summary>
    Ignored
}

public sealed record AgentLineInspection(
    AgentLineClassification Classification,
    AgentTokenUsage? Usage = null,
    string? Content = null);
