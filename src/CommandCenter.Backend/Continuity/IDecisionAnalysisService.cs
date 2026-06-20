namespace CommandCenter.Backend.Continuity;

public interface IDecisionAnalysisService
{
    DecisionAnalysisResult Analyze(IReadOnlyList<DecisionArtifactInput> decisionArtifacts);
}
