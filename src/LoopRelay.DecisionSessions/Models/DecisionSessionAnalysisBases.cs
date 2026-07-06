using LoopRelay.Persistence.Sqlite.Abstractions;

namespace LoopRelay.DecisionSessions.Models;

/// <summary>
/// Shared identity for the count-derived analysis caches (refactor-lazy-sqlite.md, Phase 3). The formula
/// version busts every dependent base when a formula/threshold changes; the per-kind source families scope
/// invalidation so a change to one family only busts the kinds that depend on it.
/// </summary>
public static class DecisionSessionAnalysisCache
{
    /// <summary>
    /// The version identifying the metrics/economics/coherence/lifecycle analysis formulas. Bump this when
    /// any of those formulas change so cached bases stamped under an older version are treated as a miss.
    /// (Ported from the recovery service's <c>AnalysisOptionsVersion</c>.)
    /// </summary>
    public const string FormulaVersion = "decision-sessions.analysis.v1";

    public const string MetricsKind = "metrics-base";
    public const string EconomicsKind = "economics-base";
    public const string CoherenceKind = "coherence-base";

    /// <summary>
    /// Metrics depends on decisions, reasoning, and operational-context content. Economics derives from the
    /// metrics base, so it shares the same source families.
    /// </summary>
    public static readonly SourceFamily[] MetricsFamilies =
    [
        SourceFamily.Decisions,
        SourceFamily.Reasoning,
        SourceFamily.OperationalContext
    ];

    /// <summary>Economics depends on metrics; its invalidation tracks the same families.</summary>
    public static readonly SourceFamily[] EconomicsFamilies = MetricsFamilies;

    /// <summary>Coherence depends only on the reasoning graph (the design's <c>coherence-base &lt;- {reasoning}</c>).</summary>
    public static readonly SourceFamily[] CoherenceFamilies =
    [
        SourceFamily.Reasoning
    ];
}

/// <summary>
/// The SOURCE-PURE metrics base: counts/bytes/tokens + base timestamps. Cache-safe — no measuredAt-relative
/// field is present, so caching it can never freeze a boot-time <c>now</c>. The wire snapshot
/// <see cref="DecisionSessionMetricsSnapshot"/> is projected from this base + a fresh clock on every read.
/// </summary>
public sealed record DecisionSessionMetricsBase(
    Guid RepositoryId,
    DateTimeOffset? CreatedAt,
    DateTimeOffset SessionStartedAt,
    DateTimeOffset LastActivityAt,
    long EvidenceItemCount,
    long ContextByteSize,
    long TotalCharacters,
    long EstimatedTokenCount,
    long ReasoningEventCount,
    long ReasoningThreadCount,
    long ReasoningRelationshipCount,
    long DecisionCount,
    long DecisionCandidateCount,
    long DecisionProposalCount,
    long OperationalContextRevisionCount,
    IReadOnlyList<DecisionSessionMetricsSourceDiagnostic> Sources,
    IReadOnlyList<string> Warnings);

/// <summary>
/// The SOURCE-PURE economics base: cost/benefit terms that depend only on the pure metrics counts (no clock
/// dependence). The time-dependent economics fields (transferValue/reuseValue/cacheBenefit) are recomputed
/// at read time from the freshly-projected metrics statistics.
/// </summary>
public sealed record DecisionSessionEconomicsBase(
    decimal ContextCost,
    decimal ReasoningCost,
    ContinuityBenefitAssessment ContinuityBenefit,
    decimal ReusableCorpusScore);

/// <summary>
/// The SOURCE-PURE coherence base: graph topology (incl. the connected-components BFS) plus the deterministic
/// density/fragmentation/continuity scores and the composite coherenceScore — all functions of the reasoning
/// graph + pure metrics counts. Only <c>transferPressure</c> is time-dependent and is recomputed on read.
/// </summary>
public sealed record DecisionSessionCoherenceBase(
    long NodeCount,
    long RelationshipCount,
    long IsolatedNodeCount,
    long DisconnectedGroupCount,
    long ResolvedNodeCount,
    long UnresolvedNodeCount,
    DensityAssessment Density,
    FragmentationAssessment Fragmentation,
    ContinuityQualityAssessment Continuity,
    decimal CoherenceScore,
    IReadOnlyList<string> GraphDiagnostics);
