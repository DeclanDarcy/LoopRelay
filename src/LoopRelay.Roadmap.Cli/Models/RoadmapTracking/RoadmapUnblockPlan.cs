using LoopRelay.Completion.Models.Certification;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Services.Execution;

namespace LoopRelay.Roadmap.Cli.Models.RoadmapTracking;

internal sealed record RoadmapUnblockPlan(
    RoadmapUnblockAction Action,
    RoadmapUnblockPlanStatus Status,
    Primitives.State.RoadmapState SourceState,
    RoadmapTransitionIntent TransitionIntent,
    string Reason,
    IReadOnlyList<RoadmapUnblockEvidence> Evidence,
    string RequiredNextStep,
    Primitives.State.RoadmapState? TargetState,
    string Decision,
    string? PrimaryEvidencePath = null,
    ExecutionDispositionValidationResult? ExecutionValidation = null,
    CompletionCertificationPolicyResult? CompletionCertification = null,
    CompletionCertificationRoute? CompletionRoute = null)
{
    public static RoadmapUnblockPlan Success(
        RoadmapUnblockAction action,
        Primitives.State.RoadmapState sourceState,
        RoadmapTransitionIntent transitionIntent,
        string reason,
        IReadOnlyList<RoadmapUnblockEvidence> evidence,
        Primitives.State.RoadmapState targetState,
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
        Primitives.State.RoadmapState sourceState,
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
        Primitives.State.RoadmapState sourceState,
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
        Primitives.State.RoadmapState sourceState,
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
        Primitives.State.RoadmapState sourceState,
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
