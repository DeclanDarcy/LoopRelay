namespace CommandCenter.DecisionSessions.Abstractions;

public interface ITokenEstimator
{
    long EstimateTokenCount(string? text);
}
