namespace LoopRelay.Agents.Abstractions;

public interface IAgentTokenEstimator
{
    int Estimate(string text);
}
