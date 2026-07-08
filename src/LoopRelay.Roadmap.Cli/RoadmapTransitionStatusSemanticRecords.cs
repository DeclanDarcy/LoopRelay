namespace LoopRelay.Roadmap.Cli;

internal sealed record RoadmapTransitionStatusSemanticRequest(
    string TransitionKey,
    string IntentSummary,
    IReadOnlyList<string> AuthorityScopes)
{
    public const string SupportedTransitionKey = "StatusReport";

    public static RoadmapTransitionStatusSemanticRequest Default => new(
        SupportedTransitionKey,
        "Produce a report-only semantic view of the current roadmap state.",
        RoadmapTransitionStatusAuthorityScopes.DefaultReportOnlyScopes);
}

internal static class RoadmapTransitionStatusAuthorityScopes
{
    public const string ParentRepositoryWork = "repositorywork.child-transition";
    public const string RoadmapStateRead = "roadmap.state.read";
    public const string LegacyStatusObservation = "roadmap.status.legacy-observation";
    public const string Report = "roadmap-transition.report";
    public const string RoadmapStateWrite = "roadmap.state.write";
    public const string Execution = "roadmap-transition.execution";

    public static readonly IReadOnlyList<string> DefaultReportOnlyScopes =
    [
        ParentRepositoryWork,
        RoadmapStateRead,
        LegacyStatusObservation,
        Report,
    ];

    public static readonly IReadOnlyList<string> ForbiddenReportOnlyScopes =
    [
        RoadmapStateWrite,
        Execution,
        RepositoryWorkAuthorityScopes.ArtifactPromotion,
        RepositoryWorkAuthorityScopes.DecisionAcceptance,
        RepositoryWorkAuthorityScopes.StateEntry,
        RepositoryWorkAuthorityScopes.Certification,
    ];
}

internal sealed record RoadmapTransitionStatusSemanticExecutionResult(
    RepositoryWorkAdmissionOutcome AdmissionOutcome,
    string SubjectId,
    string RunId,
    string ReportPath,
    bool Completed,
    IReadOnlyList<string> WrittenArtifacts);

internal sealed record RoadmapTransitionStatusAuthorityScope(
    string Scope,
    string Kind,
    string Owner,
    string Purpose);

internal sealed record RoadmapTransitionStatusInvariantDeclaration(
    string InvariantId,
    string Statement);

internal sealed record RoadmapTransitionStatusSubjectIdentityDocument(
    string SchemaVersion,
    string SubjectId,
    string ParentSubjectId,
    string ParentSubjectType,
    string SubjectType,
    string TransitionKey,
    string Relationship,
    IReadOnlyList<RoadmapTransitionStatusAuthorityScope> AuthorityScopes,
    IReadOnlyList<string> LifecycleVocabulary,
    IReadOnlyList<RoadmapTransitionStatusInvariantDeclaration> Invariants,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public const string CurrentSchemaVersion = "repository-work.roadmap-transition.status-report.subject.v1";
}

internal sealed record RoadmapTransitionStatusProtocolDefinitionRecord(
    string SchemaVersion,
    string ProtocolId,
    string ParentSubjectId,
    string SubjectClass,
    string TransitionKey,
    string AcceptedIntentShape,
    IReadOnlyList<string> RequiredInputs,
    IReadOnlyList<string> SourceFreshnessRequirements,
    IReadOnlyList<string> AuthorityScopes,
    IReadOnlyList<string> Invariants,
    IReadOnlyList<RepositoryWorkAdmissionOutcome> AllowedExits)
{
    public const string CurrentSchemaVersion = "roadmap-transition.status-report.protocol.v1";
}

internal sealed record RoadmapTransitionStatusIntentRecord(
    string IntentId,
    string SubjectId,
    string ParentSubjectId,
    string TransitionKey,
    string Summary,
    DateTimeOffset CapturedAt);

internal sealed record RoadmapTransitionStatusCheckRecord(
    string Name,
    bool Passed,
    string Scope,
    string Reason);

internal sealed record RoadmapTransitionStatusSourceSnapshotRecord(
    string CaptureId,
    string SubjectId,
    string IntentId,
    string StateSourcePath,
    string SourceCondition,
    string SnapshotMarker,
    string? SnapshotHash,
    long SnapshotBytes,
    bool Parsed,
    string? ParseError,
    RoadmapStatusBehaviorFields ExpectedBehavior,
    string FreshnessRule,
    string TrustBoundary,
    string CapturedArtifactPath,
    DateTimeOffset CapturedAt);

internal sealed record RoadmapTransitionStatusAdmissionRecord(
    string AdmissionId,
    string ProtocolId,
    string SubjectId,
    string IntentId,
    string TransitionKey,
    RepositoryWorkAdmissionOutcome Outcome,
    string Reason,
    IReadOnlyList<RoadmapTransitionStatusCheckRecord> AuthorityChecks,
    IReadOnlyList<RoadmapTransitionStatusCheckRecord> InvariantChecks,
    RoadmapTransitionStatusCheckRecord SourceFreshness,
    string AdmissionPath,
    DateTimeOffset CreatedAt);

internal sealed record RoadmapTransitionStatusObservationRecord(
    string ObservationId,
    string SubjectId,
    string AdmissionId,
    string Kind,
    string RawObservationPath,
    string OutputHash,
    RoadmapOutcome Outcome,
    RoadmapStatusBehaviorFields Behavior,
    DateTimeOffset CapturedAt);

internal sealed record RoadmapTransitionStatusEvidenceRecord(
    string EvidenceId,
    string SubjectId,
    string ObservationId,
    string SourceCaptureId,
    string ConsumerScope,
    string EvidencePath,
    string EvidenceHash,
    bool ValidationAccepted,
    string ValidationReason,
    DateTimeOffset BoundAt);

internal sealed record RoadmapTransitionStatusDecisionRecord(
    string DecisionId,
    string SubjectId,
    string EvidenceId,
    string DecisionClass,
    string AcceptedChoice,
    string AuthorizedEffect,
    string PreStateMarker,
    string PostStateMarker,
    bool StateMutationDetected,
    string DecisionPath,
    DateTimeOffset DecidedAt);

internal sealed record RoadmapTransitionStatusEquivalenceFieldComparison(
    string Field,
    string LegacyValue,
    string SemanticValue,
    bool Matched);

internal sealed record RoadmapTransitionStatusEquivalenceRecord(
    string EquivalenceId,
    string SubjectId,
    string LegacyObservationPath,
    string SemanticObservationPath,
    bool Accepted,
    IReadOnlyList<RoadmapTransitionStatusEquivalenceFieldComparison> ComparedFields,
    IReadOnlyList<string> Divergences,
    string EquivalencePath,
    DateTimeOffset ComparedAt);

internal sealed record RoadmapTransitionStatusSemanticLedgerDocument(
    string SchemaVersion,
    IReadOnlyList<RoadmapTransitionStatusSemanticRunRecord> Runs)
{
    public const string CurrentSchemaVersion = "roadmap-transition.status-report.ledger.v1";

    public static RoadmapTransitionStatusSemanticLedgerDocument Empty => new(CurrentSchemaVersion, []);
}

internal sealed record RoadmapTransitionStatusSemanticRunRecord(
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string ExecutableOutcome,
    string DurableEvidence,
    string EvaluationGate,
    string IrreversibleCommitment,
    RoadmapTransitionStatusIntentRecord Intent,
    RoadmapTransitionStatusSourceSnapshotRecord SourceSnapshot,
    RoadmapTransitionStatusAdmissionRecord Admission,
    RoadmapTransitionStatusObservationRecord? Observation,
    RoadmapTransitionStatusEvidenceRecord? Evidence,
    RoadmapTransitionStatusDecisionRecord? Decision,
    RoadmapTransitionStatusEquivalenceRecord? Equivalence,
    string ReportPath);
