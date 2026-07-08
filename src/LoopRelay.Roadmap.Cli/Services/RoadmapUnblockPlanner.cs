using LoopRelay.Completion;

namespace LoopRelay.Roadmap.Cli;

internal sealed class RoadmapUnblockPlanner(
    RoadmapArtifacts artifacts,
    ProjectContextLoader projectContextLoader,
    PromptContractRegistry contractRegistry,
    CompletionCertificationPolicy completionPolicy,
    CompletionCertificationRouter completionRouter,
    ExecutionPreparationProvenanceService executionPreparation)
{
    private readonly ExecutionDispositionParser executionDispositionParser = new();
    private readonly ExecutionDispositionPolicy executionDispositionPolicy = new();

    public async Task<RoadmapUnblockPlan> PlanAsync(
        RoadmapStateDocument? persistedState,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (persistedState is null)
        {
            return RoadmapUnblockPlan.Unsupported(
                RoadmapState.CoreReady,
                RoadmapTransitionIntent.Empty(RoadmapState.CoreReady),
                "No persisted roadmap state exists, so there is no blocker to review.",
                [],
                "Run the roadmap CLI to initialize the workflow.");
        }

        if (!IsBlockedRecoveryState(persistedState.CurrentState))
        {
            return RoadmapUnblockPlan.Unsupported(
                persistedState.CurrentState,
                persistedState.TransitionIntent,
                $"Persisted roadmap state {persistedState.CurrentState} is not a blocked recovery state.",
                [],
                "Use `run` for active workflow transitions or `status` for reporting.");
        }

        return persistedState.TransitionIntent.Intent switch
        {
            "ResolveBlocker" => await PlanResolveBlockerAsync(persistedState, cancellationToken),
            "ResolveMalformedExecutionOutput" => await PlanResolveMalformedExecutionOutputAsync(persistedState, cancellationToken),
            "ResolveInvalidCompletionCertification" => await PlanResolveInvalidCompletionCertificationAsync(persistedState, cancellationToken),
            "RepairExecutionRuntimeFailure" => await PlanRepairExecutionRuntimeFailureAsync(persistedState, cancellationToken),
            "ResolveArtifactPromotionBlocker" => await UnsupportedIntentAsync(
                persistedState,
                "Artifact promotion blockers require artifact-specific repair and promotion policy support before automatic unblock can be safe."),
            "ResolveSplitEpicBlocker" => await UnsupportedIntentAsync(
                persistedState,
                "Split epic blockers require bundle-level repair and child-selection validation support before automatic unblock can be safe."),
            "ResolveTransitionFailure" => await UnsupportedIntentAsync(
                persistedState,
                "Prompt transition failures are not automatically retried because retry safety and prompt idempotency are not yet modeled."),
            "ResolveInvariantViolation" => await UnsupportedIntentAsync(
                persistedState,
                "Invariant violation recovery requires validator-specific repair and revalidation before automatic unblock can be safe."),
            "ResolveExecutionBlocker" => await UnsupportedIntentAsync(
                persistedState,
                "Execution blocker recovery needs an execution-blocker-specific contract; the current unblock planner does not yet define one."),
            _ => await UnsupportedIntentAsync(
                persistedState,
                $"No unblock handler is registered for transition intent `{persistedState.TransitionIntent.Intent}`."),
        };
    }

    private async Task<RoadmapUnblockPlan> PlanResolveBlockerAsync(
        RoadmapStateDocument state,
        CancellationToken cancellationToken)
    {
        if (state.CurrentState != RoadmapState.EvidenceBlocked ||
            !string.Equals(state.LastTransition.Prompt, "Preflight", StringComparison.Ordinal))
        {
            return RoadmapUnblockPlan.Unsupported(
                state.CurrentState,
                state.TransitionIntent,
                "ResolveBlocker unblock is only supported for fresh Project Context preflight blockers.",
                await HashExistingEvidenceAsync(state.TransitionIntent.EvidencePaths),
                "Use status to inspect this blocker until an intent-specific unblock handler exists.");
        }

        ProjectContext projectContext;
        try
        {
            projectContext = await projectContextLoader.LoadAsync(cancellationToken);
            await contractRegistry.EmitSnapshotAsync(artifacts);
        }
        catch (RoadmapStepException exception)
        {
            return RoadmapUnblockPlan.Failed(
                RoadmapUnblockAction.RecoverToCoreReady,
                state.CurrentState,
                state.TransitionIntent,
                $"Project Context preflight is still invalid: {OneLine(exception.Message)}",
                await HashExistingEvidenceAsync(state.TransitionIntent.EvidencePaths),
                "Repair the Project Context source files and run `unblock` again.");
        }

        try
        {
            _ = await artifacts.ReadRoadmapSourceAsync();
        }
        catch (RoadmapStepException exception)
        {
            IReadOnlyList<RoadmapUnblockEvidence> failureEvidence =
            [
                ..await HashExistingEvidenceAsync(state.TransitionIntent.EvidencePaths),
                new RoadmapUnblockEvidence("ProjectContext", "ProjectContext", projectContext.Hash, "Present"),
            ];
            return RoadmapUnblockPlan.Failed(
                RoadmapUnblockAction.RecoverToCoreReady,
                state.CurrentState,
                state.TransitionIntent,
                $"Roadmap source readiness is still invalid: {OneLine(exception.Message)}",
                failureEvidence,
                "Restore roadmap source evidence and run `unblock` again.");
        }

        IReadOnlyList<RoadmapUnblockEvidence> evidence =
        [
            ..await HashExistingEvidenceAsync(state.TransitionIntent.EvidencePaths),
            new RoadmapUnblockEvidence("ProjectContext", "ProjectContext", projectContext.Hash, "Present"),
            ..await HashProjectContextSourcesAsync(),
            ..await HashRoadmapSourceAsync(),
        ];

        return RoadmapUnblockPlan.Success(
            RoadmapUnblockAction.RecoverToCoreReady,
            state.CurrentState,
            state.TransitionIntent,
            "Project Context preflight and roadmap source readiness are valid.",
            evidence,
            RoadmapState.CoreReady,
            "Project Context blocker resolved.");
    }

    private async Task<RoadmapUnblockPlan> PlanResolveMalformedExecutionOutputAsync(
        RoadmapStateDocument state,
        CancellationToken cancellationToken)
    {
        ProjectContextHealth contextHealth = await CheckProjectContextAsync(cancellationToken);
        IReadOnlyList<RoadmapUnblockEvidence> baseEvidence = await HashExistingEvidenceAsync(state.TransitionIntent.EvidencePaths);
        if (!contextHealth.IsHealthy)
        {
            return RoadmapUnblockPlan.Failed(
                RoadmapUnblockAction.RecoverExecutionDisposition,
                state.CurrentState,
                state.TransitionIntent,
                contextHealth.Reason,
                baseEvidence,
                "Repair Project Context and run `unblock` again.");
        }
        IReadOnlyList<RoadmapUnblockEvidence> reviewEvidence = WithProjectContextEvidence(baseEvidence, contextHealth);

        string? evidencePath = SingleEvidencePath(
            state.TransitionIntent.EvidencePaths,
            RoadmapArtifactPaths.ExecutionEvidenceDirectory);
        if (evidencePath is null)
        {
            return RoadmapUnblockPlan.Failed(
                RoadmapUnblockAction.RecoverExecutionDisposition,
                state.CurrentState,
                state.TransitionIntent,
                "ResolveMalformedExecutionOutput requires exactly one execution evidence path.",
                reviewEvidence,
                "Repair the persisted transition intent evidence paths before running unblock again.");
        }

        if (!OutputContains(state.LastTransition.Output, evidencePath))
        {
            return RoadmapUnblockPlan.Failed(
                RoadmapUnblockAction.RecoverExecutionDisposition,
                state.CurrentState,
                state.TransitionIntent,
                $"Execution evidence `{evidencePath}` does not match the persisted last transition output.",
                reviewEvidence,
                "Restore the original execution evidence relationship before running unblock again.");
        }

        string? content = await artifacts.ReadAsync(evidencePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return RoadmapUnblockPlan.Failed(
                RoadmapUnblockAction.RecoverExecutionDisposition,
                state.CurrentState,
                state.TransitionIntent,
                $"Execution evidence is missing or empty: {evidencePath}",
                reviewEvidence,
                "Restore the execution evidence file and run `unblock` again.");
        }

        try
        {
            ExecutionDisposition disposition = executionDispositionParser.Parse(content);
            ExecutionDispositionValidationResult validation = executionDispositionPolicy.Validate(disposition);
            if (!validation.IsValid)
            {
                return RoadmapUnblockPlan.Failed(
                    RoadmapUnblockAction.RecoverExecutionDisposition,
                    state.CurrentState,
                    state.TransitionIntent,
                    validation.ViolationReason ?? "Execution disposition policy rejected the repaired evidence.",
                    reviewEvidence,
                    validation.RequiredRecoveryPath);
            }

            return RoadmapUnblockPlan.ExecutionDispositionRecovered(
                state.CurrentState,
                state.TransitionIntent,
                $"Execution disposition repaired as {disposition.StatusText}.",
                reviewEvidence,
                evidencePath,
                validation);
        }
        catch (MarkdownParseException exception)
        {
            return RoadmapUnblockPlan.Failed(
                RoadmapUnblockAction.RecoverExecutionDisposition,
                state.CurrentState,
                state.TransitionIntent,
                $"Execution disposition evidence is still malformed: {OneLine(exception.Message)}",
                reviewEvidence,
                "Correct the Execution Disposition table and run `unblock` again.");
        }
    }

    private async Task<RoadmapUnblockPlan> PlanResolveInvalidCompletionCertificationAsync(
        RoadmapStateDocument state,
        CancellationToken cancellationToken)
    {
        ProjectContextHealth contextHealth = await CheckProjectContextAsync(cancellationToken);
        IReadOnlyList<RoadmapUnblockEvidence> baseEvidence = await HashExistingEvidenceAsync(state.TransitionIntent.EvidencePaths);
        if (!contextHealth.IsHealthy)
        {
            return RoadmapUnblockPlan.Failed(
                RoadmapUnblockAction.RecoverCompletionCertification,
                state.CurrentState,
                state.TransitionIntent,
                contextHealth.Reason,
                baseEvidence,
                "Repair Project Context and run `unblock` again.");
        }
        IReadOnlyList<RoadmapUnblockEvidence> reviewEvidence = WithProjectContextEvidence(baseEvidence, contextHealth);

        string? evaluationPath = SingleEvidencePath(
            state.TransitionIntent.EvidencePaths,
            RoadmapArtifactPaths.EvaluationEvidenceDirectory);
        if (evaluationPath is null)
        {
            return RoadmapUnblockPlan.Failed(
                RoadmapUnblockAction.RecoverCompletionCertification,
                state.CurrentState,
                state.TransitionIntent,
                "ResolveInvalidCompletionCertification requires exactly one evaluation evidence path.",
                reviewEvidence,
                "Restore the completion evaluation evidence path and run `unblock` again.");
        }

        if (!OutputContains(state.LastTransition.Output, evaluationPath))
        {
            return RoadmapUnblockPlan.Failed(
                RoadmapUnblockAction.RecoverCompletionCertification,
                state.CurrentState,
                state.TransitionIntent,
                $"Completion evaluation `{evaluationPath}` does not match the persisted last transition output.",
                reviewEvidence,
                "Restore the original completion evaluation relationship before running unblock again.");
        }

        string? content = await artifacts.ReadAsync(evaluationPath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return RoadmapUnblockPlan.Failed(
                RoadmapUnblockAction.RecoverCompletionCertification,
                state.CurrentState,
                state.TransitionIntent,
                $"Completion evaluation evidence is missing or empty: {evaluationPath}",
                reviewEvidence,
                "Restore the completion evaluation evidence and run `unblock` again.");
        }

        try
        {
            CompletionEvaluationDecision decision = new CompletionEvaluationParser().Parse(content);
            CompletionCertificationPolicyResult certification = completionPolicy.Validate(decision);
            if (!certification.IsValid)
            {
                return RoadmapUnblockPlan.Failed(
                    RoadmapUnblockAction.RecoverCompletionCertification,
                    state.CurrentState,
                    state.TransitionIntent,
                    certification.RejectionReason ?? "Completion certification policy rejected the repaired evidence.",
                    reviewEvidence,
                    "Correct the completion evaluation fields and run `unblock` again.");
            }

            CompletionCertificationRoute route = completionRouter.Route(decision);
            return RoadmapUnblockPlan.CompletionCertificationRecovered(
                state.CurrentState,
                state.TransitionIntent,
                $"Completion certification repaired as {decision.ClosureRecommendation}.",
                reviewEvidence,
                evaluationPath,
                certification,
                route);
        }
        catch (CompletionMarkdownParseException exception)
        {
            return RoadmapUnblockPlan.Failed(
                RoadmapUnblockAction.RecoverCompletionCertification,
                state.CurrentState,
                state.TransitionIntent,
                $"Completion evaluation evidence is still malformed: {OneLine(exception.Message)}",
                reviewEvidence,
                "Correct the Evaluation Summary table and run `unblock` again.");
        }
    }

    private async Task<RoadmapUnblockPlan> PlanRepairExecutionRuntimeFailureAsync(
        RoadmapStateDocument state,
        CancellationToken cancellationToken)
    {
        if (state.CurrentState != RoadmapState.Failed)
        {
            return RoadmapUnblockPlan.Unsupported(
                state.CurrentState,
                state.TransitionIntent,
                "RepairExecutionRuntimeFailure is only supported for persisted Failed states.",
                await HashExistingEvidenceAsync(state.TransitionIntent.EvidencePaths),
                "Use status to inspect this blocker until the persisted state matches the recovery intent.");
        }

        ProjectContext projectContext;
        IReadOnlyList<RoadmapUnblockEvidence> baseEvidence = await HashExistingEvidenceAsync(state.TransitionIntent.EvidencePaths);
        try
        {
            projectContext = await projectContextLoader.LoadAsync(cancellationToken);
        }
        catch (RoadmapStepException exception)
        {
            return RoadmapUnblockPlan.Failed(
                RoadmapUnblockAction.RecoverExecutionRuntimeFailure,
                state.CurrentState,
                state.TransitionIntent,
                $"Project Context preflight is invalid: {OneLine(exception.Message)}",
                baseEvidence,
                "Repair Project Context and run `unblock` again.");
        }
        IReadOnlyList<RoadmapUnblockEvidence> reviewEvidence =
        [
            ..baseEvidence,
            new RoadmapUnblockEvidence("ProjectContext", "ProjectContext", projectContext.Hash, "Present"),
        ];

        string? evidencePath = SingleEvidencePath(
            state.TransitionIntent.EvidencePaths,
            RoadmapArtifactPaths.ExecutionEvidenceDirectory);
        if (evidencePath is null)
        {
            return RoadmapUnblockPlan.Failed(
                RoadmapUnblockAction.RecoverExecutionRuntimeFailure,
                state.CurrentState,
                state.TransitionIntent,
                "RepairExecutionRuntimeFailure requires exactly one execution evidence path.",
                reviewEvidence,
                "Restore the execution runtime failure evidence path and run `unblock` again.");
        }

        return RoadmapUnblockPlan.Failed(
            RoadmapUnblockAction.RecoverExecutionRuntimeFailure,
            state.CurrentState,
            state.TransitionIntent,
            "Execution runtime failure recovery belongs to legacy execution preparation and is no longer advanced by Roadmap CLI.",
            reviewEvidence,
            "Recover execution outside Roadmap CLI, then rerun roadmap selection when the roadmap state needs to advance.");
    }

    private async Task<RoadmapUnblockPlan> UnsupportedIntentAsync(RoadmapStateDocument state, string reason) =>
        RoadmapUnblockPlan.Unsupported(
            state.CurrentState,
            state.TransitionIntent,
            reason,
            await HashExistingEvidenceAsync(state.TransitionIntent.EvidencePaths),
            "Preserve the blocker evidence and use the documented manual repair path until this intent has a deterministic unblock handler.");

    private async Task<ProjectContextHealth> CheckProjectContextAsync(CancellationToken cancellationToken)
    {
        try
        {
            ProjectContext context = await projectContextLoader.LoadAsync(cancellationToken);
            return new ProjectContextHealth(true, "Project Context is valid.", context.Hash);
        }
        catch (RoadmapStepException exception)
        {
            return new ProjectContextHealth(
                false,
                $"Project Context preflight is invalid during unblock review: {OneLine(exception.Message)}",
                null);
        }
    }

    private static IReadOnlyList<RoadmapUnblockEvidence> WithProjectContextEvidence(
        IReadOnlyList<RoadmapUnblockEvidence> evidence,
        ProjectContextHealth contextHealth) =>
        string.IsNullOrWhiteSpace(contextHealth.Hash)
            ? evidence
            :
            [
                ..evidence,
                new RoadmapUnblockEvidence("ProjectContext", "ProjectContext", contextHealth.Hash, "Present"),
            ];

    private async Task<IReadOnlyList<RoadmapUnblockEvidence>> HashExistingEvidenceAsync(
        IReadOnlyList<string> paths)
    {
        var evidence = new List<RoadmapUnblockEvidence>();
        foreach (string path in paths.Distinct(StringComparer.Ordinal))
        {
            string? content = await artifacts.ReadAsync(path);
            evidence.Add(new RoadmapUnblockEvidence(
                path,
                "TransitionIntentEvidence",
                string.IsNullOrWhiteSpace(content) ? string.Empty : RoadmapHash.Sha256(content),
                string.IsNullOrWhiteSpace(content) ? "MissingOrEmpty" : "Present"));
        }

        return evidence;
    }

    private async Task<IReadOnlyList<RoadmapUnblockEvidence>> HashProjectContextSourcesAsync()
    {
        var evidence = new List<RoadmapUnblockEvidence>();
        foreach (string path in RoadmapArtifactPaths.ProjectContextSourceFiles)
        {
            string? content = await artifacts.ReadAsync(path);
            if (!string.IsNullOrWhiteSpace(content))
            {
                evidence.Add(new RoadmapUnblockEvidence(path, "ProjectContextSource", RoadmapHash.Sha256(content), "Present"));
            }
        }

        return evidence;
    }

    private async Task<IReadOnlyList<RoadmapUnblockEvidence>> HashRoadmapSourceAsync()
    {
        var evidence = new List<RoadmapUnblockEvidence>();
        IReadOnlyList<string> roadmapFiles = await artifacts.ListAsync(RoadmapArtifactPaths.RoadmapDirectory, "*.md");
        foreach (string path in roadmapFiles.Order(StringComparer.Ordinal))
        {
            string? content = await artifacts.ReadAsync(path);
            if (!string.IsNullOrWhiteSpace(content))
            {
                evidence.Add(new RoadmapUnblockEvidence(path, "RoadmapSource", RoadmapHash.Sha256(content), "Present"));
            }
        }

        return evidence;
    }

    private async Task<IReadOnlyList<RoadmapUnblockEvidence>> HashExecutionReadinessArtifactsAsync()
    {
        var evidence = new List<RoadmapUnblockEvidence>();
        foreach (string path in new[]
        {
            RoadmapArtifactPaths.ActiveEpic,
            RoadmapArtifactPaths.OperationalContext,
            RoadmapArtifactPaths.ExecutionPrompt,
            RoadmapArtifactPaths.ExecutionPlan,
        })
        {
            string? content = await artifacts.ReadAsync(path);
            evidence.Add(new RoadmapUnblockEvidence(
                path,
                "ExecutionReadiness",
                string.IsNullOrWhiteSpace(content) ? string.Empty : RoadmapHash.Sha256(content),
                string.IsNullOrWhiteSpace(content) ? "MissingOrEmpty" : "Present"));
        }

        IReadOnlyList<string> specs = await executionPreparation.RequireFreshMilestoneSpecPathsAsync();
        foreach (string spec in specs)
        {
            string content = await artifacts.ReadRequiredAsync(spec);
            evidence.Add(new RoadmapUnblockEvidence(spec, "MilestoneSpec", RoadmapHash.Sha256(content), "Present"));
        }

        IReadOnlyList<string> milestones = ExecutionPreparationProvenanceService
            .ExpectedCompatibilityMilestonePaths(specs.Count);
        foreach (string milestone in milestones)
        {
            string? content = await artifacts.ReadAsync(milestone);
            evidence.Add(new RoadmapUnblockEvidence(
                milestone,
                "ExecutionCompatibility",
                string.IsNullOrWhiteSpace(content) ? string.Empty : RoadmapHash.Sha256(content),
                string.IsNullOrWhiteSpace(content) ? "MissingOrEmpty" : "Present"));
        }

        return evidence;
    }

    private static string? SingleEvidencePath(IReadOnlyList<string> paths, string directory)
    {
        string[] matches = paths
            .Where(path => path.StartsWith(directory, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static bool OutputContains(string output, string path) =>
        output
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(candidate => string.Equals(candidate, path, StringComparison.Ordinal));

    private static bool IsBlockedRecoveryState(RoadmapState state) =>
        state is RoadmapState.EvidenceBlocked
            or RoadmapState.Failed
            or RoadmapState.ExecutionBlocked;

    private static string OneLine(string value) =>
        string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private sealed record ProjectContextHealth(bool IsHealthy, string Reason, string? Hash);
}
