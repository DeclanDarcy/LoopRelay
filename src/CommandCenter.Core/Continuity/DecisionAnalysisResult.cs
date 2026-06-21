namespace CommandCenter.Core.Continuity;

public sealed class DecisionAnalysisResult
{
    public IReadOnlyList<DecisionSignal> Signals { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}
