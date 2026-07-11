using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Persistence;

public sealed record CanonicalWorkflowStateRecord(
    WorkflowIdentity Workflow,
    WorkflowResolutionState State,
    WorkflowStageIdentity? CurrentStage,
    RuntimeOutcomeKind? Outcome,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<string> Evidence);

public sealed record CanonicalStageStateRecord(
    WorkflowIdentity Workflow,
    WorkflowStageIdentity Stage,
    WorkflowResolutionState State,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<string> Evidence);

public sealed record CanonicalTransitionRunRecord(
    string RunId,
    WorkflowIdentity Workflow,
    WorkflowStageIdentity Stage,
    WorkflowTransitionIdentity Transition,
    TransitionDurableState State,
    RuntimeOutcomeKind Outcome,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? InputSnapshotHash,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record CanonicalTransitionEvidenceRecord(
    long EvidenceId,
    string RunId,
    WorkflowTransitionIdentity Transition,
    string EventName,
    DateTimeOffset RecordedAt,
    TransitionDurableState State,
    string Explanation,
    IReadOnlyList<string> Evidence,
    string DocumentJson);

public sealed record CanonicalGateEvaluationRecord(
    long EvaluationId,
    WorkflowIdentity Workflow,
    WorkflowStageIdentity? Stage,
    WorkflowTransitionIdentity? Transition,
    GateIdentity Gate,
    GateStatus Status,
    DateTimeOffset EvaluatedAt,
    IReadOnlyList<GateRequirementResult> Requirements,
    string Explanation,
    IReadOnlyList<string> Evidence,
    string? TransitionRunId = null);

public sealed record CanonicalEffectRecord(
    long RecordId,
    string RunId,
    EffectIdentity Effect,
    EffectCategory Category,
    EffectExecutionStatus Status,
    DateTimeOffset RecordedAt,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record CanonicalWarningRecord(
    string WarningId,
    WorkflowIdentity Workflow,
    WorkflowStageIdentity? Stage,
    WorkflowTransitionIdentity? Transition,
    WarningCategory Category,
    string Concern,
    string Authority,
    string Remediation,
    IReadOnlyList<string> Evidence,
    DateTimeOffset CreatedAt,
    string? TransitionRunId = null);

public sealed record CanonicalRecoveryMarkerRecord(
    string MarkerId,
    WorkflowIdentity Workflow,
    WorkflowStageIdentity? Stage,
    WorkflowTransitionIdentity? Transition,
    RecoveryDefinition Recovery,
    IReadOnlyList<string> Evidence,
    DateTimeOffset RecordedAt);

// Decision carries the specific boundary outcome: "Advanced" or "StoppedAtBoundary".
public sealed record CanonicalChainBoundaryEventRecord(
    string BoundaryId,
    string? RunId,
    string ChainIdentity,
    WorkflowIdentity SourceWorkflow,
    WorkflowIdentity? TargetWorkflow,
    GateStatus ExitGateStatus,
    GateStatus? EntryGateStatus,
    GateStatus? TransferGateStatus,
    string Decision,
    string Explanation,
    IReadOnlyList<string> Evidence,
    string BoundaryJson,
    DateTimeOffset RecordedAt);

public sealed record RunRecord(
    string RunId,
    string WorkspaceId,
    string ChainIdentity,
    string InvocationMode,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? StopReason,
    string Explanation);

public sealed record WorkflowInstanceRecord(
    string WorkflowInstanceId,
    string RunId,
    WorkflowIdentity Workflow,
    string CatalogVersion,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? Outcome);

public sealed record AttemptRecord(
    string AttemptId,
    string TransitionRunId,
    string WorkflowInstanceId,
    string RunId,
    int AttemptIndex,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? Outcome,
    string? PolicyId = null);

// The append-only fact behind an attempt's policy_id: the full resolved policy (the canonical
// JSON the identity hash covers) plus per-field provenance. One row per invocation that starts
// a canonical run; attempts reference PolicyId.
public sealed record CanonicalPolicyResolutionRecord(
    string ResolutionId,
    string PolicyId,
    string SchemaVersion,
    string ResolvedJson,
    string ProvenanceJson,
    string SourceDescription,
    DateTimeOffset RecordedAt);

// The append-only fact behind a rendered prompt: the exact agent-bound text, the build-time
// source hash of the template it was rendered from (policy text is template-owned, so this hash
// is the policy-complete prompt version), the consumed input files (path + content hash), and
// the policy identity in effect. (TemplateSourceHash, PolicyId, ConsumedInputs, the run's input
// snapshot) reproduce the invocation; RenderedSha256 verifies the reproduction.
public sealed record CanonicalRenderedPromptRecord(
    string RenderedPromptId,
    string TransitionRunId,
    string? AttemptId,
    string PromptIdentity,
    string? TemplateSourceHash,
    string RenderedSha256,
    string RenderedText,
    IReadOnlyList<CanonicalReadReceiptFile> ConsumedInputs,
    string? PolicyId,
    DateTimeOffset RenderedAt,
    string? SessionId = null,
    string? TurnId = null);

// Effort and Sandbox record the session spec's declared profiles (identifier when one is set,
// otherwise the level name) so every session's evidence carries the effort it ran with.
// Provider comes from the runtime's capability declaration (M7), recorded before the first turn
// so every session's effective specification is evidence ahead of launch.
public sealed record AgentSessionRecord(
    string SessionId,
    string? AttemptId,
    string? WorkspaceId,
    string Provider,
    string? ProviderThreadId,
    string Role,
    string? LegacySessionGuid,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? Effort = null,
    string? Sandbox = null);

// The v9 (M7) trailing fields carry the turn's normalized evidence: terminal state, the sha256
// of the exact transport text sent (joins the rendered-prompt fact), token usage, and the typed
// diagnosis — provider diagnostic strings are retained verbatim as evidence, never as a domain
// classifier. Pre-v9 turns read back with nulls.
public sealed record AgentTurnRecord(
    string TurnId,
    string SessionId,
    int TurnIndex,
    DateTimeOffset RecordedAt,
    string? State = null,
    string? PromptSha256 = null,
    long? PromptTokens = null,
    long? OutputTokens = null,
    long? CachedInputTokens = null,
    string? DiagnosticsKind = null,
    string? Diagnostics = null);

// One runtime prerequisite inspection (M7): the doctor's typed diagnostics serialized as
// evidence at run start, before any transition executes. RunId is null when the inspection ran
// outside a recorded run.
public sealed record CanonicalRuntimePrerequisiteRecord(
    string PrerequisiteCheckId,
    string? RunId,
    DateTimeOffset CheckedAt,
    string DiagnosticsJson);

public sealed record CanonicalReadReceiptFile(
    string Path,
    string Sha256);

public sealed record CanonicalReadReceiptProduct(
    string Identity,
    string CausalIdentity,
    string ValidationState);

public sealed record CanonicalReadReceiptRecord(
    string ReceiptId,
    string RunId,
    string WorkflowIdentity,
    string TransitionIdentity,
    string? AttemptId,
    string? CommitHash,
    IReadOnlyList<string> InputSurfaces,
    IReadOnlyDictionary<string, string?>? SurfaceTreeHashes,
    IReadOnlyList<CanonicalReadReceiptFile> Files,
    IReadOnlyList<CanonicalReadReceiptProduct> Products,
    string Validation,
    DateTimeOffset ConsumedAt,
    string? TransitionRunId = null);

public sealed record CanonicalWorkflowPersistenceSnapshot(
    IReadOnlyList<CanonicalWorkflowStateRecord> WorkflowStates,
    IReadOnlyList<CanonicalStageStateRecord> StageStates,
    IReadOnlyList<CanonicalTransitionRunRecord> TransitionRuns,
    IReadOnlyList<CanonicalTransitionEvidenceRecord> TransitionEvidence,
    IReadOnlyList<ProductRecord> Products,
    IReadOnlyList<CanonicalGateEvaluationRecord> GateEvaluations,
    IReadOnlyList<CanonicalEffectRecord> EffectRecords,
    IReadOnlyList<CanonicalWarningRecord> Warnings,
    IReadOnlyList<CanonicalRecoveryMarkerRecord> RecoveryMarkers);
