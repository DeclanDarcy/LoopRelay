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
    EffectsApplied,
    Completed,
    Stalled,
    InputUnsatisfied,
    Waiting,
    Ambiguous,
    Failed,
    Cancelled,
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
    Succeeded,
    Stalled,
    PartiallyFailed,
    Failed,
}

public sealed record TransitionRuntimeRequest(
    WorkflowIdentity Workflow,
    WorkflowStageIdentity Stage,
    WorkflowTransitionIdentity Transition,
    IReadOnlyDictionary<string, string>? Metadata = null,
    RunIdentity? Run = null,
    WorkflowInstanceIdentity? WorkflowInstance = null);

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
    string TransitionRunId,
    string? AttemptId,
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

// TemplateSourceHash is the build-time SHA-256 of the raw prompt template the text was rendered
// from (null only for deterministic local transitions that render no template). With policy text
// template-owned, this hash IS the policy-complete prompt version.
public sealed record RenderedPrompt(
    string PromptIdentity,
    string Text,
    string EvidenceLocation,
    string? TemplateSourceHash = null);

// One rendered agent-bound prompt: the exact text produced from a hashed template and its
// declared inputs, appended as an append-only fact. Together with the attempt's policy identity
// and the run's input snapshot this makes an agent invocation reproducible from
// (template source hash, policy identity, consumed input paths and hashes).
public sealed record RenderedPromptCapture(
    string TransitionRunId,
    string? AttemptId,
    string PromptIdentity,
    string? TemplateSourceHash,
    string RenderedText,
    IReadOnlyList<ConsumedInputFile> ConsumedInputs,
    string? PolicyId,
    DateTimeOffset RenderedAt,
    string? SessionId = null,
    string? TurnId = null);

public interface IRenderedPromptStore
{
    Task AppendAsync(RenderedPromptCapture capture, CancellationToken cancellationToken);
}

public sealed record PromptExecutionResult(
    PromptExecutionStatus Status,
    string RawOutput,
    TimeSpan Duration,
    IReadOnlyDictionary<string, string> Metadata,
    string? FailureMessage = null);

// ConsumedFiles carries the transition's consumed-input manifest (the same files the read
// receipt records) to the executor, so a rendered-prompt fact minted at the SEND site can embed
// the inputs that fed the render.
public sealed record PromptExecutionContext(
    string? RunId,
    string? WorkflowInstanceId,
    string? TransitionRunId,
    string? AttemptId,
    IReadOnlyList<ConsumedInputFile>? ConsumedFiles = null)
{
    public static PromptExecutionContext Empty { get; } = new(null, null, null, null);
}

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
    IReadOnlyList<string> Evidence);

public sealed record TransitionInputSnapshot(
    string Hash,
    IReadOnlyList<ProductRecord> Products,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<PromptContextSection> Sections);

public sealed record TransitionRunStarted(
    string RunId,
    DateTimeOffset StartedAt,
    TransitionRuntimeRequest Request,
    WorkflowTransitionDefinition Definition,
    TransitionInputSnapshot InputSnapshot,
    RenderedPrompt RenderedPrompt,
    string? WorkspaceRunId = null,
    string? WorkflowInstanceId = null,
    string? AttemptId = null);

public sealed record TransitionRunStateUpdate(
    string RunId,
    DateTimeOffset RecordedAt,
    WorkflowTransitionIdentity Transition,
    TransitionDurableState State,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record TransitionRunCompleted(
    string RunId,
    DateTimeOffset CompletedAt,
    WorkflowTransitionIdentity Transition,
    TransitionRuntimeResult Result);

public sealed record TransitionEvidenceEvent(
    string RunId,
    DateTimeOffset RecordedAt,
    WorkflowTransitionIdentity Transition,
    TransitionDurableState State,
    string EventName,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record TransitionWarningCapture(
    string RunId,
    DateTimeOffset RecordedAt,
    TransitionRuntimeRequest Request,
    WorkflowTransitionIdentity Transition,
    WarningCategory Category,
    string Concern,
    string Remediation,
    IReadOnlyList<string> Evidence);

public sealed record TransitionRecoveryMarkerCapture(
    string RunId,
    DateTimeOffset RecordedAt,
    TransitionRuntimeRequest Request,
    WorkflowTransitionIdentity Transition,
    TransitionDurableState DurableState,
    RuntimeOutcomeKind Outcome,
    RecoveryDefinition Recovery,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record TransitionGateEvaluationCapture(
    string RunId,
    DateTimeOffset EvaluatedAt,
    TransitionRuntimeRequest Request,
    WorkflowTransitionIdentity Transition,
    GateDefinition Gate,
    GateResult Result);

public sealed record TransitionEffectRecordCapture(
    string RunId,
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

public interface IPromptRenderer
{
    Task<RenderedPrompt> RenderAsync(
        WorkflowTransitionDefinition definition,
        PromptContext context,
        CancellationToken cancellationToken);
}

public interface IPromptExecutor
{
    Task<PromptExecutionResult> ExecuteAsync(
        WorkflowTransitionDefinition definition,
        RenderedPrompt prompt,
        PromptExecutionContext context,
        CancellationToken cancellationToken);
}

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
public sealed record EffectExecutionContext(
    string? TransitionRunId,
    string? AttemptId,
    string? RunId,
    string? WorkflowInstanceId)
{
    public static EffectExecutionContext Empty { get; } = new(null, null, null, null);
}

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
}

public interface IAttemptStore
{
    Task PersistAttemptStartedAsync(AttemptRecord attempt, CancellationToken cancellationToken);

    Task PersistAttemptCompletedAsync(
        string attemptId,
        DateTimeOffset completedAt,
        string outcome,
        CancellationToken cancellationToken);
}

public interface ITransitionEvidenceStore
{
    Task RecordEventAsync(TransitionEvidenceEvent evidence, CancellationToken cancellationToken);

    Task RecordRawOutputAsync(
        string runId,
        WorkflowTransitionIdentity transition,
        PromptExecutionResult executionResult,
        CancellationToken cancellationToken);

    Task RecordFailureAsync(
        string runId,
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
