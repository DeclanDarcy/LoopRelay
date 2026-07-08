using LoopRelay.Completion.Models;
using LoopRelay.Roadmap.Cli.Primitives;
using LoopRelay.Roadmap.Cli.Services;

namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record RoadmapUnblockPlan(
    RoadmapUnblockAction Action,
    RoadmapUnblockPlanStatus Status,
    RoadmapState SourceState,
    RoadmapTransitionIntent TransitionIntent,
    string Reason,
    IReadOnlyList<RoadmapUnblockEvidence> Evidence,
    string RequiredNextStep,
    RoadmapState? TargetState,
    string Decision,
    string? PrimaryEvidencePath = null,
    ExecutionDispositionValidationResult? ExecutionValidation = null,
    CompletionCertificationPolicyResult? CompletionCertification = null,
    CompletionCertificationRoute? CompletionRoute = null)
{
    public static RoadmapUnblockPlan Success(
        RoadmapUnblockAction action,
        RoadmapState sourceState,
        RoadmapTransitionIntent transitionIntent,
        string reason,
        IReadOnlyList<RoadmapUnblockEvidence> evidence,
        RoadmapState targetState,
        string decision) =>
        new(
            action,
            RoadmapUnblockPlanStatus.Success,
            sourceState,
            transitionIntent,
            reason,
            evidence,
            "Run the roadmap CLI to continue from the recovered state.",
            targetState,
            decision);

    public static RoadmapUnblockPlan ExecutionDispositionRecovered(
        RoadmapState sourceState,
        RoadmapTransitionIntent transitionIntent,
        string reason,
        IReadOnlyList<RoadmapUnblockEvidence> evidence,
        string primaryEvidencePath,
        ExecutionDispositionValidationResult validation) =>
        new(
            RoadmapUnblockAction.RecoverExecutionDisposition,
            RoadmapUnblockPlanStatus.Success,
            sourceState,
            transitionIntent,
            reason,
            evidence,
            "Run the roadmap CLI to continue from the recovered execution disposition route.",
            validation.Route!.TargetState,
            validation.Disposition.StatusText,
            primaryEvidencePath,
            validation);

    public static RoadmapUnblockPlan CompletionCertificationRecovered(
        RoadmapState sourceState,
        RoadmapTransitionIntent transitionIntent,
        string reason,
        IReadOnlyList<RoadmapUnblockEvidence> evidence,
        string primaryEvidencePath,
        CompletionCertificationPolicyResult certification,
        CompletionCertificationRoute route) =>
        new(
            RoadmapUnblockAction.RecoverCompletionCertification,
            RoadmapUnblockPlanStatus.Success,
            sourceState,
            transitionIntent,
            reason,
            evidence,
            "Run the roadmap CLI to continue from the recovered completion route.",
            RoadmapCompletionRouteMapper.Map(route).TargetState,
            certification.Decision.ClosureRecommendation,
            primaryEvidencePath,
            CompletionCertification: certification,
            CompletionRoute: route);

    public static RoadmapUnblockPlan Failed(
        RoadmapUnblockAction action,
        RoadmapState sourceState,
        RoadmapTransitionIntent transitionIntent,
        string reason,
        IReadOnlyList<RoadmapUnblockEvidence> evidence,
        string requiredNextStep) =>
        new(
            action,
            RoadmapUnblockPlanStatus.Failed,
            sourceState,
            transitionIntent,
            reason,
            evidence,
            requiredNextStep,
            null,
            "Unblock Review Failed");

    public static RoadmapUnblockPlan Unsupported(
        RoadmapState sourceState,
        RoadmapTransitionIntent transitionIntent,
        string reason,
        IReadOnlyList<RoadmapUnblockEvidence> evidence,
        string requiredNextStep) =>
        new(
            RoadmapUnblockAction.ReportOnly,
            RoadmapUnblockPlanStatus.Unsupported,
            sourceState,
            transitionIntent,
            reason,
            evidence,
            requiredNextStep,
            null,
            "Unblock Unsupported");
}
