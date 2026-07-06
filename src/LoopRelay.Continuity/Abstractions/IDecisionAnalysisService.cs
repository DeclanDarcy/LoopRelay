using LoopRelay.Continuity.Models;

namespace LoopRelay.Continuity.Abstractions;

public interface IDecisionAnalysisService
{
    DecisionAnalysisResult Analyze(IReadOnlyList<DecisionArtifactInput> decisionArtifacts);
}
