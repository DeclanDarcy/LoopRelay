using CommandCenter.DecisionSessions.Abstractions;

namespace CommandCenter.DecisionSessions.Services;

public sealed class DeterministicTokenEstimator : ITokenEstimator
{
    public long EstimateTokenCount(string? text)
    {
        return string.IsNullOrEmpty(text) ? 0 : (text.Length + 3L) / 4L;
    }
}
