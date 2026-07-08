namespace LoopRelay.Agents.Models;

public enum AgentLineClassification
{
    /// <summary>The line contributes to the turn's output text (verbatim, or <see cref="AgentLineInspection.Content"/> when extracted).</summary>
    Output,

    /// <summary>The line is a display-only tool invocation and must not be folded into the turn output.</summary>
    ToolCall,

    /// <summary>The line marks the end of the turn; <see cref="AgentLineInspection.Usage"/> may carry agent-reported token usage.</summary>
    TurnCompleted,

    /// <summary>The line is protocol noise, such as reasoning or lifecycle events, and is neither output nor a boundary.</summary>
    Ignored
}
