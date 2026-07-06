namespace LoopRelay.Orchestration.Models;

/// <summary>
/// Repository-level plan lifecycle projection surfaced by <c>GET /plan/status</c>. It is the
/// "or map them through an equivalent repository lifecycle projection" option from the milestone:
/// a small, additive projection that does NOT perturb the operational <c>RepositoryExecutionState</c>
/// (which the legacy AwaitingAcceptance flow owns and the UI already binds to).
/// </summary>
public enum PlanLifecycleState
{
    /// <summary>No <c>.agents/plan.md</c> exists yet — the repository view opens Plan Authoring.</summary>
    PlanAuthoring,

    /// <summary>A plan exists — the repository is executing (or ready to execute) that plan.</summary>
    ExecutingPlan
}
