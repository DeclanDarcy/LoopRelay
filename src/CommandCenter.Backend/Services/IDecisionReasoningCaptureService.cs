using CommandCenter.Continuity.Models;
using CommandCenter.Decisions.Models;
using CommandCenter.Execution.Models;

namespace CommandCenter.Backend.Services;

public interface IDecisionReasoningCaptureService
{
    Task<IReadOnlyList<ReasoningCaptureAttemptResult>> CaptureProposalResolvedAsync(
        Guid repositoryId,
        Decision decision,
        ResolveDecisionCommand command);

    Task<IReadOnlyList<ReasoningCaptureAttemptResult>> CaptureDecisionSupersededAsync(
        Guid repositoryId,
        Decision supersededDecision,
        SupersedeDecisionCommand command);

    Task<IReadOnlyList<ReasoningCaptureAttemptResult>> CaptureDecisionArchivedAsync(
        Guid repositoryId,
        Decision archivedDecision,
        ArchiveDecisionCommand command);

    Task<IReadOnlyList<ReasoningCaptureAttemptResult>> CaptureGovernanceContradictionsAsync(
        Guid repositoryId,
        DecisionGovernanceReport report);

    Task<IReadOnlyList<ReasoningCaptureAttemptResult>> CaptureOperationalContextPromotionAsync(
        Guid repositoryId,
        OperationalContextProposal proposal);

    Task<ReasoningCaptureAttemptResult> CaptureExecutionHandoffDecisionAsync(
        ExecutionSession session,
        bool accepted);
}
