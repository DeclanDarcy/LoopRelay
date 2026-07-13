using LoopRelay.Orchestration.Workflows;
using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Orchestration.Services;

public sealed record PlanScopedArtifactOperationSpec(
    WorkflowTransitionIdentity Transition,
    string PromptIdentity,
    string Label,
    IReadOnlyList<string> AllowedReads,
    IReadOnlyList<OperationPathGlob> AllowedReadGlobs,
    IReadOnlyList<string> AllowedWrites,
    IReadOnlyList<OperationPathGlob> AllowedWriteGlobs,
    IReadOnlyList<string> RequiredOutputs,
    OperationPathGlob? RequiredOutputGlob,
    string? ChangedGuard,
    bool RequireChecklistInGlob,
    bool PreserveWriteGlobFileSet);

public static class PlanScopedArtifactOperationCatalog
{
    private static readonly PlanScopedArtifactOperationSpec[] Operations =
    [
        new(
            new WorkflowTransitionIdentity("CollectExecutionDetails"),
            "CollectDetails",
            "collect-details",
            [OrchestrationArtifactPaths.Plan],
            [new OperationPathGlob(OrchestrationArtifactPaths.SpecsDirectory, "*.md")],
            [OrchestrationArtifactPaths.Details],
            [],
            [OrchestrationArtifactPaths.Details],
            null,
            null,
            false,
            false),
        new(
            new WorkflowTransitionIdentity("GenerateExecutionMilestones"),
            "ExtractMilestones",
            "extract-milestones",
            [OrchestrationArtifactPaths.Plan, OrchestrationArtifactPaths.Details],
            [],
            [OrchestrationArtifactPaths.Plan],
            [new OperationPathGlob(OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern)],
            [],
            new OperationPathGlob(OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern),
            OrchestrationArtifactPaths.Plan,
            true,
            false),
        new(
            new WorkflowTransitionIdentity("RefineExecutionDetails"),
            "ExtractDetails",
            "extract-details",
            [OrchestrationArtifactPaths.Details],
            [new OperationPathGlob(OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern)],
            [OrchestrationArtifactPaths.Details],
            [new OperationPathGlob(OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern)],
            [OrchestrationArtifactPaths.Details],
            null,
            null,
            false,
            true),
    ];

    private static readonly IReadOnlyDictionary<WorkflowTransitionIdentity, PlanScopedArtifactOperationSpec> ByTransition =
        Operations.ToDictionary(operation => operation.Transition);

    public static IReadOnlyList<PlanScopedArtifactOperationSpec> All => Operations;

    public static bool Supports(WorkflowTransitionIdentity transition) =>
        ByTransition.ContainsKey(transition);

    public static bool TryGet(
        WorkflowTransitionIdentity transition,
        out PlanScopedArtifactOperationSpec operation)
    {
        bool found = ByTransition.TryGetValue(transition, out PlanScopedArtifactOperationSpec? resolved);
        operation = resolved!;
        return found;
    }

    public static PlanScopedArtifactOperationSpec Get(WorkflowTransitionIdentity transition) =>
        ByTransition.TryGetValue(transition, out PlanScopedArtifactOperationSpec? operation)
            ? operation
            : throw new InvalidOperationException($"No Plan scoped artifact operation is registered for `{transition}`.");
}
