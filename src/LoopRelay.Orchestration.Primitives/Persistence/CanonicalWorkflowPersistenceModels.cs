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
    IReadOnlyList<string> Evidence);

public sealed record CanonicalEffectRecord(
    long RecordId,
    string RunId,
    EffectIdentity Effect,
    EffectCategory Category,
    EffectExecutionStatus Status,
    DateTimeOffset RecordedAt,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record CanonicalBlockerRecord(
    string BlockerId,
    WorkflowIdentity Workflow,
    WorkflowStageIdentity? Stage,
    WorkflowTransitionIdentity? Transition,
    ResolutionBlocker Blocker,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

public sealed record CanonicalRecoveryMarkerRecord(
    string MarkerId,
    WorkflowIdentity Workflow,
    WorkflowStageIdentity? Stage,
    WorkflowTransitionIdentity? Transition,
    RecoveryDefinition Recovery,
    IReadOnlyList<string> Evidence,
    DateTimeOffset RecordedAt);

public sealed record CanonicalWorkflowChainRunRecord(
    string ChainRunId,
    string ChainIdentity,
    WorkflowIdentity CurrentWorkflow,
    RuntimeOutcomeKind Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record CanonicalWorkflowPersistenceSnapshot(
    IReadOnlyList<CanonicalWorkflowStateRecord> WorkflowStates,
    IReadOnlyList<CanonicalStageStateRecord> StageStates,
    IReadOnlyList<CanonicalTransitionRunRecord> TransitionRuns,
    IReadOnlyList<CanonicalTransitionEvidenceRecord> TransitionEvidence,
    IReadOnlyList<ProductRecord> Products,
    IReadOnlyList<CanonicalGateEvaluationRecord> GateEvaluations,
    IReadOnlyList<CanonicalEffectRecord> EffectRecords,
    IReadOnlyList<CanonicalBlockerRecord> Blockers,
    IReadOnlyList<CanonicalRecoveryMarkerRecord> RecoveryMarkers,
    IReadOnlyList<CanonicalWorkflowChainRunRecord> WorkflowChainRuns);
