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

    /// <summary>Operational context the plan text is copied to as Execute Plan crosses into execution (m4).</summary>
    public const string OperationalContext = ".agents/operational_context.md";

    /// <summary>Directory the milestone-extraction turn writes <c>m*.md</c> files into (m4).</summary>
    public const string MilestonesDirectory = ".agents/milestones";

    /// <summary>Glob the orchestrator uses to verify Codex produced milestone files under <see cref="MilestonesDirectory"/>.</summary>
    public const string MilestoneSearchPattern = "m*.md";

    /// <summary>Directory holding the live handoff and its rotated history.</summary>
    public const string HandoffsDirectory = ".agents/handoffs";

    /// <summary>
    /// The live handoff the start-execution turn writes. Matches <c>HandoffService.CurrentHandoffPath</c>
    /// — the plural path the entire runtime reads/writes (the plan doc's singular <c>.agents/handoff.md</c>
    /// is stale; the prompts were corrected to this path in m0).
    /// </summary>
    public const string LiveHandoff = ".agents/handoffs/handoff.md";

    /// <summary>Glob matching the rotated historical handoffs (<c>handoff.0001.md</c>, ...) but NOT the live <c>handoff.md</c>.</summary>
    public const string HistoricalHandoffSearchPattern = "handoff.*.md";

    /// <summary>Rotated historical handoff path: <c>handoff.0001.md</c>, <c>handoff.0002.md</c>, ... (run-scoped 4-digit counter, m4).</summary>
    public static string HistoricalHandoff(int sequence) => $".agents/handoffs/handoff.{sequence:0000}.md";
}
