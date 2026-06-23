using CommandCenter.Decisions.Models;

namespace CommandCenter.Backend.Services;

public interface IDecisionReasoningCaptureService
{
    Task CaptureProposalResolvedAsync(
        Guid repositoryId,
        Decision decision,
        ResolveDecisionCommand command);

    Task CaptureDecisionSupersededAsync(
        Guid repositoryId,
        Decision supersededDecision,
        SupersedeDecisionCommand command);
}
