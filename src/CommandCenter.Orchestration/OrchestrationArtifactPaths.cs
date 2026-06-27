namespace CommandCenter.Orchestration;

/// <summary>
/// Repository-relative artifact paths the orchestrator reads/writes through <c>IArtifactStore</c>.
/// Behaviour-bearing paths (specs, handoffs, decisions, operational_delta) land as the lifecycle
/// phases that own them are built (m3-m7); m2 needs only the plan-existence gate.
/// </summary>
public static class OrchestrationArtifactPaths
{
    public const string Plan = ".agents/plan.md";
}
