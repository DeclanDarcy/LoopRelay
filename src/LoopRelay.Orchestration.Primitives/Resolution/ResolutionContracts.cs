using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Resolution;

public enum StorageAuthorityKind
{
    Missing,
    FilesystemExport,
    ImportedSqlite,
    CanonicalSqlite,
    Mixed,
    Corrupt,
    Unsupported,
    Ambiguous,
}

public enum RepositoryClassification
{
    Fresh,
    InProgress,
    Blocked,
    Waiting,
    Completed,
    Cancelled,
    Failed,
    Ambiguous,
    Corrupt,
    Unsupported,
}

public enum WorkflowResolutionState
{
    Absent,
    EligibleToStart,
    Active,
    Resumable,
    Completed,
    Blocked,
    Waiting,
    Cancelled,
    Failed,
    Invalid,
    Ambiguous,
}

public enum TransitionEligibilityState
{
    Eligible,
    Blocked,
    Waiting,
    Invalid,
    Completed,
    Ambiguous,
}

public enum InvocationModeKind
{
    DefaultChained,
    ForcedEvalChain,
    ForcedTraditionalChain,
    BoundedEval,
    BoundedTraditional,
    BoundedPlan,
    BoundedExecute,
}

public enum BlockerCategory
{
    Storage,
    Workflow,
    Stage,
    Transition,
    Validation,
    Human,
    Permission,
    Recovery,
    Repository,
}

public enum AmbiguityCategory
{
    Workflow,
    Stage,
    Authority,
    Repository,
    Storage,
    Recovery,
    Completion,
}

public enum HumanInteractionCategory
{
    Approval,
    Review,
    RoadmapRevision,
    StrategicInvestigation,
    Permission,
    EvidenceRepair,
    CompletionDecision,
}

public sealed record StorageAuthoritySnapshot(
    StorageAuthorityKind Authority,
    bool UsableAuthority,
    string ConfidenceQualifier,
    IReadOnlyList<string> Evidence);

public sealed record StorageVerificationResult(
    StorageAuthorityKind Authority,
    bool UsableAuthority,
    IReadOnlyList<string> StaleExports,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> Corruption,
    IReadOnlyList<string> UnsupportedSchema,
    IReadOnlyList<string> UnresolvedReferences,
    IReadOnlyList<string> PartialTransactions,
    IReadOnlyList<ResolutionBlocker> BlockingConditions,
    IReadOnlyList<string> Evidence)
{
    public bool IsBlocked =>
        !UsableAuthority ||
        BlockingConditions.Count > 0 ||
        Conflicts.Count > 0 ||
        Corruption.Count > 0 ||
        UnsupportedSchema.Count > 0 ||
        PartialTransactions.Count > 0;
}

public sealed record RepositoryObservation(
    string RepositoryPath,
    StorageAuthoritySnapshot StorageAuthority,
    IReadOnlyList<ObservedWorkflowState> WorkflowStates,
    IReadOnlyList<ObservedProduct> Products,
    IReadOnlyList<ObservedLifecycleRow> LifecycleRows,
    IReadOnlyList<ObservedEvidence> Evidence,
    IReadOnlyList<ObservedTransitionRun> TransitionRuns,
    ObservedGitFacts GitFacts,
    IReadOnlyList<HumanInteractionRequirement> HumanInteractionRequirements,
    IReadOnlyList<string> EvaluationIntentPaths,
    StorageVerificationResult StorageVerification);

public sealed record ObservedWorkflowState(
    WorkflowIdentity Workflow,
    WorkflowResolutionState State,
    WorkflowStageIdentity? CurrentStage,
    IReadOnlyList<WorkflowStageIdentity> CompletedStages,
    IReadOnlyList<ResolutionBlocker> Blockers,
    IReadOnlyList<string> Evidence);

public sealed record ObservedProduct(
    ProductRecord Product,
    bool GateUsable,
    IReadOnlyList<string> Evidence);

public sealed record ObservedLifecycleRow(
    string Identity,
    string State,
    IReadOnlyList<string> Evidence);

public sealed record ObservedEvidence(
    string Identity,
    string Location,
    string Authority,
    bool Ignored);

public sealed record ObservedTransitionRun(
    WorkflowIdentity Workflow,
    WorkflowStageIdentity Stage,
    WorkflowTransitionIdentity Transition,
    TransitionEligibilityState State,
    IReadOnlyList<ProductIdentity> ProducedProducts,
    IReadOnlyList<string> Evidence);

public sealed record ObservedGitFacts(
    bool IsRepository,
    bool HasWorkingTreeChanges,
    string CurrentBranch,
    IReadOnlyList<string> Evidence);

public sealed record HumanInteractionRequirement(
    HumanInteractionCategory Category,
    string Reason,
    string Authority,
    string BlockingScope,
    IReadOnlyList<string> Evidence);

public sealed record ResolutionBlocker(
    BlockerCategory Category,
    string Reason,
    string Authority,
    string RequiredAction,
    bool Recoverable,
    IReadOnlyList<string> Evidence);

public sealed record ResolutionAmbiguity(
    AmbiguityCategory Category,
    string Reason,
    IReadOnlyList<string> ConflictingFacts,
    IReadOnlyList<string> Evidence);

public sealed record WorkflowInvocation(InvocationModeKind Mode)
{
    public bool IsBounded =>
        Mode is InvocationModeKind.BoundedEval
            or InvocationModeKind.BoundedTraditional
            or InvocationModeKind.BoundedPlan
            or InvocationModeKind.BoundedExecute;
}

public sealed record WorkflowSelectionResult(
    InvocationModeKind InvocationMode,
    WorkflowIdentity SelectedWorkflow,
    string SelectedChain,
    bool IsBounded,
    IReadOnlyList<string> Evidence,
    string Explanation);

public sealed record TransitionEligibility(
    WorkflowTransitionIdentity Transition,
    TransitionEligibilityState State,
    IReadOnlyList<string> SatisfiedGates,
    IReadOnlyList<string> UnsatisfiedGates,
    IReadOnlyList<ResolutionBlocker> Blockers,
    IReadOnlyList<string> Evidence);

public sealed record WorkflowResolutionResult(
    RepositoryClassification Classification,
    WorkflowSelectionResult Selection,
    WorkflowResolutionState WorkflowState,
    WorkflowStageIdentity? SelectedStage,
    IReadOnlyList<TransitionEligibility> TransitionEligibility,
    ResolutionExplanation Explanation);

public sealed record ResolutionExplanation(
    string Decision,
    WorkflowIdentity SelectedWorkflow,
    WorkflowStageIdentity? SelectedStage,
    IReadOnlyList<WorkflowTransitionIdentity> EligibleTransitions,
    IReadOnlyList<string> SatisfiedGates,
    IReadOnlyList<string> UnsatisfiedGates,
    IReadOnlyList<ResolutionBlocker> Blockers,
    IReadOnlyList<string> Evidence,
    StorageAuthoritySnapshot Authority,
    IReadOnlyList<string> IgnoredEvidence,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<ResolutionAmbiguity> Ambiguities,
    string ConfidenceQualifier,
    string RemainingUncertainty);

public interface IStorageVerifier
{
    Task<StorageVerificationResult> VerifyAsync(string repositoryPath, CancellationToken cancellationToken);
}
