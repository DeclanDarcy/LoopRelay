namespace LoopRelay.Orchestration.Workflows;

public readonly record struct WorkflowIdentity(string Value)
{
    public static WorkflowIdentity TraditionalRoadmap { get; } = new("TraditionalRoadmap");
    public static WorkflowIdentity EvalRoadmap { get; } = new("EvalRoadmap");
    public static WorkflowIdentity Plan { get; } = new("Plan");
    public static WorkflowIdentity Execute { get; } = new("Execute");

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct WorkflowStageIdentity(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct WorkflowTransitionIdentity(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct ProductIdentity(string Value)
{
    public static ProductIdentity EvaluationIntent { get; } = new("EvaluationIntent");
    public static ProductIdentity RoadmapCompletionContext { get; } = new("RoadmapCompletionContext");
    public static ProductIdentity StrategicInitiativeSelection { get; } = new("StrategicInitiativeSelection");
    public static ProductIdentity DependencyInventory { get; } = new("DependencyInventory");
    public static ProductIdentity HypothesisInventory { get; } = new("HypothesisInventory");
    public static ProductIdentity ArchitecturalCatalog { get; } = new("ArchitecturalCatalog");
    public static ProductIdentity EvalDag { get; } = new("EvalDag");
    public static ProductIdentity NextEpicRoadmap { get; } = new("NextEpicRoadmap");
    public static ProductIdentity PreparedEpic { get; } = new("PreparedEpic");
    public static ProductIdentity MilestoneSpecificationSet { get; } = new("MilestoneSpecificationSet");
    public static ProductIdentity ExecutablePlan { get; } = new("ExecutablePlan");
    public static ProductIdentity AdversarialProjection { get; } = new("AdversarialProjection");
    public static ProductIdentity AdversarialReview { get; } = new("AdversarialReview");
    public static ProductIdentity OperationalContext { get; } = new("OperationalContext");
    public static ProductIdentity ExecutionDetails { get; } = new("ExecutionDetails");
    public static ProductIdentity ExecutionMilestoneSet { get; } = new("ExecutionMilestoneSet");
    public static ProductIdentity ExecutionReadiness { get; } = new("ExecutionReadiness");
    public static ProductIdentity DecisionSet { get; } = new("DecisionSet");
    public static ProductIdentity ImplementationSlice { get; } = new("ImplementationSlice");
    public static ProductIdentity ExecutionHandoff { get; } = new("ExecutionHandoff");
    public static ProductIdentity OperationalDelta { get; } = new("OperationalDelta");
    public static ProductIdentity RepositoryChanges { get; } = new("RepositoryChanges");
    public static ProductIdentity CompletionEvidence { get; } = new("CompletionEvidence");
    public static ProductIdentity CompletionRoute { get; } = new("CompletionRoute");
    public static ProductIdentity CertifiedCompletion { get; } = new("CertifiedCompletion");

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct GateIdentity(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct EffectIdentity(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public enum DependencyTargetKind
{
    Product,
    Transition,
    Stage,
    Workflow,
}

public enum DependencyStrength
{
    Required,
    Optional,
    Advisory,
    FreshnessSensitive,
    Invalidating,
}

public enum GateStatus
{
    Satisfied,
    Unsatisfied,
    Blocked,
    Waiting,
    Invalid,
    Ambiguous,
}

public enum RuntimeOutcomeKind
{
    Completed,
    Paused,
    Blocked,
    Failed,
    Cancelled,
    Waiting,
    Stalled,
    Ambiguous,
}

public enum ExecutionPostureKind
{
    OneShotAgentPrompt,
    PersistentSession,
    WarmSession,
    ScopedArtifactOperation,
    DecisionSession,
    ReadOnlyPrompt,
}

public enum EffectCategory
{
    ProductPersistence,
    LifecycleUpdate,
    Evidence,
    DecisionRecording,
    Publication,
    Git,
    Archive,
    RecoveryBookkeeping,
    PreUnificationExport,
}

public enum ProductLifecycle
{
    Proposed,
    Active,
    Superseded,
    Archived,
    Invalid,
}

public enum ProductValidationState
{
    Unknown,
    Valid,
    Invalid,
    Stale,
    Ambiguous,
}

public enum ProductFreshness
{
    Unknown,
    Fresh,
    Stale,
}

public sealed record ExecutionPosture(
    ExecutionPostureKind Kind,
    string Purpose)
{
    public static ExecutionPosture OneShotAgentPrompt { get; } =
        new(ExecutionPostureKind.OneShotAgentPrompt, "Run one prompt and capture its output.");

    public static ExecutionPosture PersistentSession { get; } =
        new(ExecutionPostureKind.PersistentSession, "Reuse a persistent agent session.");

    public static ExecutionPosture WarmSession { get; } =
        new(ExecutionPostureKind.WarmSession, "Continue an already-warmed session.");

    public static ExecutionPosture ScopedArtifactOperation { get; } =
        new(ExecutionPostureKind.ScopedArtifactOperation, "Run a permission-scoped artifact operation.");

    public static ExecutionPosture DecisionSession { get; } =
        new(ExecutionPostureKind.DecisionSession, "Run or continue a decision session.");

    public static ExecutionPosture ReadOnlyPrompt { get; } =
        new(ExecutionPostureKind.ReadOnlyPrompt, "Render and run a read-only assessment prompt.");
}

public sealed record WorkflowChainDefinition(
    string Identity,
    string Purpose,
    WorkflowIdentity InitialWorkflow,
    IReadOnlyList<WorkflowDefinition> Workflows);

public sealed record WorkflowDefinition(
    WorkflowIdentity Identity,
    string Purpose,
    IReadOnlyList<ProductRequirement> EntryProducts,
    GateDefinition EntryGate,
    IReadOnlyList<WorkflowStageDefinition> Stages,
    IReadOnlyList<WorkflowTransitionDefinition> Transitions,
    IReadOnlyList<ProductDefinition> ExitProducts,
    GateDefinition ExitGate,
    WorkflowIdentity? DownstreamWorkflow,
    WorkflowCompletionDefinition Completion,
    BlockerDefinition Blocker,
    RecoveryDefinition Recovery);

public sealed record WorkflowStageDefinition(
    WorkflowStageIdentity Identity,
    string Purpose,
    IReadOnlyList<ProductRequirement> RequiredProducts,
    IReadOnlyList<ProductIdentity> ProducedProducts,
    IReadOnlyList<TransitionDependency> Dependencies,
    IReadOnlyList<WorkflowTransitionIdentity> Transitions,
    IReadOnlyList<WorkflowStageIdentity> AllowedSuccessors,
    GateDefinition EntryGate,
    GateDefinition CompletionGate,
    IReadOnlyList<RuntimeOutcomeKind> TerminalOutcomes);

public sealed record WorkflowTransitionDefinition(
    WorkflowTransitionIdentity Identity,
    string Purpose,
    IReadOnlyList<ProductRequirement> RequiredInputProducts,
    GateDefinition InputGate,
    string PromptIdentity,
    ExecutionPosture ExecutionPosture,
    IReadOnlyList<ProductDefinition> ProducedProducts,
    GateDefinition OutputGate,
    IReadOnlyList<string> Validators,
    IReadOnlyList<EffectDefinition> Effects,
    IReadOnlyList<TransitionDependency> Dependencies,
    IReadOnlyList<WorkflowTransitionIdentity> EligibleSuccessors,
    RecoveryDefinition Recovery);

public sealed record TransitionDependency(
    string Identity,
    DependencyTargetKind TargetKind,
    DependencyStrength Strength,
    string Producer,
    string Consumer,
    ProductIdentity? Product,
    WorkflowTransitionIdentity? Transition,
    WorkflowStageIdentity? Stage,
    WorkflowIdentity? Workflow,
    string InvalidationRule);

public sealed record ProductDefinition(
    ProductIdentity Identity,
    WorkflowIdentity ProducerWorkflow,
    WorkflowTransitionIdentity ProducerTransition,
    IReadOnlyList<WorkflowIdentity> IntendedConsumers,
    string RepositoryOwnership,
    string Authority,
    ProductLifecycle Lifecycle,
    ProductValidationState ValidationState,
    ProductFreshness Freshness,
    IReadOnlyList<TransitionDependency> Dependencies,
    IReadOnlyList<string> StorageRepresentations);

public sealed record ProductRequirement(
    ProductIdentity Product,
    DependencyStrength Strength,
    bool RequiresFreshness,
    string RequiredAuthority,
    string Purpose);

public sealed record GateDefinition(
    GateIdentity Identity,
    string Purpose,
    IReadOnlyList<GateRequirementDefinition> Requirements,
    string Authority,
    string FailureSemantics);

public sealed record GateRequirementDefinition(
    string Identity,
    string Description,
    ProductIdentity? Product,
    DependencyStrength Strength,
    bool BlocksProgress);

public sealed record GateResult(
    GateStatus Status,
    IReadOnlyList<GateRequirementResult> Requirements,
    string Explanation,
    IReadOnlyList<string> Evidence)
{
    public bool IsSatisfied => Status == GateStatus.Satisfied;
}

public sealed record GateRequirementResult(
    string RequirementIdentity,
    GateStatus Status,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record WorkflowOutcome(
    RuntimeOutcomeKind Status,
    WorkflowIdentity Workflow,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record StageOutcome(
    RuntimeOutcomeKind Status,
    WorkflowIdentity Workflow,
    WorkflowStageIdentity Stage,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record TransitionOutcome(
    RuntimeOutcomeKind Status,
    WorkflowIdentity Workflow,
    WorkflowStageIdentity Stage,
    WorkflowTransitionIdentity Transition,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record EffectDefinition(
    EffectIdentity Identity,
    EffectCategory Category,
    string Trigger,
    IReadOnlyList<ProductIdentity> Inputs,
    IReadOnlyList<ProductIdentity> Outputs,
    int Order,
    string FailureSemantics);

public sealed record BlockerDefinition(
    string Identity,
    string Semantics,
    RuntimeOutcomeKind Outcome,
    IReadOnlyList<string> RecoveryHints);

public sealed record RecoveryDefinition(
    string Identity,
    string Semantics,
    IReadOnlyList<string> SupportedActions,
    IReadOnlyList<string> UnsupportedActions);

public sealed record WorkflowCompletionDefinition(
    string Identity,
    string Semantics,
    GateDefinition CompletionGate,
    IReadOnlyList<ProductIdentity> RequiredProducts);

public sealed record ProductRecord(
    ProductIdentity Identity,
    WorkflowIdentity ProducerWorkflow,
    WorkflowTransitionIdentity ProducerTransition,
    IReadOnlyList<WorkflowIdentity> IntendedConsumers,
    string RepositoryOwnership,
    string Authority,
    IReadOnlyList<string> StorageRepresentations,
    string CausalIdentity,
    ProductFreshness Freshness,
    ProductValidationState ValidationState,
    ProductLifecycle Lifecycle,
    IReadOnlyList<string> EvidenceLocations);
