namespace CommandCenter.Orchestration;

/// <summary>
/// Repository-relative artifact paths the orchestrator reads/writes through <c>IArtifactStore</c>.
/// Behaviour-bearing paths (specs, handoffs, decisions, operational_delta) land as the lifecycle
/// phases that own them are built (m3-m7); m2 needs only the plan-existence gate.
/// </summary>
public static class OrchestrationArtifactPaths
{
    /// <summary>
    /// Repository-relative root of the agent artifacts, now a git submodule. The CLI loop commits/pushes
    /// this submodule directly; the parent repo only ever sees it as a single gitlink entry in
    /// <c>git status</c>, which is why <c>CommitGate</c> treats a lone <c>.agents</c> change as bookkeeping.
    /// </summary>
    public const string AgentsDirectory = ".agents";

    public const string Plan = ".agents/plan.md";

    /// <summary>
    /// Optional companion to <see cref="Plan"/>: extended detail a non-self-contained plan.md declares as a
    /// required addendum. Injected into the execution prompt directly after the plan (same existence-guarded
    /// treatment: read when present, rendered as the empty string when absent) so the execution agent never
    /// has to chase the file on disk.
    /// </summary>
    public const string Details = ".agents/details.md";

    /// <summary>Epic textarea, persisted before the initial planning prompt runs (m3).</summary>
    public const string SpecsEpic = ".agents/specs/epic.md";

    /// <summary>Repository-relative path of the <c>n</c>-th Spec textarea (1-based: <c>s1.md</c>, <c>s2.md</c>, ...).</summary>
    public static string Spec(int index) => $".agents/specs/s{index}.md";

    /// <summary>Operational context the plan text is copied to as Execute Plan crosses into execution (m4).</summary>
    public const string OperationalContext = ".agents/operational_context.md";

    /// <summary>
    /// The operational delta a Transfer extracts from the warm Decision process (<c>ProduceOperationalDelta</c>)
    /// before recycling it — the input <c>UpdateOperationalContext</c> folds into the next
    /// <see cref="OperationalContext"/> revision (m7).
    /// </summary>
    public const string OperationalDelta = ".agents/operational_delta.md";

    /// <summary>Directory holding the archived operational deltas rotated out of the live <see cref="OperationalDelta"/>
    /// once a Transfer has folded each into the next <see cref="OperationalContext"/> revision (numbered history).</summary>
    public const string DeltasDirectory = ".agents/deltas";

    /// <summary>Glob matching the archived operational deltas (<c>operational_delta.0001.md</c>, ...) under
    /// <see cref="DeltasDirectory"/> but NOT the live single-dot <see cref="OperationalDelta"/>.</summary>
    public const string HistoricalDeltaSearchPattern = "operational_delta.*.md";

    /// <summary>Archived operational-delta path: <c>.agents/deltas/operational_delta.0001.md</c>, ... Each successful
    /// Transfer rotates the consumed live delta here (run-scoped 4-digit counter) after the context update succeeds.</summary>
    public static string HistoricalDelta(int sequence) => $".agents/deltas/operational_delta.{sequence:0000}.md";

    /// <summary>
    /// The current governance decisions the human review gate persists (m5). This is the canonical path
    /// every independent decisions consumer reads (<c>ArtifactService.GetCurrentDecisionsAsync</c>,
    /// <c>DecisionContextService</c>, <c>ArtifactRotationService</c>, continuity), so the Decision Runtime
    /// writes the human-approved decisions HERE — and only here — to feed the next execution turn.
    /// </summary>
    public const string Decisions = ".agents/decisions/decisions.md";

    /// <summary>Directory holding the live <c>decisions.md</c> and its rotated submission history (m6).</summary>
    public const string DecisionsDirectory = ".agents/decisions";

    /// <summary>
    /// Glob matching the rotated decision submissions (<c>decisions.0001.md</c>, ...) but NOT the live
    /// <c>decisions.md</c> (single-dot) — symmetric with <see cref="HistoricalHandoffSearchPattern"/>.
    /// </summary>
    public const string HistoricalDecisionSearchPattern = "decisions.*.md";

    /// <summary>
    /// Rotated decision submission path: <c>decisions.0001.md</c>, <c>decisions.0002.md</c>, ... Each human
    /// Submit persists a numbered copy for history/recovery alongside rewriting the live <see cref="Decisions"/>
    /// the next continuation reads (run-scoped 4-digit counter, m6).
    /// </summary>
    public static string HistoricalDecision(int sequence) => $".agents/decisions/decisions.{sequence:0000}.md";

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
