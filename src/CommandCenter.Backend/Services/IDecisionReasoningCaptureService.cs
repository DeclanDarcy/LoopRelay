using CommandCenter.Decisions.Models;

namespace CommandCenter.Backend.Services;

public interface IDecisionReasoningCaptureService
{
    Task CaptureDecisionSupersededAsync(
        Guid repositoryId,
        Decision supersededDecision,
        SupersedeDecisionCommand command);
}
