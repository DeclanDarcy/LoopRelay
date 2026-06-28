namespace CommandCenter.Orchestration;

/// <summary>
/// Repository-relative artifact paths the orchestrator reads/writes through <c>IArtifactStore</c>.
/// Behaviour-bearing paths (specs, handoffs, decisions, operational_delta) land as the lifecycle
/// phases that own them are built (m3-m7); m2 needs only the plan-existence gate.
/// </summary>
public static class OrchestrationArtifactPaths
{
    public const string Plan = ".agents/plan.md";

    /// <summary>Roadmap textarea, persisted before the initial planning prompt runs (m3).</summary>
    public const string SpecsRoadmap = ".agents/specs/roadmap.md";

    /// <summary>Repository-relative path of the <c>n</c>-th Spec textarea (1-based: <c>s1.md</c>, <c>s2.md</c>, ...).</summary>
    public static string Spec(int index) => $".agents/specs/s{index}.md";
}
