using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

public interface IOptionValidationService
{
    DecisionOptionValidationResult ValidateOption(
        DecisionOption option,
        DecisionCandidate candidate,
        IReadOnlyList<DecisionOption> acceptedOptions);
}
