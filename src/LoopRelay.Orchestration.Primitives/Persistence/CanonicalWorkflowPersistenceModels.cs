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
    string? Outcome);

public sealed record AgentSessionRecord(
    string SessionId,
    string? AttemptId,
    string? WorkspaceId,
    string Provider,
    string? ProviderThreadId,
    string Role,
    string? LegacySessionGuid,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record AgentTurnRecord(
    string TurnId,
    string SessionId,
    int TurnIndex,
    DateTimeOffset RecordedAt);

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
