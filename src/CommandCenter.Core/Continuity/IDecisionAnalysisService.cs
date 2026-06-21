namespace CommandCenter.Core.Continuity;

public interface IDecisionAnalysisService
{
    DecisionAnalysisResult Analyze(IReadOnlyList<DecisionArtifactInput> decisionArtifacts);
}
