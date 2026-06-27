using CommandCenter.Agents.Abstractions;

namespace CommandCenter.Agents.Services;

/// <summary>
/// Deterministic token estimate (<c>(len + 3) / 4</c>) used as the governed fallback
/// until real Codex token accounting is available (see m1 "transcript and token accounting hooks").
/// </summary>
public sealed class DeterministicAgentTokenEstimator : IAgentTokenEstimator
{
    public int Estimate(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;
}
