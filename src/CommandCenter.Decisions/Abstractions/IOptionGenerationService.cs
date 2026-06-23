using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IOptionGenerationService
{
    IReadOnlyList<DecisionOption> GenerateOptions(
        DecisionCandidate candidate,
        IReadOnlyList<DecisionEvidence> evidence);
}
