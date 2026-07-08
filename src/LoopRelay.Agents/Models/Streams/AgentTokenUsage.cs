namespace LoopRelay.Agents.Models;

/// <summary>
/// Token accounting for one turn. <paramref name="CachedInputTokens"/> is the cached SUBSET of
/// <paramref name="PromptTokens"/> (i.e. <c>CachedInputTokens ≤ PromptTokens</c>); the fresh input is
/// <c>PromptTokens − CachedInputTokens</c>. It is surfaced so a cost model can weight cached vs fresh
/// input (cached usually bills far cheaper), which the cost-aware decision router needs. Defaults to 0,
/// so callers/estimators that don't know the cached split (e.g. the deterministic fallback) stay correct.
/// </summary>
public sealed record AgentTokenUsage(int PromptTokens, int OutputTokens, int CachedInputTokens = 0)
{
    public static AgentTokenUsage Zero { get; } = new(0, 0);

    public int TotalTokens => PromptTokens + OutputTokens;

    public AgentTokenUsage Add(AgentTokenUsage other) =>
        new(
            PromptTokens + other.PromptTokens,
            OutputTokens + other.OutputTokens,
            CachedInputTokens + other.CachedInputTokens);
}
