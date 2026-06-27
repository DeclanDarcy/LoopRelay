namespace CommandCenter.Agents.Models;

public sealed record AgentTokenUsage(int PromptTokens, int OutputTokens)
{
    public static AgentTokenUsage Zero { get; } = new(0, 0);

    public int TotalTokens => PromptTokens + OutputTokens;

    public AgentTokenUsage Add(AgentTokenUsage other) =>
        new(PromptTokens + other.PromptTokens, OutputTokens + other.OutputTokens);
}
