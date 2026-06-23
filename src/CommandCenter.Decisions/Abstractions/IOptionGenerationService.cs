using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IOptionGenerationService
{
    DecisionOptionGenerationResult GenerateOptions(
        DecisionCandidate candidate,
        IReadOnlyList<DecisionEvidence> evidence);
}
