using System.Text.Json.Serialization;

namespace LoopRelay.Certification;

[Flags]
public enum CaseAuthority
{
    None = 0,
    WorkflowArtifacts = 1 << 0,
    Persistence = 1 << 1,
    Git = 1 << 2,
    Interruption = 1 << 3,
    Provider = 1 << 4,
    Configuration = 1 << 5,
    Oracle = 1 << 6,
}

public enum CertificationClassification
{
    Passed,
    ProductRegression,
    ProviderRegression,
    EnvironmentFailure,
    FixtureDrift,
    OracleDrift,
    Blocked,
    UnsupportedCapability,
}

public enum EvidenceLevel
{
    Uncovered = 0,
    DeterministicComponent = 1,
    Replay = 2,
    LiveTransition = 3,
    LiveChainRecovery = 4,
}

public sealed record FixtureFile(string Path, string Content);

public sealed record FixtureRepository(
    string Identity,
    string Version,
    IReadOnlyList<FixtureFile> Files)
{
    public static FixtureRepository MinimalText { get; } = new(
        "minimal-text",
        "1",
        [new FixtureFile("README.md", "# Loop Relay certification fixture\n")]);
}

public sealed record ScenarioOverlay(
    string Identity,
    string Version,
    CaseAuthority Authorities,
    int Precedence = 0,
    IReadOnlyList<string>? Requires = null,
    IReadOnlyList<string>? IncompatibleWith = null);

public sealed record FixtureScenario(
    string Identity,
    string Version,
    IReadOnlyList<ScenarioOverlay> Overlays)
{
    public static FixtureScenario StatusCanary { get; } = new(
        "status-canary",
        "1",
        [
            new ScenarioOverlay("workflow-null-state", "1", CaseAuthority.WorkflowArtifacts),
            new ScenarioOverlay("persistence-missing", "1", CaseAuthority.Persistence),
            new ScenarioOverlay("no-git", "1", CaseAuthority.Git),
            new ScenarioOverlay("provider-not-exercised", "1", CaseAuthority.Provider),
            new ScenarioOverlay("status-exact-oracle", "1", CaseAuthority.Oracle),
        ]);
}

public sealed record ComposedCaseIdentity(
    string RepositoryIdentity,
    string RepositoryVersion,
    string ScenarioIdentity,
    string ScenarioVersion,
    string CompositionDigest);

public sealed record BehaviorIdentity(
    string FixtureDigest,
    string ScenarioDigest,
    string OracleVersion,
    string NormalizerVersion,
    string WorkflowDigest,
    string PromptDigest,
    string DatabaseSchemaDigest,
    string CliBuildDigest,
    string SettingsDigest,
    string Platform,
    string GitIdentity,
    string CodexBinaryIdentity,
    string CodexSchemaIdentity,
    string Model,
    string Effort,
    EvidenceLevel EvidenceLevel);

public sealed record FileObservation(string Path, long Length, string Sha256);

public sealed record OracleResult(
    string Oracle,
    bool Passed,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed record CoverageObligation(
    string Dimension,
    string Identity,
    EvidenceLevel Level,
    IReadOnlyList<string> Evidence);

public sealed record CoverageLedger(
    string Version,
    string ProductionDigest,
    IReadOnlyList<CoverageObligation> Obligations)
{
    [JsonIgnore]
    public IReadOnlyList<CoverageObligation> Uncovered =>
        Obligations.Where(item => item.Level == EvidenceLevel.Uncovered).ToArray();
}

public sealed record CertificationRunResult(
    string SchemaVersion,
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    ComposedCaseIdentity Case,
    BehaviorIdentity Behavior,
    CertificationClassification Classification,
    int ExitCode,
    string NormalizedStandardOutput,
    string NormalizedStandardError,
    string BaseHash,
    string PostRunHash,
    IReadOnlyList<FileObservation> Mutations,
    IReadOnlyList<OracleResult> Oracles,
    IReadOnlyList<string> PrivacyFindings,
    CoverageLedger Coverage,
    IReadOnlyList<string> EvidenceInventory);

public sealed record CanaryCertificationResult(
    string SchemaVersion,
    bool Reproducible,
    CertificationClassification Classification,
    IReadOnlyList<CertificationRunResult> Runs);

public sealed record CertificationOptions(
    string WorkspaceRoot,
    string CliPath,
    string CaseAuthorityRoot,
    bool RetainCase = false,
    int Repetitions = 2);

public sealed record PublicCliCaseResult(
    string Identity,
    IReadOnlyList<string> Arguments,
    int ExpectedExitCode,
    int ActualExitCode,
    bool MutationExpected,
    bool MutationObserved,
    bool ProviderStateObserved,
    bool Passed,
    string NormalizedStandardOutput,
    string NormalizedStandardError,
    IReadOnlyList<string> Diagnostics);

public sealed record MilestoneTwoCertificationResult(
    string SchemaVersion,
    CertificationClassification Classification,
    IReadOnlyList<PublicCliCaseResult> Cases);

public sealed record LiveProviderCheck(
    string Identity,
    bool Passed,
    string Classification,
    IReadOnlyList<string> Evidence);

public sealed record MilestoneThreeCertificationResult(
    string SchemaVersion,
    CertificationClassification Classification,
    string CodexVersion,
    string SchemaDigest,
    IReadOnlyList<LiveProviderCheck> Checks,
    IReadOnlyList<string> PrivacyFindings);

public sealed record RecoveryBoundaryCaseResult(
    string Identity,
    string Boundary,
    string InitialState,
    string RecoveryDisposition,
    string RestartState,
    int ProviderCalls,
    int EffectCalls,
    bool DuplicateProviderTurn,
    bool DuplicateEffect,
    bool PublicStatusExposedRecovery,
    bool PublicUnblockFailedClosed,
    bool Passed,
    IReadOnlyList<string> Evidence);

public sealed record MilestoneFourCertificationResult(
    string SchemaVersion,
    CertificationClassification Classification,
    string CodexVersion,
    string SchemaDigest,
    IReadOnlyList<RecoveryBoundaryCaseResult> Cases,
    IReadOnlyList<string> PrivacyFindings);

public sealed record PlanTransitionCaseResult(
    string Transition,
    int ExitCode,
    IReadOnlyList<string> ChangedAgentArtifacts,
    bool MutationScopeValid,
    bool Completed,
    IReadOnlyList<string> Diagnostics);

public sealed record PlanProducerCaseResult(
    string ProducerWorkflow,
    IReadOnlyList<PlanTransitionCaseResult> Transitions,
    bool SameAuthoringThread,
    bool RestartContinuityObserved,
    bool ExactExecuteEntryProducts,
    bool BoundedBeforeExecute,
    bool ScopedRollbackVerified,
    bool ProviderProcessesCleanedUp,
    bool Passed,
    IReadOnlyList<string> Evidence);

public sealed record MilestoneFiveCertificationResult(
    string SchemaVersion,
    CertificationClassification Classification,
    string CodexVersion,
    string SchemaDigest,
    IReadOnlyList<PlanProducerCaseResult> ProducerCases,
    IReadOnlyList<string> PrivacyFindings);

public sealed record ExecuteTransitionCaseResult(
    string Transition,
    int ExitCode,
    IReadOnlyList<string> ChangedRepositoryPaths,
    bool Completed,
    IReadOnlyList<string> Diagnostics);

public sealed record MilestoneSixCertificationResult(
    string SchemaVersion,
    CertificationClassification Classification,
    string CodexVersion,
    string SchemaDigest,
    IReadOnlyList<ExecuteTransitionCaseResult> Transitions,
    bool IndependentAcceptancePassed,
    bool VerifierUnchanged,
    bool SameImplementationThread,
    bool RestartContinuityObserved,
    bool DurableSliceFactsObserved,
    bool DecisionContinuityObserved,
    bool StoppedBeforePublication,
    bool ProviderProcessesCleanedUp,
    IReadOnlyList<string> PrivacyFindings,
    IReadOnlyList<string> Evidence);

public sealed record GitPublicationCaseResult(
    string Identity,
    string AgentsTopology,
    int ExitCode,
    bool ParentHeadExpected,
    bool AgentsHeadExpected,
    bool ParentRemoteExpected,
    bool AgentsRemoteExpected,
    bool GitlinkExpected,
    bool OutsideAuthorityUnchanged,
    bool Passed,
    IReadOnlyList<string> Evidence);

public sealed record MilestoneSevenCertificationResult(
    string SchemaVersion,
    CertificationClassification Classification,
    IReadOnlyList<GitPublicationCaseResult> Cases,
    IReadOnlyList<string> PrivacyFindings);

public sealed record PersistenceLifecycleCaseResult(
    string Identity,
    bool PublicPath,
    bool NonMutating,
    bool Passed,
    IReadOnlyList<string> Evidence);

public sealed record MilestoneEightCertificationResult(
    string SchemaVersion,
    CertificationClassification Classification,
    IReadOnlyList<string> SchemaTables,
    IReadOnlyDictionary<string, IReadOnlyList<string>> SchemaColumns,
    IReadOnlyList<PersistenceLifecycleCaseResult> Cases,
    IReadOnlyList<string> PrivacyFindings);

public sealed record RoadmapLiveCertificationResult(
    string SchemaVersion,
    CertificationClassification Classification,
    string Workflow,
    string CodexVersion,
    string SchemaDigest,
    IReadOnlyList<ExecuteTransitionCaseResult> Transitions,
    bool UniversalPlanEntryProducts,
    bool StructuralArtifactsValid,
    bool BoundedBeforePlan,
    bool ProducerIdentityPreserved,
    bool ProviderProcessesCleanedUp,
    IReadOnlyList<string> PrivacyFindings,
    IReadOnlyList<string> Evidence);

public sealed record MilestoneElevenCertificationResult(
    string SchemaVersion,
    CertificationClassification Classification,
    string CodexVersion,
    string SchemaDigest,
    IReadOnlyList<ExecuteTransitionCaseResult> Transitions,
    bool RestartRouteRestored,
    bool ArchiveComplete,
    bool RoadmapContextUpdated,
    bool CanonicalClosureObserved,
    bool ContinuityRetired,
    bool IdempotentRerunNoModelWork,
    bool IndependentAcceptancePassed,
    bool ProviderProcessesCleanedUp,
    IReadOnlyList<string> PrivacyFindings,
    IReadOnlyList<string> Evidence);

public sealed record FailureCoverageCaseResult(
    string Identity,
    string Domain,
    string ExpectedDisposition,
    EvidenceLevel EvidenceLevel,
    bool Supported,
    bool ReviewedExclusion,
    string? Owner,
    string? RecertificationCondition,
    bool Passed,
    IReadOnlyList<string> Evidence);

public sealed record TransitionRecoveryCoverageResult(
    string Workflow,
    string Transition,
    string Posture,
    IReadOnlyList<string> EffectCategories,
    bool SafeRetryCovered,
    bool UncertainProviderSideEffectCovered,
    bool PostValidationRecoveryCovered,
    bool OrderedEffectRecoveryCovered,
    bool DuplicateProviderTurnPrevented,
    bool DuplicateOrderedEffectPrevented,
    bool Passed,
    IReadOnlyList<string> Evidence);

public sealed record OracleControlCaseResult(
    string OracleClass,
    bool PositiveControlAccepted,
    bool NegativeControlRejected,
    bool Passed,
    IReadOnlyList<string> Evidence);

public sealed record CertificationGovernanceResult(
    int MinimumRepeatedRuns,
    double FlakeThreshold,
    string RerunRule,
    bool QuarantinesRequireOwner,
    bool QuarantinesRequireExpiryOrRecertification,
    IReadOnlyDictionary<string, string> EvidenceRetention,
    bool Passed);

public sealed record MilestoneTwelveCertificationResult(
    string SchemaVersion,
    CertificationClassification Classification,
    IReadOnlyList<FailureCoverageCaseResult> FailureCases,
    IReadOnlyList<TransitionRecoveryCoverageResult> TransitionClasses,
    IReadOnlyList<OracleControlCaseResult> OracleControls,
    CertificationGovernanceResult Governance,
    bool EveryFailureClassCovered,
    bool EveryPromptEffectClassCovered,
    bool NoDuplicateSemanticProgress,
    bool UnsupportedCapabilitiesReleaseVisible,
    IReadOnlyList<string> PrivacyFindings,
    IReadOnlyList<string> Evidence);

public sealed record FullChainTransitionResult(
    int Sequence,
    string ExpectedWorkflow,
    string ExpectedTransition,
    string? ActualWorkflow,
    string? ActualTransition,
    int ExitCode,
    long ElapsedMilliseconds,
    bool RestartedPublicProcess,
    bool Passed,
    IReadOnlyList<string> Evidence);

public sealed record FullChainCertificationResult(
    string SchemaVersion,
    CertificationClassification Classification,
    string Chain,
    string CodexVersion,
    string SchemaDigest,
    IReadOnlyList<FullChainTransitionResult> Transitions,
    bool DefaultSelectionPassed,
    bool ForcedSelectionTargetedEvidencePassed,
    bool WorkflowBoundariesPassed,
    bool ProducerConvergencePassed,
    bool RepositoryAcceptancePassed,
    bool GitPublicationPassed,
    bool ArchiveClosurePassed,
    bool TraceabilityPassed,
    bool IdempotentRerunNoModelOrMutation,
    bool ProviderProcessesCleanedUp,
    long TotalElapsedMilliseconds,
    long ProviderEvidenceBytes,
    string BudgetDecision,
    IReadOnlyList<string> PrivacyFindings,
    IReadOnlyList<string> Evidence);

public sealed record PlatformCertificationResult(
    string SchemaVersion,
    CertificationClassification Classification,
    string Platform,
    string Architecture,
    bool CaseSensitiveFilesystem,
    bool UnixExecutableBitObserved,
    bool SeparatorNormalizationPassed,
    bool LineEndingNormalizationPassed,
    bool Utf8RoundTripPassed,
    bool GitBehaviorPassed,
    bool PathLengthProbePassed,
    string NormalizedContractDigest,
    IReadOnlyList<string> PrivacyFindings,
    IReadOnlyList<string> Evidence);

public sealed record CertificationTierResult(
    string Identity,
    string Cadence,
    EvidenceLevel RequiredLevel,
    IReadOnlyList<string> Dimensions,
    bool ReleaseBlocking);

public sealed record ReleaseDimensionResult(
    string Dimension,
    EvidenceLevel RequiredLevel,
    EvidenceLevel ActualLevel,
    string EvidenceFile,
    bool Current,
    bool Passed,
    IReadOnlyList<string> Evidence);

public sealed record ContinuousCertificationResult(
    string SchemaVersion,
    CertificationClassification Classification,
    string ProductionSurfaceDigest,
    IReadOnlyList<CertificationTierResult> Tiers,
    IReadOnlyList<ReleaseDimensionResult> Dimensions,
    IReadOnlyList<PlatformCertificationResult> Platforms,
    bool CrossPlatformContractAgreement,
    bool ClassificationRoutingDistinct,
    bool DriftInvalidationEnabled,
    bool EvidenceRetirementReturnsToUncovered,
    bool NoCriticalDimensionAtZero,
    bool BudgetsPassed,
    IReadOnlyList<FailureCoverageCaseResult> FutureTopologyObligations,
    IReadOnlyList<string> PrivacyFindings,
    IReadOnlyList<string> Evidence);
