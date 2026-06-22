using CommandCenter.Continuity.Models;

namespace CommandCenter.Continuity.Abstractions;

public interface IDecisionAnalysisService
{
    DecisionAnalysisResult Analyze(IReadOnlyList<DecisionArtifactInput> decisionArtifacts);
}
