using System.Security.Cryptography;
using System.Text;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Runtime;

public enum TransitionDurableState
{
    NotStarted,
    Started,
    PromptCompleted,
    OutputInterpreted,
    OutputValidated,
    EffectsPartiallyApplied,
    EffectsPending,
    EffectsApplied,
    Completed,
    Stalled,
    InputUnsatisfied,
    Waiting,
    Ambiguous,
    Failed,
    Cancelled,
    ProviderOutcomeUnknown,
    InputInvalidated,
    ConcurrentStateConflict,
}

public enum PromptExecutionStatus
{
    Completed,
    Failed,
    Cancelled,
}

public enum OutputInterpretationStatus
{
    Valid,
    Malformed,
    Incomplete,
    Unexpected,
    Unavailable,
}

public enum ProductValidationStatus
{
    Valid,
    Missing,
    Invalid,
    Stale,
    Ambiguous,
}

public enum EffectExecutionStatus
{
    Planned,
    Started,
    Succeeded,
    Stalled,
    PartiallyFailed,
    Failed,
    Unknown,
}

/// <summary>
/// Immutable invocation-wide and workflow-instance facts required to begin a transition attempt.
/// The runtime, not its caller, mints the transition-run and attempt identities.
/// </summary>
public abstract record TransitionExecutionContext(WorkflowInvocation RootInvocation);

public sealed record CanonicalTransitionExecutionContext : TransitionExecutionContext
{
    public CanonicalTransitionExecutionContext(
        WorkflowInvocation rootInvocation,
        WorkspaceIdentity workspace,
        RunIdentity run,
        WorkflowInstanceIdentity workflowInstance,
        PolicyIdentity resolvedPolicy,
        RuntimeProfileIdentity runtimeProfile,
        PromptPolicyProfileIdentity promptPolicyProfile)
        : base(rootInvocation)
    {
        ArgumentNullException.ThrowIfNull(rootInvocation);
        if (workspace.IsEmpty || run.IsEmpty || workflowInstance.IsEmpty || resolvedPolicy.IsEmpty || runtimeProfile.IsEmpty ||
            promptPolicyProfile.IsEmpty)
        {
            throw new ArgumentException("Canonical transition execution identities must not be empty.");
        }

        Workspace = workspace;
        Run = run;
        WorkflowInstance = workflowInstance;
        ResolvedPolicy = resolvedPolicy;
        RuntimeProfile = runtimeProfile;
        PromptPolicyProfile = promptPolicyProfile;
    }

    public WorkspaceIdentity Workspace { get; }

    public RunIdentity Run { get; }

    public WorkflowInstanceIdentity WorkflowInstance { get; }

    public PolicyIdentity ResolvedPolicy { get; }

    public RuntimeProfileIdentity RuntimeProfile { get; }

    public PromptPolicyProfileIdentity PromptPolicyProfile { get; }

    public CanonicalCausalContext BeginAttempt(
        TransitionRunIdentity transitionRun,
        AttemptIdentity attempt) =>
        new(Workspace, Run, WorkflowInstance, transitionRun, attempt);
}

/// <summary>
/// Explicit compatibility boundary for callers that do not yet possess the canonical spine.
/// A translator must replace this context before canonical runtime execution begins.
/// </summary>
public sealed record LegacyTransitionExecutionContext : TransitionExecutionContext
{
    public LegacyTransitionExecutionContext(
        WorkflowInvocation rootInvocation,
        string compatibilitySource)
        : base(rootInvocation)
    {
        ArgumentNullException.ThrowIfNull(rootInvocation);
        if (string.IsNullOrWhiteSpace(compatibilitySource))
        {
            throw new ArgumentException("Compatibility source must not be empty.", nameof(compatibilitySource));
        }

        CompatibilitySource = compatibilitySource.Trim();
    }

    public string CompatibilitySource { get; }
}

public sealed record TransitionRuntimeRequest(
    WorkflowIdentity Workflow,
    WorkflowStageIdentity Stage,
    WorkflowTransitionIdentity Transition,
    TransitionExecutionContext ExecutionContext,
    AttemptAuthorization Authorization,
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>
/// Typed authorization for exactly one attempt. It replaces caller-supplied nullable run ids.
/// </summary>
public abstract record AttemptAuthorization;

public sealed record FreshAttemptAuthorization : AttemptAuthorization
{
    public static FreshAttemptAuthorization Instance { get; } = new();

    private FreshAttemptAuthorization()
    {
    }
}

public sealed record RecoveryAttemptAuthorization(TransitionRecoveryPlan Plan) : AttemptAuthorization;

public sealed record ProductResolutionResult(
    IReadOnlyList<ProductRecord> Products,
    IReadOnlyList<ProductRequirement> Missing,
    IReadOnlyList<ProductRecord> Stale,
    IReadOnlyList<ProductRecord> Invalid,
    IReadOnlyList<ProductRecord> Ambiguous)
{
    public bool IsUsable => Missing.Count == 0 && Stale.Count == 0 && Invalid.Count == 0 && Ambiguous.Count == 0;
}

public sealed record PromptContext(
    WorkflowTransitionDefinition Transition,
    ProductResolutionResult Inputs,
    TransitionInputSnapshot InputSnapshot,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<PromptContextSection> Sections,
    IReadOnlyList<ConsumedInputFile>? ConsumedFiles = null);

public sealed record PromptContextSection(
    string Title,
    string Content,
    string SourcePath,
    IReadOnlyList<string> Evidence);

public sealed class PromptContextUnavailableException(
    string message,
    IReadOnlyList<string> evidence,
    IReadOnlyList<ConsumedInputFile>? consumedFiles = null) : Exception(message)
{
    public IReadOnlyList<string> Evidence { get; } = evidence;

    // Files that were actually read before the context proved unusable; the read receipt for a
    // failed consumption is recorded from these.
    public IReadOnlyList<ConsumedInputFile> ConsumedFiles { get; } = consumedFiles ?? [];
}

// One consumption event: the transition read its inputs (files and resolved ledger products) at
// a single moment. The store enriches and persists this as an append-only read receipt.
public sealed record ReadReceiptCapture(
    CanonicalCausalContext Causality,
    TransitionRuntimeRequest Request,
    WorkflowTransitionDefinition Definition,
    IReadOnlyList<ConsumedInputFile> ConsumedFiles,
    IReadOnlyList<ProductRecord> ConsumedProducts,
    string Validation,
    DateTimeOffset ConsumedAt);

public interface IReadReceiptStore
{
    Task AppendAsync(ReadReceiptCapture capture, CancellationToken cancellationToken);
}

// Intermediate output of canonical template + policy-profile composition. It is not dispatchable
// until Prompt Authority binds it to a causal attempt, hashes it, and persists a RenderedPromptFact.
public sealed record RenderedPrompt(
    PromptTemplateIdentity TemplateIdentity,
    PromptPolicyProfileIdentity PolicyProfileIdentity,
    string Text,
    string EvidenceLocation,
    string? TemplateSourceHash = null);

/// <summary>
/// Immutable provider-visible prompt content and the authority inputs that produced it.
/// </summary>
public sealed record RenderedPromptFact
{
    public RenderedPromptFact(
        RenderedPromptFactIdentity identity,
        CanonicalCausalContext causality,
        string renderedContent,
        string contentHash,
        PromptTemplateIdentity templateIdentity,
        string? templateSourceHash,
        PolicyIdentity policyIdentity,
        PromptPolicyProfileIdentity policyProfileIdentity,
        ConsumedInputManifestIdentity consumedInputManifestIdentity,
        IReadOnlyList<ConsumedInputFile> consumedInputs,
        DateTimeOffset renderedAt,
        string renderedEncoding = "utf-8")
    {
        ArgumentNullException.ThrowIfNull(causality);
        ArgumentNullException.ThrowIfNull(consumedInputs);
        if (identity.IsEmpty || templateIdentity.IsEmpty || policyIdentity.IsEmpty ||
            policyProfileIdentity.IsEmpty || consumedInputManifestIdentity.IsEmpty)
        {
            throw new ArgumentException("Rendered-prompt identities must not be empty.");
        }

        if (string.IsNullOrEmpty(renderedContent) || string.IsNullOrWhiteSpace(renderedEncoding))
        {
            throw new ArgumentException("Rendered prompt content must not be empty.", nameof(renderedContent));
        }

        string computedHash = ComputeContentHash(renderedContent);
        if (!string.Equals(contentHash, computedHash, StringComparison.Ordinal))
        {
            throw new ArgumentException("Rendered prompt content hash does not match its content.", nameof(contentHash));
        }

        Identity = identity;
        Causality = causality;
        RenderedContent = renderedContent;
        ContentHash = contentHash;
        TemplateIdentity = templateIdentity;
        TemplateSourceHash = templateSourceHash;
        PolicyIdentity = policyIdentity;
        PolicyProfileIdentity = policyProfileIdentity;
        ConsumedInputManifestIdentity = consumedInputManifestIdentity;
        ConsumedInputs = consumedInputs.ToArray();
        RenderedAt = renderedAt;
        RenderedEncoding = renderedEncoding;
    }

    public RenderedPromptFactIdentity Identity { get; }

    public CanonicalCausalContext Causality { get; }

    public string RenderedContent { get; }

    public string ContentHash { get; }

    public PromptTemplateIdentity TemplateIdentity { get; }

    public string? TemplateSourceHash { get; }

    public PolicyIdentity PolicyIdentity { get; }

    public PromptPolicyProfileIdentity PolicyProfileIdentity { get; }

    public ConsumedInputManifestIdentity ConsumedInputManifestIdentity { get; }

    public IReadOnlyList<ConsumedInputFile> ConsumedInputs { get; }

    public DateTimeOffset RenderedAt { get; }

    public string RenderedEncoding { get; }

    public static string ComputeContentHash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}

public interface IRenderedPromptStore
{
    Task<PersistedRenderedPromptFact> AppendAsync(
        RenderedPromptFact fact,
        CancellationToken cancellationToken);
}

public sealed record PersistedRenderedPromptFact
{
    public PersistedRenderedPromptFact(
        RenderedPromptFact fact,
        RenderedPromptPersistenceIdentity persistenceIdentity,
        long ledgerSequence,
        DateTimeOffset persistedAt)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (persistenceIdentity.IsEmpty || ledgerSequence <= 0)
        {
            throw new ArgumentException("Rendered-prompt persistence evidence must be valid.");
        }

        Fact = fact;
        PersistenceIdentity = persistenceIdentity;
        LedgerSequence = ledgerSequence;
        PersistedAt = persistedAt;
    }

    public RenderedPromptFact Fact { get; }

    public RenderedPromptPersistenceIdentity PersistenceIdentity { get; }

    public long LedgerSequence { get; }

    public DateTimeOffset PersistedAt { get; }
}

public sealed record PromptExecutionResult(
    PromptExecutionStatus Status,
    string RawOutput,
    TimeSpan Duration,
    IReadOnlyDictionary<string, string> Metadata,
    string? FailureMessage = null);

public sealed record InterpretedTransitionOutput(
    OutputInterpretationStatus Status,
    IReadOnlyList<ProductRecord> CandidateProducts,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record ProductValidationResult(
    ProductValidationStatus Status,
    IReadOnlyList<ProductRecord> Products,
    IReadOnlyList<ProductIdentity> MissingProducts,
    IReadOnlyList<ProductIdentity> InvalidProducts,
    IReadOnlyList<ProductIdentity> StaleProducts,
    IReadOnlyList<ProductIdentity> AmbiguousProducts,
    string Explanation,
    IReadOnlyList<string> Evidence)
{
    public bool IsValid => Status == ProductValidationStatus.Valid &&
        MissingProducts.Count == 0 &&
        InvalidProducts.Count == 0 &&
        StaleProducts.Count == 0 &&
        AmbiguousProducts.Count == 0;
}

public sealed record EffectExecutionRecord(
    EffectIdentity Effect,
    EffectExecutionStatus Status,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record EffectExecutionResult(
    EffectExecutionStatus Status,
    IReadOnlyList<EffectExecutionRecord> Effects,
    string Explanation,
    IReadOnlyList<string> Evidence)
{
    public bool IsSuccess => Status == EffectExecutionStatus.Succeeded;
}

public sealed record TransitionRuntimeResult(
    RuntimeOutcomeKind Outcome,
    TransitionDurableState DurableState,
    WorkflowTransitionIdentity Transition,
    GateResult? InputGate,
    GateResult? OutputGate,
    ProductValidationResult? ProductValidation,
    EffectExecutionResult? Effects,
    IReadOnlyList<WorkflowTransitionIdentity> EligibleSuccessors,
    string Explanation,
    IReadOnlyList<string> Evidence,
    TransitionRunIdentity? TransitionRun = null,
    AttemptIdentity? Attempt = null,
    bool AttemptCompleted = false,
    bool RequiredEffectsPending = false);

public sealed record TransitionInputSnapshot(
    string Hash,
    IReadOnlyList<ProductRecord> Products,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<PromptContextSection> Sections);

public sealed record TransitionRunStarted(
    CanonicalCausalContext Causality,
    DateTimeOffset StartedAt,
    TransitionRuntimeRequest Request,
    WorkflowTransitionDefinition Definition,
    TransitionInputSnapshot InputSnapshot,
    PersistedRenderedPromptFact RenderedPrompt);

public sealed record TransitionRunStateUpdate(
    CanonicalCausalContext Causality,
    DateTimeOffset RecordedAt,
    WorkflowTransitionIdentity Transition,
    TransitionDurableState State,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record TransitionRunCompleted(
    CanonicalCausalContext Causality,
    DateTimeOffset CompletedAt,
    WorkflowTransitionIdentity Transition,
    TransitionRuntimeResult Result);

public sealed record TransitionRunRecoverySnapshot(
    CanonicalCausalContext Causality,
    WorkflowTransitionIdentity Transition,
    TransitionDurableState State,
    RuntimeOutcomeKind Outcome,
    string? InputSnapshotHash,
    PromptExecutionResult? RawOutput,
    IReadOnlyList<EffectExecutionRecord> Effects,
    IReadOnlyList<TransitionBoundaryObservation> Boundaries,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record TransitionEvidenceEvent(
    CanonicalCausalContext Causality,
    DateTimeOffset RecordedAt,
    WorkflowTransitionIdentity Transition,
    TransitionDurableState State,
    string EventName,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record TransitionWarningCapture(
    CanonicalCausalContext Causality,
    DateTimeOffset RecordedAt,
    TransitionRuntimeRequest Request,
    WorkflowTransitionIdentity Transition,
    WarningCategory Category,
    string Concern,
    string Remediation,
    IReadOnlyList<string> Evidence);

public sealed record TransitionRecoveryMarkerCapture(
    CanonicalCausalContext Causality,
    DateTimeOffset RecordedAt,
    TransitionRuntimeRequest Request,
    WorkflowTransitionIdentity Transition,
    TransitionDurableState DurableState,
    RuntimeOutcomeKind Outcome,
    RecoveryDefinition Recovery,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record TransitionGateEvaluationCapture(
    CanonicalCausalContext Causality,
    DateTimeOffset EvaluatedAt,
    TransitionRuntimeRequest Request,
    WorkflowTransitionIdentity Transition,
    GateDefinition Gate,
    GateResult Result);

public sealed record TransitionEffectRecordCapture(
    CanonicalCausalContext Causality,
    DateTimeOffset RecordedAt,
    TransitionRuntimeRequest Request,
    WorkflowTransitionIdentity Transition,
    EffectIdentity Effect,
    EffectCategory Category,
    EffectExecutionStatus Status,
    string Explanation,
    IReadOnlyList<string> Evidence);

public interface ITransitionDefinitionResolver
{
    Task<WorkflowTransitionDefinition> ResolveAsync(TransitionRuntimeRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkflowTransitionIdentity>> ResolveEligibleSuccessorsAsync(
        WorkflowTransitionDefinition definition,
        IReadOnlyList<ProductRecord> validatedProducts,
        CancellationToken cancellationToken);
}

public interface ITransitionRuntime
{
    Task<TransitionRuntimeResult> RunAsync(
        TransitionRuntimeRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProductResolver
{
    Task<ProductResolutionResult> ResolveAsync(
        IReadOnlyList<ProductRequirement> requirements,
        CancellationToken cancellationToken);
}

public interface IGateEvaluator
{
    Task<GateResult> EvaluateInputGateAsync(
        GateDefinition gate,
        ProductResolutionResult inputs,
        CancellationToken cancellationToken);

    Task<GateResult> EvaluateOutputGateAsync(
        GateDefinition gate,
        ProductValidationResult validation,
        CancellationToken cancellationToken);
}

public interface IPromptContextBuilder
{
    Task<PromptContext> BuildAsync(
        TransitionRuntimeRequest request,
        WorkflowTransitionDefinition definition,
        ProductResolutionResult inputs,
        CancellationToken cancellationToken);
}

public sealed record PromptRenderRequest(
    WorkflowTransitionDefinition Definition,
    PromptContext Context,
    CanonicalCausalContext Causality,
    PolicyIdentity Policy,
    PromptPolicyProfileIdentity PolicyProfile,
    ConsumedInputManifestIdentity ConsumedInputManifest);

public interface IPromptRenderer
{
    Task<RenderedPrompt> RenderAsync(
        PromptRenderRequest request,
        CancellationToken cancellationToken);
}

public interface IPromptExecutor : IPromptRuntimeDispatcher;

public interface IOutputInterpreter
{
    Task<InterpretedTransitionOutput> InterpretAsync(
        WorkflowTransitionDefinition definition,
        PromptExecutionResult executionResult,
        CancellationToken cancellationToken);
}

public interface IProductValidator
{
    Task<ProductValidationResult> ValidateAsync(
        WorkflowTransitionDefinition definition,
        InterpretedTransitionOutput output,
        CancellationToken cancellationToken);
}

// Identity context for effect execution: effects that append history facts (for example loop
// history rotation) carry the causal spine ids of the transition that ran them.
public sealed record EffectExecutionContext(CanonicalCausalContext Causality);

public interface IEffectExecutor
{
    Task<EffectExecutionResult> ExecuteAsync(
        WorkflowTransitionDefinition definition,
        ProductValidationResult validation,
        EffectExecutionContext context,
        CancellationToken cancellationToken);
}

public interface ITransitionRunStore
{
    Task PersistStartedAsync(TransitionRunStarted started, CancellationToken cancellationToken);

    Task PersistStateAsync(TransitionRunStateUpdate update, CancellationToken cancellationToken);

    Task PersistCompletedAsync(TransitionRunCompleted completed, CancellationToken cancellationToken);

    Task<TransitionRunRecoverySnapshot?> LoadRecoveryAsync(
        TransitionRunIdentity transitionRun,
        CancellationToken cancellationToken) =>
        Task.FromResult<TransitionRunRecoverySnapshot?>(null);
}

public interface IAttemptStore
{
    Task PersistAttemptStartedAsync(AttemptRecord attempt, CancellationToken cancellationToken);

    Task PersistAttemptCompletedAsync(
        AttemptIdentity attempt,
        DateTimeOffset completedAt,
        string outcome,
        CancellationToken cancellationToken);
}

public interface ITransitionEvidenceStore
{
    Task RecordEventAsync(TransitionEvidenceEvent evidence, CancellationToken cancellationToken);

    Task RecordRawOutputAsync(
        CanonicalCausalContext causality,
        WorkflowTransitionIdentity transition,
        PromptExecutionResult executionResult,
        CancellationToken cancellationToken);

    Task RecordFailureAsync(
        CanonicalCausalContext causality,
        WorkflowTransitionIdentity transition,
        string failure,
        CancellationToken cancellationToken);
}

public interface ITransitionWarningStore
{
    Task RecordWarningAsync(
        TransitionWarningCapture warning,
        CancellationToken cancellationToken);
}

public interface ITransitionRecoveryStore
{
    Task RecordRecoveryMarkerAsync(
        TransitionRecoveryMarkerCapture marker,
        CancellationToken cancellationToken);
}

public interface ITransitionGateEvaluationStore
{
    Task RecordGateEvaluationAsync(
        TransitionGateEvaluationCapture evaluation,
        CancellationToken cancellationToken);
}

public interface ITransitionEffectStore
{
    Task RecordEffectAsync(
        TransitionEffectRecordCapture effect,
        CancellationToken cancellationToken);
}

public interface ICandidateProductStore
{
    Task RegisterAsync(
        CanonicalCausalContext causality,
        IReadOnlyList<ProductRecord> candidates,
        CancellationToken cancellationToken);
}

public enum InputFreshnessStatus
{
    Fresh,
    InputInvalidated,
    ConcurrentStateAdvanced,
}

public sealed record InputFreshnessResult(
    InputFreshnessStatus Status,
    string Explanation,
    IReadOnlyList<string> Evidence)
{
    public bool IsFresh => Status == InputFreshnessStatus.Fresh;
}

public interface IInputFreshnessValidator
{
    Task<InputFreshnessResult> ValidateAsync(
        CanonicalCausalContext causality,
        TransitionRuntimeRequest request,
        WorkflowTransitionDefinition definition,
        PromptContext frozenContext,
        CancellationToken cancellationToken);
}

/// <summary>
/// Atomic state-commit boundary after deterministic validation. Implementations promote products,
/// persist output-gate/state evidence, and enqueue required effect intents in one transaction.
/// </summary>
public sealed record TransitionCommitCapture(
    CanonicalCausalContext Causality,
    TransitionRuntimeRequest Request,
    WorkflowTransitionDefinition Definition,
    ProductValidationResult Validation,
    GateResult OutputGate,
    IReadOnlyList<WorkflowTransitionIdentity> EligibleSuccessors,
    DateTimeOffset CommittedAt);

public interface ITransitionCommitStore
{
    Task CommitAsync(TransitionCommitCapture capture, CancellationToken cancellationToken);
}
