using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Certification;

public sealed class FailureOracleMatrixRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    // Declared taxonomy of the failure modes the product claims to handle, with the disposition
    // each one is expected to take. Supported rows are documentation: nothing here verifies them,
    // and nothing here should pretend to. Unsupported rows additionally carry the exclusion review
    // (owner plus recertification condition) that RunAsync does check and that keeps every
    // uncovered profile capability release-visible.
    private static readonly FailureSpec[] MaintainedFailures =
    [
        Recover("repaired-context", "authority", "safe-retry"),
        Recover("corrected-malformed-output", "prompt-output", "safe-retry"),
        Recover("canonical-artifact-restoration", "artifact", "operator-unblock"),
        Recover("projection-regeneration", "projection", "deterministic-regeneration"),
        Recover("scoped-rollback", "artifact", "scoped-rollback"),
        Recover("incomplete-split-or-promotion", "artifact", "resume-or-fail-closed"),
        Recover("stranded-publication", "git", "operator-unblock", EvidenceLevel.LiveTransition),
        Recover("missing-parent-pointer", "recovery", "operator-unblock"),
        Recover("changed-implementation-without-handoff", "completion", "continue-execution"),
        Recover("handoff-without-publication", "completion", "continue-execution"),
        Recover("committed-decision-without-artifact", "recovery", "materialize-committed-output"),
        Recover("pointer-conflict", "persistence", "compare-and-swap-fail-closed"),
        Recover("partial-archive-or-context-update", "archive", "resume-singular-closure"),
        Recover("cancelled-output", "provider", "boundary-classification"),
        Recover("corrected-stall", "workflow", "explicit-rerun"),
        Recover("usage-limit-after-failure", "provider", "bounded-wait-retry"),
        FailClosed("unsupported-schema-or-profile", "configuration"),
        FailClosed("untrusted-corrupt-authority", "persistence", EvidenceLevel.LiveTransition),
        FailClosed("ambiguous-provider-side-effect", "provider", EvidenceLevel.LiveChainRecovery),
        FailClosed("multiple-fork-children", "recovery"),
        FailClosed("causal-mismatch", "recovery"),
        FailClosed("recovery-marker-mismatch", "persistence"),
        FailClosed("hard-deny-violation", "permission", EvidenceLevel.LiveTransition),
        FailClosed("unresolved-dual-authority", "authority"),
        FailClosed("closed-evidence-contradicted-by-repository", "completion"),
        Recover("process-death-before-request", "interruption", "safe-retry", EvidenceLevel.LiveChainRecovery),
        Recover("process-death-after-write", "interruption", "safe-retry-before-submission", EvidenceLevel.LiveChainRecovery),
        Recover("process-death-after-acceptance", "interruption", "reconcile-provider", EvidenceLevel.LiveChainRecovery),
        Recover("process-death-during-output", "interruption", "reconcile-or-materialize", EvidenceLevel.LiveChainRecovery),
        Recover("process-death-after-terminal", "interruption", "materialize-committed-output", EvidenceLevel.LiveChainRecovery),
        Recover("process-death-at-ordered-effect", "interruption", "fail-closed-unknown-side-effect", EvidenceLevel.LiveChainRecovery),
        Recover("provider-outage", "provider", "no-blind-retry"),
        Recover("retry-exhaustion", "provider", "bounded-terminal-failure"),
        FailClosed("git-publication-failure", "git", EvidenceLevel.LiveTransition),
        FailClosed("evaluator-failure", "oracle"),
        Recover("archive-recovery", "archive", "resume-singular-closure", EvidenceLevel.LiveChainRecovery),
    ];

    // Profile capabilities the product does not support. Unlike the taxonomy above, these are
    // evaluated: each row must carry a completed review - the reviewed flag, an owning function,
    // and the condition that would put it back on the certification hook. A row added without one
    // fails the matrix, which is what keeps an exclusion from being quietly buried in the table.
    private static readonly ExclusionSpec[] ReviewedExclusions =
    [
        Unsupported("provider-session-reconstruction-live", "provider", "profile-gated", reviewed: true, "provider-compatibility", "Recertify when an exact profile exposes a reconstructable provider history contract."),
        Unsupported("native-fork-reconciliation-live", "provider", "profile-gated", reviewed: true, "provider-compatibility", "Recertify when exact parent-child enumeration is live-certified."),
        Unsupported("provider-capacity-signal-live", "provider", "profile-gated", reviewed: true, "provider-compatibility", "Recertify when the provider exposes a certified capacity signal."),
        Unsupported("ambiguous-provider-effect-reconciliation-live", "provider", "operator-unblock", reviewed: true, "provider-compatibility", "Recertify when accepted-turn reconciliation is deterministic for the exact profile."),
    ];

    public async Task<FailureOracleMatrixCertificationResult> RunAsync(
        string workspaceRoot,
        string authorityRoot,
        CancellationToken cancellationToken = default)
    {
        FailureCoverageCaseResult[] exclusions = ReviewedExclusions
            .Select(spec => FailureCoverageCaseResult.ForReviewedExclusion(
                spec.Identity,
                spec.Domain,
                spec.Disposition,
                spec.Reviewed,
                spec.Owner,
                spec.RecertificationCondition,
                ["release-visible:unsupported-profile-capability"]))
            .ToArray();
        TransitionRecoveryCoverageResult[] transitions = CanonicalWorkflowCatalog.Current.Workflows
            .SelectMany(workflow => workflow.Transitions.Select(transition => EvaluateTransition(workflow, transition)))
            .OrderBy(item => item.Workflow, StringComparer.Ordinal)
            .ThenBy(item => item.Transition, StringComparer.Ordinal)
            .ToArray();
        OracleControlCaseResult[] oracles = CreateOracleControls();

        bool everyTransition = transitions.Length > 0 && transitions.All(item => item.Passed);
        bool noDuplicates = transitions.All(item =>
            item.DuplicateProviderTurnPrevented && item.DuplicateOrderedEffectPrevented);
        bool unsupportedVisible = exclusions.Length > 0 && exclusions.All(item => item.Passed);
        bool passed = everyTransition && noDuplicates && unsupportedVisible &&
            oracles.All(item => item.Passed);
        string[] evidence =
        [
            $"declared-failure-taxonomy:{MaintainedFailures.Length}",
            $"declared-taxonomy-levels:{string.Join(',', MaintainedFailures
                .GroupBy(item => item.Level)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key}={group.Count()}"))}",
            $"canonical-transition-denominator:{transitions.Length}",
            $"prompt-postures:{string.Join(',', transitions.Select(item => item.Posture).Distinct().Order())}",
            $"effect-categories:{string.Join(',', transitions.SelectMany(item => item.EffectCategories).Distinct().Order())}",
            $"oracle-classes:{oracles.Length}",
            $"reviewed-unsupported:{exclusions.Length}",
            $"production-digest:{CoverageLedgerBuilder.Build(workspaceRoot).ProductionDigest}",
        ];
        IReadOnlyList<string> privacy = PrivacyScanner.Scan(string.Join('\n', evidence), authorityRoot);
        CertificationClassification classification = privacy.Count > 0
            ? CertificationClassification.OracleDrift
            : passed ? CertificationClassification.Passed : CertificationClassification.ProductRegression;
        var result = new FailureOracleMatrixCertificationResult(
            CertificationEvidenceSchema.Version,
            classification,
            exclusions,
            transitions,
            oracles,
            everyTransition,
            noDuplicates,
            unsupportedVisible,
            privacy,
            evidence);
        string path = Path.Combine(authorityRoot, "evidence", "failure-oracle-matrix.latest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
        return result;
    }

    private static TransitionRecoveryCoverageResult EvaluateTransition(
        WorkflowDefinition workflow,
        WorkflowTransitionDefinition transition)
    {
        TransitionRecoveryDecision safe = TransitionRecoveryClassifier.Classify(Snapshot(
            transition.Identity, TransitionDurableState.Cancelled, TransitionBoundaryKind.PreSubmission));
        TransitionRecoveryDecision uncertainProvider = TransitionRecoveryClassifier.Classify(Snapshot(
            transition.Identity, TransitionDurableState.ProviderOutcomeUnknown, TransitionBoundaryKind.RequestAccepted));
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

        bool safeCovered = safe.Disposition == TransitionRecoveryDisposition.Cancelled &&
            !safe.MaySubmitProviderTurn && !safe.MayApplyEffects;
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
        PromptExecutionResult? raw = null)
    {
        CanonicalCausalContext causality = new(
            new WorkspaceIdentity("workspace_failure_oracle_matrix"),
            new RunIdentity("run_failure_oracle_matrix"),
            new WorkflowInstanceIdentity("workflow_instance_failure_oracle_matrix"),
            new TransitionRunIdentity("transition_run_failure_oracle_matrix"),
            new AttemptIdentity("attempt_failure_oracle_matrix"));
        return new(
            causality,
            transition,
            state,
            RuntimeOutcomeKind.Waiting,
            "input-hash",
            raw,
            [],
            [new TransitionBoundaryObservation(
                causality, transition, boundary, 1, DateTimeOffset.UnixEpoch,
                "input-hash", null, ["deterministic-boundary-control"])],
            "deterministic recovery classifier control",
            ["failure-oracle-matrix"]);
    }

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

    private static FailureSpec Recover(
        string identity,
        string domain,
        string disposition,
        EvidenceLevel level = EvidenceLevel.DeterministicComponent) =>
        new(identity, domain, disposition, level);

    private static FailureSpec FailClosed(
        string identity,
        string domain,
        EvidenceLevel level = EvidenceLevel.DeterministicComponent) =>
        Recover(identity, domain, "fail-closed", level);

    private static ExclusionSpec Unsupported(
        string identity,
        string domain,
        string disposition,
        bool reviewed,
        string owner,
        string recertification) =>
        new(identity, domain, disposition, reviewed, owner, recertification);

    /// <summary>A declared failure mode and the disposition it is expected to take. Documentation.</summary>
    private sealed record FailureSpec(
        string Identity,
        string Domain,
        string Disposition,
        EvidenceLevel Level);

    /// <summary>A deliberately unsupported capability and the review that keeps it release-visible.</summary>
    private sealed record ExclusionSpec(
        string Identity,
        string Domain,
        string Disposition,
        bool Reviewed,
        string Owner,
        string RecertificationCondition);
}
