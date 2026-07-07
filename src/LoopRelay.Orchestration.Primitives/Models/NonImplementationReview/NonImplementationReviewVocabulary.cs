namespace LoopRelay.Orchestration.Models.NonImplementationReview;

/// <summary>
/// Deterministic classifier routes for changed files before any semantic review runs.
/// </summary>
public enum NonImplementationArtifactRoute
{
    ExcludedImplementationArtifact,
    ExcludedMachineRequiredArtifact,
    ExcludedSanctionedOperationalArtifact,
    SemanticReviewCandidate,
    AmbiguousForSemanticReview,
}

/// <summary>
/// Semantic confirmation outcomes for files routed to read-only review.
/// </summary>
public enum NonImplementationSemanticDisposition
{
    ConfirmedNonImplementation,
    FalsePositive,
    Uncertain,
}

/// <summary>
/// Durable HITL resolution state for ledger entries.
/// </summary>
public enum NonImplementationResolutionState
{
    Unresolved,
    HitlKept,
    HitlDeleted,
    HitlFalsePositive,
    HitlDeferred,
}

/// <summary>
/// Provenance for an allowed human-in-the-loop non-implementation artifact.
/// </summary>
public enum NonImplementationHitlProvenanceKind
{
    None,
    HitlRequested,
    HitlKept,
}

public static class NonImplementationReviewTerms
{
    public const string ImplementationArtifact = "Implementation artifact";
    public const string MachineRequiredArtifact = "Machine-required artifact";
    public const string SanctionedOperationalArtifact = "Sanctioned operational artifact";
    public const string SemanticReviewCandidate = "Semantic review candidate";
    public const string AmbiguousForSemanticReview = "Ambiguous for semantic review";
    public const string ConfirmedNonImplementationFile = "Confirmed non-implementation file";
    public const string FalsePositive = "False positive";
    public const string SemanticUncertainty = "Semantic uncertainty";
    public const string HitlRequestedNonImplementationFile = "HITL-requested non-implementation file";
    public const string FreeFormInsightSynthesis = "Free-form insight synthesis";
}

public static class NonImplementationReviewOwnership
{
    public const string SliceBaselineAndChangeDetection = "LoopRelay.Orchestration.Primitives";
    public const string SemanticConfirmationAndSynthesis = "INonImplementationReviewRunner";
    public const string MainCliPostExecutionInvocation =
        "LoopRelay.Cli after execution writes and before .agents post-execution publish";
    public const string EpicCompletionReview =
        "LoopRelay.Completion before final completion evaluation closes the epic";
    public const string RoadmapPlanningPromptPolicy =
        "Centralized ImplementationFirstPromptPolicyComposer";
}
