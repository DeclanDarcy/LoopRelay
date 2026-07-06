using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

public interface IOptionGenerationService
{
    DecisionOptionGenerationResult GenerateOptions(
        DecisionCandidate candidate,
        IReadOnlyList<DecisionEvidence> evidence);
}
