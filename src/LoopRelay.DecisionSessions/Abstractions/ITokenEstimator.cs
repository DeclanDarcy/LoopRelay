namespace LoopRelay.DecisionSessions.Abstractions;

public interface ITokenEstimator
{
    long EstimateTokenCount(string? text);
}
