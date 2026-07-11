using System.Text.Json;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Certification;

public sealed class MilestoneTwelveRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly FailureSpec[] MaintainedFailures =
    [
        Recover("repaired-context", "authority", "safe-retry", "tests/LoopRelay.Projections.Tests/Services/ProjectContextLoaderTests.cs"),
        Recover("corrected-malformed-output", "prompt-output", "safe-retry", "tests/LoopRelay.Roadmap.Cli.Tests/Services/ArtifactManagement/EpicArtifactPromotionTests.cs"),
        Recover("canonical-artifact-restoration", "artifact", "operator-unblock", "tests/LoopRelay.Roadmap.Cli.Tests/Services/ArtifactManagement/ArtifactLifecycleTests.cs"),
        Recover("projection-regeneration", "projection", "deterministic-regeneration", "tests/LoopRelay.Projections.Tests/Services/ProjectionServiceTests.cs"),
        Recover("scoped-rollback", "artifact", "scoped-rollback", "src/LoopRelay.Certification/MilestoneFiveRunner.cs"),
        Recover("incomplete-split-or-promotion", "artifact", "resume-or-fail-closed", "tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionCoordination/ActiveEpicPromotionCoordinatorTests.cs"),
        Recover("stranded-publication", "git", "operator-unblock", "src/LoopRelay.Certification/MilestoneSevenRunner.cs", EvidenceLevel.LiveTransition),
        Recover("missing-parent-pointer", "recovery", "operator-unblock", "tests/LoopRelay.Orchestration.Primitives.Tests/Recovery/NativeForkRecoveryMechanismTests.cs"),
        Recover("changed-implementation-without-handoff", "completion", "continue-execution", "tests/LoopRelay.Completion.Tests/Services/CompletionCertificationServiceTests.cs"),
        Recover("handoff-without-publication", "completion", "continue-execution", "tests/LoopRelay.Completion.Tests/Services/CompletionCertificationServiceTests.cs"),
        Recover("committed-decision-without-artifact", "recovery", "materialize-committed-output", "tests/LoopRelay.Cli.Tests/Services/Decisions/RecoveryEnvelopeTests.cs"),
        Recover("pointer-conflict", "persistence", "compare-and-swap-fail-closed", "tests/LoopRelay.Orchestration.Primitives.Tests/Recovery/SqliteRecoveryStoreTests.cs"),
        Recover("partial-archive-or-context-update", "archive", "resume-singular-closure", "tests/LoopRelay.Completion.Tests/Services/CompletionCertificationServiceTests.cs"),
        Recover("cancelled-output", "provider", "boundary-classification", "tests/LoopRelay.Orchestration.Primitives.Tests/Runtime/TransitionRecoveryClassifierTests.cs"),
        Recover("corrected-stall", "workflow", "explicit-rerun", "tests/LoopRelay.Roadmap.Cli.Tests/Services/Execution/RoadmapFailurePersistenceTests.cs"),
        Recover("usage-limit-after-failure", "provider", "bounded-wait-retry", "tests/LoopRelay.Cli.Tests/Services/Agents/GatedAgentRuntimeTests.cs"),
        FailClosed("unsupported-schema-or-profile", "configuration", "src/LoopRelay.Certification/MilestoneThreeRunner.cs"),
        FailClosed("untrusted-corrupt-authority", "persistence", "src/LoopRelay.Certification/MilestoneEightRunner.cs", EvidenceLevel.LiveTransition),
        FailClosed("ambiguous-provider-side-effect", "provider", "src/LoopRelay.Certification/MilestoneFourRunner.cs", EvidenceLevel.LiveChainRecovery),
        FailClosed("multiple-fork-children", "recovery", "tests/LoopRelay.Orchestration.Primitives.Tests/Recovery/NativeForkRecoveryMechanismTests.cs"),
        FailClosed("causal-mismatch", "recovery", "tests/LoopRelay.Orchestration.Primitives.Tests/Recovery/RecoveryPlannerTests.cs"),
        FailClosed("recovery-marker-mismatch", "persistence", "tests/LoopRelay.Orchestration.Primitives.Tests/Persistence/CanonicalTransitionPersistenceStoresTests.cs"),
        FailClosed("hard-deny-violation", "permission", "src/LoopRelay.Certification/MilestoneThreeRunner.cs", EvidenceLevel.LiveTransition),
        FailClosed("unresolved-dual-authority", "authority", "tests/LoopRelay.Orchestration.Primitives.Tests/Resolution/WorkflowResolverTests.cs"),
        FailClosed("closed-evidence-contradicted-by-repository", "completion", "tests/LoopRelay.Roadmap.Cli.Tests/Services/Execution/CompletionCertificationPolicyTests.cs"),
        Recover("process-death-before-request", "interruption", "safe-retry", "src/LoopRelay.Certification/MilestoneFourRunner.cs", EvidenceLevel.LiveChainRecovery),
        Recover("process-death-after-write", "interruption", "safe-retry-before-submission", "src/LoopRelay.Certification/MilestoneFourRunner.cs", EvidenceLevel.LiveChainRecovery),
        Recover("process-death-after-acceptance", "interruption", "reconcile-provider", "src/LoopRelay.Certification/MilestoneFourRunner.cs", EvidenceLevel.LiveChainRecovery),
        Recover("process-death-during-output", "interruption", "reconcile-or-materialize", "src/LoopRelay.Certification/MilestoneFourRunner.cs", EvidenceLevel.LiveChainRecovery),
        Recover("process-death-after-terminal", "interruption", "materialize-committed-output", "src/LoopRelay.Certification/MilestoneFourRunner.cs", EvidenceLevel.LiveChainRecovery),
        Recover("process-death-at-ordered-effect", "interruption", "fail-closed-unknown-side-effect", "src/LoopRelay.Certification/MilestoneFourRunner.cs", EvidenceLevel.LiveChainRecovery),
        Recover("provider-outage", "provider", "no-blind-retry", "tests/LoopRelay.Cli.Tests/Services/Agents/GatedAgentRuntimeTests.cs"),
        Recover("retry-exhaustion", "provider", "bounded-terminal-failure", "tests/LoopRelay.Cli.Tests/Services/Agents/GatedAgentRuntimeTests.cs"),
        FailClosed("git-publication-failure", "git", "src/LoopRelay.Certification/MilestoneSevenRunner.cs", EvidenceLevel.LiveTransition),
        FailClosed("evaluator-failure", "oracle", "tests/LoopRelay.Roadmap.Cli.Tests/Services/Execution/CompletionCertificationPolicyTests.cs"),
        Recover("archive-recovery", "archive", "resume-singular-closure", "src/LoopRelay.Certification/MilestoneElevenRunner.cs", EvidenceLevel.LiveChainRecovery),
        Unsupported("provider-session-reconstruction-live", "provider", "profile-gated", "provider-compatibility", "Recertify when an exact profile exposes a reconstructable provider history contract."),
        Unsupported("native-fork-reconciliation-live", "provider", "profile-gated", "provider-compatibility", "Recertify when exact parent-child enumeration is live-certified."),
        Unsupported("provider-capacity-signal-live", "provider", "profile-gated", "provider-compatibility", "Recertify when the provider exposes a certified capacity signal."),
        Unsupported("ambiguous-provider-effect-reconciliation-live", "provider", "operator-unblock", "provider-compatibility", "Recertify when accepted-turn reconciliation is deterministic for the exact profile."),
    ];

    public async Task<MilestoneTwelveCertificationResult> RunAsync(
        string workspaceRoot,
        string authorityRoot,
        CancellationToken cancellationToken = default)
    {
        var failures = MaintainedFailures.Select(spec => EvaluateFailure(workspaceRoot, spec)).ToArray();
        TransitionRecoveryCoverageResult[] transitions = CanonicalWorkflowDefinitionSketches.CreateAll()
            .SelectMany(workflow => workflow.Transitions.Select(transition => EvaluateTransition(workflow, transition)))
            .OrderBy(item => item.Workflow, StringComparer.Ordinal)
            .ThenBy(item => item.Transition, StringComparer.Ordinal)
            .ToArray();
        OracleControlCaseResult[] oracles = CreateOracleControls();
        CertificationGovernanceResult governance = Governance();

        bool everyFailure = failures.All(item => item.Passed);
        bool everyTransition = transitions.Length > 0 && transitions.All(item => item.Passed);
        bool noDuplicates = transitions.All(item =>
            item.DuplicateProviderTurnPrevented && item.DuplicateOrderedEffectPrevented);
        bool unsupportedVisible = failures.Where(item => !item.Supported).Any() &&
            failures.Where(item => !item.Supported).All(item =>
                item.ReviewedExclusion && !string.IsNullOrWhiteSpace(item.Owner) &&
                !string.IsNullOrWhiteSpace(item.RecertificationCondition));
        bool passed = everyFailure && everyTransition && noDuplicates && unsupportedVisible &&
            oracles.All(item => item.Passed) && governance.Passed;
        string[] evidence =
        [
            $"maintained-failure-denominator:{failures.Length}",
            $"canonical-transition-denominator:{transitions.Length}",
            $"prompt-postures:{string.Join(',', transitions.Select(item => item.Posture).Distinct().Order())}",
            $"effect-categories:{string.Join(',', transitions.SelectMany(item => item.EffectCategories).Distinct().Order())}",
            $"oracle-classes:{oracles.Length}",
            $"reviewed-unsupported:{failures.Count(item => !item.Supported)}",
            $"production-digest:{CoverageLedgerBuilder.Build(workspaceRoot).ProductionDigest}",
        ];
        IReadOnlyList<string> privacy = PrivacyScanner.Scan(string.Join('\n', evidence), authorityRoot);
        CertificationClassification classification = privacy.Count > 0
            ? CertificationClassification.OracleDrift
            : passed ? CertificationClassification.Passed : CertificationClassification.ProductRegression;
        var result = new MilestoneTwelveCertificationResult(
            CertificationRunner.ResultSchemaVersion,
            classification,
            failures,
            transitions,
            oracles,
            governance,
            everyFailure,
            everyTransition,
            noDuplicates,
            unsupportedVisible,
            privacy,
            evidence);
        string path = Path.Combine(authorityRoot, "evidence", "milestone-12.latest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
        return result;
    }

    private static FailureCoverageCaseResult EvaluateFailure(string workspaceRoot, FailureSpec spec)
    {
        bool evidenceExists = !spec.Supported || File.Exists(Path.Combine(
            workspaceRoot,
            spec.EvidencePath!.Replace('/', Path.DirectorySeparatorChar)));
        bool exclusionValid = spec.Supported ||
            (spec.ReviewedExclusion && !string.IsNullOrWhiteSpace(spec.Owner) &&
             !string.IsNullOrWhiteSpace(spec.RecertificationCondition));
        return new FailureCoverageCaseResult(
            spec.Identity,
            spec.Domain,
            spec.Disposition,
            spec.Level,
            spec.Supported,
            spec.ReviewedExclusion,
            spec.Owner,
            spec.RecertificationCondition,
            evidenceExists && exclusionValid,
            spec.Supported ? [$"source:{spec.EvidencePath}"] : ["release-visible:unsupported-profile-capability"]);
    }

    private static TransitionRecoveryCoverageResult EvaluateTransition(
        WorkflowDefinition workflow,
        WorkflowTransitionDefinition transition)
    {
        TransitionRecoveryDecision safe = TransitionRecoveryClassifier.Classify(Snapshot(
            transition.Identity, TransitionDurableState.Cancelled, TransitionBoundaryKind.PreSubmission));
        TransitionRecoveryDecision uncertainProvider = TransitionRecoveryClassifier.Classify(Snapshot(
            transition.Identity, TransitionDurableState.Blocked, TransitionBoundaryKind.RequestAccepted));
        TransitionRecoveryDecision postValidation = TransitionRecoveryClassifier.Classify(Snapshot(
            transition.Identity,
            TransitionDurableState.OutputValidated,
            TransitionBoundaryKind.OutputValidated,
            raw: new PromptExecutionResult(PromptExecutionStatus.Completed, "VALID", TimeSpan.Zero,
                new Dictionary<string, string>())));
        bool effectful = transition.Effects.Count > 0;
        TransitionRecoveryDecision? partialEffect = effectful
            ? TransitionRecoveryClassifier.Classify(Snapshot(
                transition.Identity, TransitionDurableState.EffectsPartiallyApplied, TransitionBoundaryKind.DuringEffects))
            : null;
        TransitionRecoveryDecision? appliedEffect = effectful
            ? TransitionRecoveryClassifier.Classify(Snapshot(
                transition.Identity, TransitionDurableState.EffectsApplied, TransitionBoundaryKind.EffectsApplied))
            : null;

        bool safeCovered = safe.Disposition == TransitionRecoveryDisposition.SafeRetry && safe.MaySubmitProviderTurn;
        bool uncertainCovered = uncertainProvider.Disposition == TransitionRecoveryDisposition.ReconcileProvider &&
            !uncertainProvider.MaySubmitProviderTurn && !uncertainProvider.MayApplyEffects;
        bool postCovered = postValidation.Disposition == TransitionRecoveryDisposition.MaterializeCommittedOutput &&
            !postValidation.MaySubmitProviderTurn && postValidation.MayApplyEffects;
        bool effectCovered = !effectful ||
            (partialEffect!.Disposition == TransitionRecoveryDisposition.FailClosedUnknownSideEffect &&
             !partialEffect.MaySubmitProviderTurn && !partialEffect.MayApplyEffects &&
             appliedEffect!.Disposition == TransitionRecoveryDisposition.CompleteWithoutWork &&
             !appliedEffect.MaySubmitProviderTurn && !appliedEffect.MayApplyEffects);
        bool duplicateProviderPrevented = uncertainCovered && postCovered;
        bool duplicateEffectPrevented = effectCovered;
        return new TransitionRecoveryCoverageResult(
            workflow.Identity.Value,
            transition.Identity.Value,
            transition.ExecutionPosture.Kind.ToString(),
            transition.Effects.Select(item => item.Category.ToString()).Distinct().Order().ToArray(),
            safeCovered,
            uncertainCovered,
            postCovered,
            effectCovered,
            duplicateProviderPrevented,
            duplicateEffectPrevented,
            safeCovered && uncertainCovered && postCovered && effectCovered &&
                duplicateProviderPrevented && duplicateEffectPrevented,
            [
                $"recovery:{transition.Recovery.Identity}",
                $"safe:{safe.Disposition}",
                $"uncertain:{uncertainProvider.Disposition}",
                $"post-validation:{postValidation.Disposition}",
                $"ordered-effect:{(effectful ? partialEffect!.Disposition : "not-applicable")}",
            ]);
    }

    private static TransitionRunRecoverySnapshot Snapshot(
        WorkflowTransitionIdentity transition,
        TransitionDurableState state,
        TransitionBoundaryKind boundary,
        PromptExecutionResult? raw = null) =>
        new(
            "milestone-12",
            transition,
            state,
            RuntimeOutcomeKind.Waiting,
            "input-hash",
            raw,
            [],
            [new TransitionBoundaryObservation(
                "milestone-12", transition, boundary, 1, DateTimeOffset.UnixEpoch,
                "input-hash", null, ["deterministic-boundary-control"])],
            "deterministic recovery classifier control",
            ["milestone-12"]);

    private static OracleControlCaseResult[] CreateOracleControls() =>
    [
        Oracle("exact", value => value == "EXPECTED", "EXPECTED", "ACTUAL"),
        Oracle("structural", value => value.StartsWith("# Artifact\n", StringComparison.Ordinal) && value.Contains("## Evidence", StringComparison.Ordinal), "# Artifact\n## Evidence", "Artifact without headings"),
        Oracle("semantic", value => value.Contains("capability:ready", StringComparison.Ordinal), "result capability:ready", "result capability:unknown"),
        Oracle("invariant", value => value == "authority=singular", "authority=singular", "authority=dual"),
        Oracle("state", value => value == "Completed:null", "Completed:null", "Completed:Completion"),
        Oracle("graph", value => value == "acyclic:A>B>C", "acyclic:A>B>C", "cycle:A>B>A"),
        Oracle("workflow", value => value == "Execute/Completion/CertifiedCompletion", "Execute/Completion/CertifiedCompletion", "Execute/Execution/CertifiedCompletion"),
        Oracle("persistence", value => value == "schema=3;integrity=ok", "schema=3;integrity=ok", "schema=99;integrity=unknown"),
        Oracle("protocol", value => value == "accepted;terminal;id=1", "accepted;terminal;id=1", "terminal;id=missing"),
        Oracle("git", value => value == "head=abc;remote=abc;clean=true", "head=abc;remote=abc;clean=true", "head=abc;remote=def;clean=true"),
        Oracle("repository-acceptance", value => value == "exit=0;verifier-unchanged=true", "exit=0;verifier-unchanged=true", "exit=1;verifier-unchanged=true"),
    ];

    private static OracleControlCaseResult Oracle(
        string identity,
        Func<string, bool> evaluate,
        string positive,
        string negative)
    {
        bool accepts = evaluate(positive);
        bool rejects = !evaluate(negative);
        return new OracleControlCaseResult(identity, accepts, rejects, accepts && rejects,
            ["positive-control", "deliberate-negative-control"]);
    }

    private static CertificationGovernanceResult Governance()
    {
        var retention = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["success"] = "normalized-summary-and-required-artifacts",
            ["failure"] = "full-scrubbed-boundary-and-oracle-evidence",
            ["flake"] = "all-attempts-plus-variance-classification",
            ["block"] = "durable-operator-action-and-authority-snapshot",
            ["incompatibility"] = "exact-profile-identity-and-missing-capability",
            ["privacy-sensitive"] = "redacted-summary-only-with-local-retention-pointer",
        };
        return new CertificationGovernanceResult(
            3,
            0.05,
            "Rerun identical behavior identity; classify variance before product blame; never erase the first failure.",
            true,
            true,
            retention,
            retention.Count == 6);
    }

    private static FailureSpec Recover(
        string identity,
        string domain,
        string disposition,
        string evidence,
        EvidenceLevel level = EvidenceLevel.DeterministicComponent) =>
        new(identity, domain, disposition, level, true, false, evidence, null, null);

    private static FailureSpec FailClosed(
        string identity,
        string domain,
        string evidence,
        EvidenceLevel level = EvidenceLevel.DeterministicComponent) =>
        Recover(identity, domain, "fail-closed", evidence, level);

    private static FailureSpec Unsupported(
        string identity,
        string domain,
        string disposition,
        string owner,
        string recertification) =>
        new(identity, domain, disposition, EvidenceLevel.Uncovered, false, true, null, owner, recertification);

    private sealed record FailureSpec(
        string Identity,
        string Domain,
        string Disposition,
        EvidenceLevel Level,
        bool Supported,
        bool ReviewedExclusion,
        string? EvidencePath,
        string? Owner,
        string? RecertificationCondition);
}
