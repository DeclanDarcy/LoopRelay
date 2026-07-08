namespace LoopRelay.Roadmap.Cli;

internal enum RepositoryWorkAdmissionOutcome
{
    Admitted,
    Denied,
    Blocked,
    ReportOnly,
    Unsupported,
}

internal enum RepositoryWorkLifecycleState
{
    Initialized,
    IntentCaptured,
    SourceCaptured,
    ProtocolAdmitted,
    InteractionObserved,
    EvidenceBound,
    CandidateCreated,
    ArtifactPromoted,
    DecisionAccepted,
    StateCurrent,
    Blocked,
    Recovered,
    CompletionCertified,
    Distilled,
    CapabilityEvaluated,
}

internal enum RepositoryWorkCapabilityEvaluationOutcome
{
    Accepted,
    Rejected,
    BlockedForMissingSemantics,
    ReportOnly,
}

internal sealed record RepositoryWorkSemanticRequest(
    string Operation,
    string IntentSummary,
    string SourcePath,
    IReadOnlyList<string> AuthorityScopes)
{
    public const string ExecutionOperation = "RepositoryWorkSemanticExecution";
    public const string ReportOperation = "RepositoryWorkSemanticReport";

    public static RepositoryWorkSemanticRequest Default => new(
        ExecutionOperation,
        "Execute the canonical semantic architecture plan as one narrow RepositoryWork vertical slice.",
        "plan.md",
        RepositoryWorkAuthorityScopes.DefaultExecutionScopes);
}

internal static class RepositoryWorkAuthorityScopes
{
    public const string RepositoryRead = "repository.read";
    public const string SemanticExecution = "repositorywork.semantic-execution";
    public const string ArtifactPromotion = "repositorywork.artifact-promotion";
    public const string DecisionAcceptance = "repositorywork.decision-acceptance";
    public const string StateEntry = "repositorywork.state-entry";
    public const string RecoveryReview = "repositorywork.recovery-review";
    public const string Certification = "repositorywork.certification";
    public const string Distillation = "repositorywork.distillation";
    public const string CapabilityEvaluation = "repositorywork.capability-evaluation";
    public const string Report = "repositorywork.report";

    public static readonly IReadOnlyList<string> DefaultExecutionScopes =
    [
        RepositoryRead,
        SemanticExecution,
        ArtifactPromotion,
        DecisionAcceptance,
        StateEntry,
        RecoveryReview,
        Certification,
        Distillation,
        CapabilityEvaluation,
        Report,
    ];
}

internal sealed record RepositoryWorkSemanticExecutionResult(
    RepositoryWorkAdmissionOutcome AdmissionOutcome,
    string SubjectId,
    string RunId,
    string ReportPath,
    IReadOnlyList<string> WrittenArtifacts)
{
    public bool Completed => AdmissionOutcome == RepositoryWorkAdmissionOutcome.Admitted;
}

internal sealed record RepositoryWorkRepositoryIdentity(
    string Kind,
    string Value,
    string Hash);

internal sealed record RepositoryWorkAuthorityScope(
    string Scope,
    string Kind,
    string Owner,
    string Purpose);

internal sealed record RepositoryWorkInvariantDeclaration(
    string InvariantId,
    string Statement);

internal sealed record RepositoryWorkSubjectIdentityDocument(
    string SchemaVersion,
    string SubjectId,
    string SubjectType,
    RepositoryWorkRepositoryIdentity RepositoryIdentity,
    string OwnerRule,
    IReadOnlyList<RepositoryWorkAuthorityScope> AuthorityScopes,
    IReadOnlyList<string> LifecycleVocabulary,
    IReadOnlyList<RepositoryWorkInvariantDeclaration> Invariants,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public const string CurrentSchemaVersion = "repository-work.subject.v1";
}

internal sealed record RepositoryWorkProtocolDefinitionRecord(
    string SchemaVersion,
    string ProtocolId,
    string ProtocolOwner,
    string SubjectClass,
    string AcceptedIntentShape,
    IReadOnlyList<string> RequiredInputs,
    IReadOnlyList<string> SourceFreshnessRequirements,
    IReadOnlyList<string> AuthorityScopes,
    IReadOnlyList<string> Invariants,
    IReadOnlyList<RepositoryWorkAdmissionOutcome> AllowedExits)
{
    public const string CurrentSchemaVersion = "repository-work.protocol.v1";
}

internal sealed record RepositoryWorkSemanticLedgerDocument(
    string SchemaVersion,
    IReadOnlyList<RepositoryWorkSemanticRunRecord> Runs)
{
    public const string CurrentSchemaVersion = "repository-work.semantic-ledger.v1";

    public static RepositoryWorkSemanticLedgerDocument Empty => new(CurrentSchemaVersion, []);
}

internal sealed record RepositoryWorkSemanticRunRecord(
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string ExecutableOutcome,
    string DurableEvidence,
    string EvaluationGate,
    string IrreversibleCommitment,
    RepositoryWorkIntentRecord Intent,
    RepositoryWorkSourceCaptureRecord? SourceCapture,
    RepositoryWorkAdmissionRecord Admission,
    RepositoryWorkInteractionRecord? Interaction,
    RepositoryWorkEvidenceRecord? Evidence,
    RepositoryWorkArtifactRecord? CandidateArtifact,
    RepositoryWorkArtifactValidationRecord? ArtifactValidation,
    RepositoryWorkDecisionRecord? Decision,
    RepositoryWorkPromotionRecord? Promotion,
    RepositoryWorkStateEntryRecord? StateEntry,
    RepositoryWorkBlockerRecord? Blocker,
    RepositoryWorkRecoveryReviewRecord? RecoveryReview,
    RepositoryWorkCertificationRecord? Certification,
    RepositoryWorkDistillationRecord? Distillation,
    RepositoryWorkCapabilityDeclarationRecord? CapabilityDeclaration,
    RepositoryWorkCapabilityEvaluationRecord? CapabilityEvaluation,
    string ReportPath);

internal sealed record RepositoryWorkIntentRecord(
    string IntentId,
    string SubjectId,
    string Operation,
    string Summary,
    string SourcePath,
    DateTimeOffset CapturedAt);

internal sealed record RepositoryWorkSourceCaptureRecord(
    string CaptureId,
    string SubjectId,
    string IntentId,
    string Origin,
    string CaptureMethod,
    string SnapshotHash,
    long SnapshotBytes,
    string FreshnessRule,
    string TrustBoundary,
    string ConsumerScope,
    string CapturedArtifactPath,
    DateTimeOffset CapturedAt);

internal sealed record RepositoryWorkCheckRecord(
    string Name,
    bool Passed,
    string Scope,
    string Reason);

internal sealed record RepositoryWorkAdmissionRecord(
    string AdmissionId,
    string ProtocolId,
    string SubjectId,
    string IntentId,
    string Operation,
    RepositoryWorkAdmissionOutcome Outcome,
    string Reason,
    IReadOnlyList<RepositoryWorkCheckRecord> AuthorityChecks,
    IReadOnlyList<RepositoryWorkCheckRecord> InvariantChecks,
    RepositoryWorkCheckRecord SourceFreshness,
    string AdmissionPath,
    DateTimeOffset CreatedAt);

internal sealed record RepositoryWorkSnapshotEntry(
    string Kind,
    string Path,
    string Hash,
    string Role);

internal sealed record RepositoryWorkObservationRecord(
    string ObservationId,
    string SubjectId,
    string Kind,
    string Summary,
    string RawObservationPath,
    DateTimeOffset CapturedAt);

internal sealed record RepositoryWorkValidationRecord(
    string Validator,
    bool Accepted,
    string Reason,
    DateTimeOffset ValidatedAt);

internal sealed record RepositoryWorkInteractionRecord(
    string InteractionId,
    string AdmissionId,
    string ProtocolId,
    string SubjectId,
    IReadOnlyList<RepositoryWorkSnapshotEntry> InputSnapshot,
    RepositoryWorkObservationRecord Observation,
    RepositoryWorkValidationRecord Validation,
    string Outcome,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

internal sealed record RepositoryWorkEvidenceRecord(
    string EvidenceId,
    string SubjectId,
    string InteractionId,
    string ObservationId,
    string ConsumerScope,
    string EvidenceKind,
    string EvidencePath,
    string EvidenceHash,
    IReadOnlyList<string> SourcePaths,
    DateTimeOffset BoundAt);

internal sealed record RepositoryWorkArtifactRecord(
    string ArtifactId,
    string SubjectId,
    string Owner,
    string Role,
    string RepresentationKey,
    int Version,
    string Lifecycle,
    string Path,
    string Hash,
    IReadOnlyList<string> Provenance,
    string MutationAuthority);

internal sealed record RepositoryWorkArtifactValidationRecord(
    string ValidationId,
    string CandidateArtifactId,
    bool Accepted,
    IReadOnlyList<RepositoryWorkCheckRecord> Checks,
    DateTimeOffset ValidatedAt);

internal sealed record RepositoryWorkDecisionRecord(
    string DecisionId,
    string SubjectId,
    string DecisionClass,
    IReadOnlyList<string> Alternatives,
    string SuggestedChoice,
    string AcceptedChoice,
    string EvidenceId,
    string Validator,
    string DecisionAuthority,
    string AuthorizedEffect,
    DateTimeOffset DecidedAt);

internal sealed record RepositoryWorkPromotionRecord(
    string PromotionId,
    string CandidateArtifactId,
    string DecisionId,
    bool Accepted,
    string AuthorityTransferred,
    string CurrentArtifactPath,
    string AuthoritativeVersionPath,
    string? SupersededVersionPath,
    int Version,
    string Reason,
    DateTimeOffset PromotedAt);

internal sealed record RepositoryWorkStateEntryRecord(
    string StateEntryId,
    string SubjectId,
    RepositoryWorkLifecycleState State,
    string Owner,
    string EntryInteractionId,
    string EntryEvidenceId,
    string LifecycleAgreement,
    IReadOnlyList<string> SupportingArtifacts,
    IReadOnlyList<string> SupportingDecisions,
    IReadOnlyList<string> AllowedNextInteractions,
    string Classification,
    DateTimeOffset EnteredAt);

internal sealed record RepositoryWorkBlockerRecord(
    string BlockerId,
    string SubjectId,
    string BlockerType,
    string OriginalIntentId,
    IReadOnlyList<string> EvidenceIds,
    string Reason,
    string RequiredRecovery,
    string BlockerPath,
    DateTimeOffset CreatedAt);

internal sealed record RepositoryWorkRecoveryReviewRecord(
    string RecoveryReviewId,
    string SubjectId,
    string RecoveryIntent,
    string BlockerId,
    bool Eligible,
    string RepairInput,
    bool ValidationAccepted,
    string Decision,
    RepositoryWorkLifecycleState TargetState,
    string RecoveryReviewPath,
    DateTimeOffset ReviewedAt);

internal sealed record RepositoryWorkCertificationRecord(
    string CertificationId,
    string SubjectId,
    string Claim,
    IReadOnlyList<string> EvidenceIds,
    string Evaluator,
    string Policy,
    bool Accepted,
    string Decision,
    RepositoryWorkLifecycleState LifecycleMovement,
    DateTimeOffset CertifiedAt);

internal sealed record RepositoryWorkDistillationRecord(
    string DistillationId,
    string SubjectId,
    string SourceEvidenceId,
    string CurrentUnderstandingPath,
    string VersionPath,
    string PlacementValidation,
    IReadOnlyList<string> Lineage,
    DateTimeOffset DistilledAt);

internal sealed record RepositoryWorkCapabilityDeclarationRecord(
    string CapabilityId,
    string CapabilityName,
    IReadOnlyList<string> OwnedSubjects,
    IReadOnlyList<string> AcceptedIntents,
    IReadOnlyList<string> Protocols,
    IReadOnlyList<string> AuthorityScopes,
    IReadOnlyList<string> SourcesAndObservations,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Artifacts,
    IReadOnlyList<string> Decisions,
    IReadOnlyList<string> RelationTypes,
    IReadOnlyList<string> States,
    IReadOnlyList<string> LifecycleMovements,
    IReadOnlyList<string> Invariants,
    IReadOnlyList<string> Reports,
    IReadOnlyList<string> Recovery,
    IReadOnlyList<string> Replay,
    IReadOnlyList<string> Retirement);

internal sealed record RepositoryWorkCapabilityEvaluationRecord(
    string EvaluationId,
    string CapabilityId,
    RepositoryWorkCapabilityEvaluationOutcome Outcome,
    IReadOnlyList<string> MissingSemantics,
    string ReportPath,
    string Decision,
    DateTimeOffset EvaluatedAt);
