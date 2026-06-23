using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IOptionValidationService
{
    DecisionOptionValidationResult ValidateOption(
        DecisionOption option,
        DecisionCandidate candidate,
        IReadOnlyList<DecisionOption> acceptedOptions);
}
