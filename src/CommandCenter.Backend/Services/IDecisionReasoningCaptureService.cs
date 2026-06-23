using CommandCenter.Continuity.Models;
using CommandCenter.Decisions.Models;
using CommandCenter.Execution.Models;

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

    Task CaptureDecisionArchivedAsync(
        Guid repositoryId,
        Decision archivedDecision,
        ArchiveDecisionCommand command);

    Task CaptureGovernanceContradictionsAsync(
        Guid repositoryId,
        DecisionGovernanceReport report);

    Task CaptureOperationalContextPromotionAsync(
        Guid repositoryId,
        OperationalContextProposal proposal);

    Task CaptureExecutionHandoffDecisionAsync(
        ExecutionSession session,
        bool accepted);
}
